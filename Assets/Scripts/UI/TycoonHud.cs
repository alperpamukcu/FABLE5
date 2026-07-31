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

        // day end
        private RectTransform _dayEndPanel;
        private Text _invoiceText;
        private RectTransform _offerRow;
        private RectTransform _openTomorrow;
        private Text _bannerText;

        // ledger history (GDD 24 §7, 2026-07-22): the register opens the book of past days.
        private RectTransform _ledgerPanel;
        private RectTransform _ledgerRows;

        // ID card (GDD 24 §5): the licence you read a customer by. Emotion→recipe pivot
        // (2026-07-22): it now shows the drink's RECIPE and the garnishes they want, not moods.
        private RectTransform _idRoot;
        private Image _idPhoto;
        private Text _idName, _idAgeFrom, _idRel, _idIntent, _idOrder, _idRates, _idRatesLabel;
        private Image _idOrderIcon;

        // The shop tablet (v5 P13). Two errands, not one wall of cards: what goes behind the
        // bar, and what the room itself is made of.
        private static readonly string[] ShopTabs = { "THE WELL", "THE ROOM" };
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
                    prop.anchoredPosition = new Vector2(v.SeatX, CounterLineY + 2f);
                    var img = prop.gameObject.AddComponent<Image>();
                    img.sprite = ItemArt.Glass;
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
            _binProp.sizeDelta = new Vector2(78, 93);
            _binProp.anchoredPosition = new Vector2(-70, 96);
            _binImage = _binProp.gameObject.AddComponent<Image>();
            _binImage.sprite = ItemArt.Load("bin_prop");
            _binImage.preserveAspect = true;
            _binImage.raycastTarget = false;
            _binImage.color = new Color(0.72f, 0.72f, 0.74f, 1f);
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

            // The liquid goes in first so the hollow glass draws over it, and it is clipped to
            // the interior silhouette — a martini's drink narrows into the cone rather than
            // being a rectangle poking through the walls.
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
            var piece = GlassArt.For(run.ServingGlassware);
            if (!ReferenceEquals(_drinkGlassware, run.ServingGlassware) || _drinkGlassArt.sprite == null)
            {
                _drinkGlassware = run.ServingGlassware;
                _drinkGlassArt.sprite = piece.Sprite;
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
                    run.DiscardGlass();
                    Toast("BINNED");
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
        private Color DrinkColor()
        {
            var run = Run;
            var glass = run?.ServingGlass;
            if (glass == null || glass.IsEmpty) return UITheme.Amber[3];
            var parts = new List<(string, IngredientType, float)>();
            foreach (var id in glass.Ingredients)
            {
                var card = run.Shelf.Find(id)?.Ingredient;
                parts.Add((card?.Info?.Style, card?.Type ?? IngredientType.Spirit, (float)glass.RatioOf(id)));
            }
            return UITheme.BlendLiquid(parts, UITheme.Amber[3], 0.92f);
        }

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
            if (stage == null || run == null) return;
            stage.ApplyBarLook(run.GlasswareTier, run.CounterTier, run.WallTier, run.HasMusician);
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
                : $"{hh:00}:{mm / 5 * 5:00}  ·  NIGHT {run.Day}";
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
            for (int i = 0; i < _ratingStars.Length; i++)
            {
                double fill = System.Math.Max(0.0, System.Math.Min(1.0, stars - i));
                _ratingStars[i].color = Color.Lerp(UITheme.Night[3], UITheme.Amber[3], (float)fill);
            }
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

                // A regular ordering again after a perfect serve gets a gold star and the
                // round count (GDD 24 §4) — the reward for reading them right, made visible.
                string star = visit.ExtraOrdersTaken > 0
                    ? $"<color=#F5C97B>★{visit.ExtraOrdersTaken + 1} </color>" : "";
                view.Name.supportRichText = true;
                view.Name.text = star + (visit.Regular?.Name ?? "Customer").ToUpperInvariant();
                view.Name.color = UITheme.TextPrimary;
                view.Order.color = UITheme.Amber[4];

                // The bubble only knows what the PLAYER knows (v5 C3): until the ID card has
                // been read, Core refuses to hand the order over at all, so an unread customer
                // shows a signal, not a drink. This is what makes reading the card a verb — the
                // bubble used to print the order and its price over every head, and the card was
                // decoration. No price appears even after reading: prices live on the menu.
                bool known = visit.IdInspected;
                if (view.Icon != null)
                {
                    view.Icon.sprite = deciding || !known
                        ? null
                        : DrinkIcon.For(visit.Order.Wanted, _bootstrap.Glassware);
                    view.Icon.enabled = view.Icon.sprite != null;
                    view.Icon.color = drinking ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
                }

                if (deciding)
                {
                    // Nothing to read or serve yet — they are still making up their mind.
                    view.Wants.text = "DECIDING...";
                    view.Order.text = "...";
                }
                else if (drinking)
                {
                    // Served and content; the drink is theirs to finish before they go.
                    view.Wants.text = "ENJOYING IT";
                    view.Order.text = known ? $"{visit.Order.Wanted.Name.ToUpperInvariant()}  ·" : "·";
                    view.Order.color = UITheme.Lime[3];
                }
                else if (!known)
                {
                    // They have ordered and you have not looked: the card is the only way in.
                    view.Wants.text = "READY · TAP THE ID";
                    view.Order.text = "?";
                }
                else
                {
                    // A glanceable tell that they want extras; the licence (GDD 24 §5) shows which.
                    view.Wants.text = visit.Order.Garnishes.Count > 0 ? "WANTS EXTRAS" : "WAITING";
                    view.Order.text = visit.Order.Wanted.Name.ToUpperInvariant();
                }

                // The icon docks against the text's measured width, so the centred line reads
                // as one piece: [glass] DRAUGHT, the pair centred together.
                if (view.Icon != null && view.Icon.enabled)
                    view.Icon.rectTransform.anchoredPosition =
                        new Vector2(-view.Order.preferredWidth * 0.5f - 4f, -42f);

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
                    : new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.86f);
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
            _dayEndPanel.gameObject.SetActive(true);
            RebuildDayEnd();
        }

        private void RebuildDayEnd()
        {
            var run = Run;
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
            sb.AppendLine($"   NIGHT {run.Day} · {CrowdName(run.CrowdToday)}");
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

            if (_shopTab == 0)
            {
                int restock = run.Shelf.RefillCost(cfg.RefillPricePerCapacity);
                AddCard("RESTOCK THE WELL", "well is full", restock, restock > 0, () =>
                {
                    run.RefillShelf(); RebuildDayEnd();
                });

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

                // The recipe book (v5 P16): the locked cocktails, bought onto the menu the way
                // stock is bought onto the shelf. The better ones want the room talking first
                // (the star gate) — a card that is gated says so instead of hiding.
                foreach (var recipe in run.LockedRecipes)
                {
                    var r = recipe;
                    double gate = run.RecipeStarGate(r);
                    bool gated = run.Rating.Average < gate;
                    string title = gated
                        ? $"✦ {r.Name.ToUpperInvariant()} · NEEDS {gate:0.0}★"
                        : $"✦ {r.Name.ToUpperInvariant()}";
                    AddCard(title, "gated", run.RecipePrice(r), !gated, () =>
                    {
                        try { run.UnlockRecipe(r.Id); Toast($"{r.Name.ToUpperInvariant()} — ON THE MENU TOMORROW"); }
                        catch (InvalidOperationException e) { Toast(e.Message.ToUpperInvariant()); }
                        RebuildDayEnd();
                    }, DrinkIcon.For(r, _bootstrap.Glassware));
                }
            }
            else
            {
                AddCard($"STOOL #{run.Seats + 1}", "bar is full", cfg.SeatPrice(run.Seats),
                    run.Seats < cfg.MaxSeats, () => { run.BuySeat(); ApplyBarLook(); RebuildDayEnd(); });

                AddCard($"GLASSWARE ★{run.GlasswareTier}", "top tier", cfg.GlasswarePrice(run.GlasswareTier),
                    run.GlasswareTier < cfg.MaxAmbienceTier,
                    () => { run.BuyGlassware(); ApplyBarLook(); RebuildDayEnd(); }, ItemArt.Glass);
                AddCard($"COUNTER ★{run.CounterTier}", "top tier", cfg.CounterPrice(run.CounterTier),
                    run.CounterTier < cfg.MaxAmbienceTier,
                    () => { run.BuyCounter(); ApplyBarLook(); RebuildDayEnd(); });
                AddCard($"BACK BAR ★{run.WallTier}", "top tier", cfg.WallPrice(run.WallTier),
                    run.WallTier < cfg.MaxAmbienceTier,
                    () => { run.BuyWall(); ApplyBarLook(); RebuildDayEnd(); });
                AddCard("MUSICIAN", "on stage", cfg.MusicianPrice, !run.HasMusician,
                    () => { run.BuyMusician(); ApplyBarLook(); RebuildDayEnd(); });
            }
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
            Place(_settingsPanel, new Vector2(1, 1), new Vector2(240, 128), new Vector2(-16, -48));
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
            _idOrderIcon.sprite = DrinkIcon.For(visit.Order.Wanted, _bootstrap.Glassware);
            _idOrderIcon.enabled = _idOrderIcon.sprite != null;

            // The endorsements line: everything the spec asks of the serve, in one place —
            // garnishes, worked hard, filled to the top. This is the read the tip grades.
            var spec = visit.Order.Spec;
            var wants = visit.Order.Garnishes.Select(g => g.Name.ToUpperInvariant()).ToList();
            if (spec.ExtraShaken) wants.Add("SHAKEN HARD");
            if (spec.FilledToTheTop) wants.Add("FILLED TO THE TOP");
            _idIntent.text = wants.Count == 0
                ? "SERVE IT CLEAN"
                : string.Join("   ·   ", wants);
        }

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
        private const float LicScale = 2.5f;
        private const float LicW = 266f * LicScale, LicH = 176f * LicScale;

        // Anchors measured off licence_shell.png at install, ×2.5: portrait frame x 15–89,
        // y 37–132; navy header rows 3–21; the six rule lines above.
        private static readonly Rect LicPortrait = new Rect(37.5f, -92.5f, 187.5f, 240f);
        private const float LicHeaderH = 45f;
        private const float LicHeaderY = -7.5f;
        private const float LicFieldsX = 250f;   // the rules span art x 100–250 → card 250–625
        private const float LicFieldsW = 375f;
        private static readonly float[] LicLines =   // card-local y (down from the top), ×2.5
            { 58f * LicScale, 76f * LicScale, 94f * LicScale, 113f * LicScale,
              132f * LicScale, 150f * LicScale };

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
            var val = NewText("V_" + label, card, _body, valueSize, TextAnchor.LowerLeft, UITheme.Night[1]);
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
            shell.sprite = ItemArt.Load("licence_shell");
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
            Place(idIcon, new Vector2(0, 1), new Vector2(44, 44), Vector2.zero);
            idIcon.pivot = new Vector2(0, 0);
            idIcon.anchoredPosition = new Vector2(LicFieldsX, -LicLines[3] + 2f);
            _idOrderIcon = idIcon.gameObject.AddComponent<Image>();
            _idOrderIcon.preserveAspect = true;
            _idOrderIcon.raycastTarget = false;
            _idOrder = LicenceField(card, "ORDER", LicFieldsX + 54f, LicLines[3],
                LicFieldsW - 54f, out _, 16);

            // Serving preferences — the endorsements line. What the licence permits.
            _idIntent = LicenceField(card, "SERVING PREFERENCES", LicFieldsX, LicLines[4],
                LicFieldsW, out _, 12);

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

            // Place() pivots on the anchor, so these offsets are RIGHT edges: the button's own
            // right edge sits at -68 and it is 110 wide, so the stars have to clear -178.
            const float StarSize = 16f, StarGap = 20f, StarsRight = -196f;
            for (int i = 0; i < _ratingStars.Length; i++)
            {
                var star = NewRect($"Star{i}", top);
                Place(star, new Vector2(1, 0.5f), new Vector2(StarSize, StarSize),
                    new Vector2(StarsRight - (_ratingStars.Length - 1 - i) * StarGap, 0));
                var img = star.gameObject.AddComponent<Image>();
                img.sprite = ItemArt.Load("star");
                img.preserveAspect = true;
                img.raycastTarget = false;
                _ratingStars[i] = img;
            }

            _ratingText = NewText("Rating", top, _display, 14, TextAnchor.MiddleRight, UITheme.Amber[3]);
            Place(_ratingText.rectTransform, new Vector2(1, 0.5f), new Vector2(60, 30),
                new Vector2(StarsRight - _ratingStars.Length * StarGap - 6f, 0));

            _crowdText = NewText("Crowd", top, _body, 13, TextAnchor.MiddleRight, UITheme.TextSecondary);
            Place(_crowdText.rectTransform, new Vector2(1, 0.5f), new Vector2(200, 30),
                new Vector2(StarsRight - _ratingStars.Length * StarGap - 74f, 0));

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
                seat.Tag.sizeDelta = new Vector2(BustW + 44f, 70f);
                seat.Tag.anchoredPosition = new Vector2(0, CharWinH + 10f);
                seat.TagBg = seat.Tag.gameObject.AddComponent<Image>();
                seat.TagBg.raycastTarget = false;

                seat.Name = NewText("Name", seat.Tag, _body, 12, TextAnchor.UpperCenter, UITheme.TextPrimary);
                Stretch(seat.Name.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, -4));
                seat.Name.horizontalOverflow = HorizontalWrapMode.Overflow;

                seat.Wants = NewText("Wants", seat.Tag, _body, 10, TextAnchor.UpperCenter, UITheme.Cyan[4]);
                Stretch(seat.Wants.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, -20));
                seat.Wants.horizontalOverflow = HorizontalWrapMode.Overflow;

                // Centred for real (2026-07-31): the row used to keep a 32px left inset so the
                // corner-pinned icon had room, which centred the text in a right-shifted box —
                // visibly off-centre on every seat. Now the TEXT owns the middle and the icon
                // rides just left of its measured width, per refresh, like a bullet point.
                seat.Order = NewText("Order", seat.Tag, _body, 11, TextAnchor.UpperCenter, UITheme.Amber[4]);
                Stretch(seat.Order.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, -36));
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

            BuildDrinkGlass(root);
            BuildSnackRow(root);
            BuildIdCard(root);

            // Day end: a plain invoice panel with the night's business under it.
            _dayEndPanel = NewRect("DayEnd", root);
            Place(_dayEndPanel, new Vector2(0.5f, 0.5f), new Vector2(940, 600), new Vector2(0, 10));
            var panelImg = _dayEndPanel.gameObject.AddComponent<Image>();
            panelImg.color = new Color(UITheme.Night[1].r, UITheme.Night[1].g, UITheme.Night[1].b, 0.97f);

            var title = NewText("Title", _dayEndPanel, _display, 16, TextAnchor.MiddleCenter, UITheme.PrimaryAction);
            Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -46), new Vector2(0, -8));
            title.text = "LAST CALL — THE BOOKS";

            // Left column: the till slip (v5 P13). Cream stock, and pinned to 16pt — a whole
            // multiple of the face's 8px design size, so the monospace columns the receipt is
            // set in actually land on the pixel grid instead of blurring between it.
            var bill = NewRect("Bill", _dayEndPanel);
            Place(bill, new Vector2(0, 1), new Vector2(360, 470), new Vector2(24, -56));
            bill.gameObject.AddComponent<Image>().color = UITheme.Cream[4];
            _invoiceText = NewText("Invoice", bill, _body, 16, TextAnchor.UpperLeft, UITheme.Night[1]);
            Stretch(_invoiceText.rectTransform, Vector2.zero, Vector2.one, new Vector2(14, 12), new Vector2(-14, -12));
            _invoiceText.supportRichText = true;

            // Right column: the shop, as the tablet the bar orders from (v5 P13). It was a flat
            // grid of thirteen identical cards — a bottle, a stool and a musician all reading the
            // same. The shell is the cheap half of the fix; the tabs are the real one, because
            // restocking the well and buying a musician are different errands and the player is
            // only ever doing one of them.
            var tablet = NewRect("Tablet", _dayEndPanel);
            Place(tablet, new Vector2(0, 1), new Vector2(524, 468), new Vector2(396, -56));
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
                Place(key, new Vector2(0, 1), new Vector2(120, 24), new Vector2(10 + i * 128, -28));
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

            _offerRow = NewRect("Offers", screen);
            Place(_offerRow, new Vector2(0, 1), new Vector2(476, 372), new Vector2(10, -58));
            var grid = _offerRow.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(153, 70);
            grid.spacing = new Vector2(8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;

            _openTomorrow = NewRect("OpenTomorrow", _dayEndPanel);
            Place(_openTomorrow, new Vector2(0.5f, 0), new Vector2(892, 40), new Vector2(0, 16));
            _openTomorrow.gameObject.AddComponent<Image>().color = UITheme.PrimaryAction;
            var otBtn = _openTomorrow.gameObject.AddComponent<Button>();
            otBtn.onClick.AddListener(OnOpenTomorrow);
            var otLabel = NewText("Label", _openTomorrow, _display, 14, TextAnchor.MiddleCenter, UITheme.TextOnAmber);
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
        private void AddCard(string title, string sub, int price, bool available, Action onBuy,
            Sprite art = null)
        {
            var rt = NewRect("Card", _offerRow);
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
