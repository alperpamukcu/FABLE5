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

namespace LastCall.DebugUI
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
        private Image _satisfactionFill;

        // Seats at the counter (GDD 24 §4, 2026-07-22): customers sit along the bar as
        // head-and-shoulders busts, not in a bottom strip. Each bust rises/slides into its
        // stool when its patron arrives, wears a floating order tag, and is the click target.
        private const int SeatSlots = 6;
        private const float CounterLineY = 262f;   // HUD-space y (from bottom) of the bar top
        private const float BustW = 108f;
        private const float BustH = 128f;
        private const float WalkDuration = 0.6f;   // seconds to walk in and sit
        private sealed class SeatView
        {
            public RectTransform Root;       // the bust, positioned at the counter (click target)
            public CanvasGroup Group;        // fades the bust in as it walks up
            public Image Portrait;           // the customer's face/bust
            public RectTransform Tag;        // the floating order ticket above the head
            public Image TagBg;
            public Text Name;
            public Text Wants;
            public Text Order;
            public Image PatienceFill;
            public float SeatX;              // this stool's resting x
            public float WalkT;              // 0..1 walk-in progress
            public bool Exiting;             // playing the leave animation
            public float ExitT;              // 0..1 leave progress
            public bool ExitStorm;           // stormed off (angry exit) vs served (calm)
            public CustomerVisit Visit;      // who is assigned to this stool (stable until they leave)
        }
        private readonly List<SeatView> _seats = new List<SeatView>();
        private const float ExitDuration = 0.55f;

        // The finished drink on the counter (GDD 24 §3, 2026-07-22): a glass you drag onto a
        // customer to serve, carried with a heavy, springy AAA feel.
        private RectTransform _drinkGlass;
        private Image _drinkGlassLiquid;
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

        // ID card v2 (P6, GDD 24 §5): the licence you read a customer by.
        private RectTransform _idRoot;
        private Image _idPhoto;
        private Text _idName, _idAgeFrom, _idRel, _idIntent, _idOrder, _idGreeting;
        private sealed class StatRow { public Text Tag; public Text Value; public Image Band; public RectTransform Track; }
        private readonly Dictionary<Emotion, StatRow> _idRows = new Dictionary<Emotion, StatRow>();
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

            RefreshTopBar();
            RefreshSeats();
            UpdateDrinkGlass();
        }

        // ── the floor ───────────────────────────────────────────────────────────

        private void OnMenuClicked() => _flow?.Open();

        private void OnSeatClicked(int index)
        {
            var run = Run;
            if (run == null || run.Phase != TycoonPhase.DayOpen) return;
            if (_flow != null && _flow.IsOpen) return;   // finish the build first
            var visit = _seats[index].Visit;
            if (visit == null || visit.State != VisitState.Waiting) return;

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
            if (run.ServingGlass.IsEmpty && run.Glass.IsEmpty) return false;

            var verdict = run.ServeTo(visit);
            CloseId();
            StartCoroutine(ServeReaction(index, verdict));   // reaction + payment float up
            return true;
        }

        // ── the drink you carry (GDD 24 §3, 2026-07-22) ──────────────────────────

        private void BuildDrinkGlass(RectTransform root)
        {
            _glassHome = new Vector2(0, -200f);   // staged on the counter, above the MENU button
            _drinkGlass = NewRect("DrinkGlass", root);
            _drinkGlass.anchorMin = _drinkGlass.anchorMax = _drinkGlass.pivot = new Vector2(0.5f, 0.5f);
            _drinkGlass.sizeDelta = new Vector2(78, 100);
            _drinkGlass.anchoredPosition = _glassHome;

            var body = _drinkGlass.gameObject.AddComponent<Image>();   // the glass, and the grab target
            body.color = new Color(0.85f, 0.93f, 1f, 0.20f);
            var grab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            grab.callback.AddListener(ev => OnGlassGrab((PointerEventData)ev));
            _drinkGlass.gameObject.AddComponent<EventTrigger>().triggers.Add(grab);

            var bowl = NewRect("Bowl", _drinkGlass);
            Stretch(bowl, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -12));
            var bowlImg = bowl.gameObject.AddComponent<Image>();
            bowlImg.color = new Color(0f, 0f, 0f, 0.28f); bowlImg.raycastTarget = false;

            var liquid = NewRect("Liquid", bowl);   // a bottom-anchored fill, ~two-thirds full
            liquid.anchorMin = new Vector2(0, 0); liquid.anchorMax = new Vector2(1, 0);
            liquid.pivot = new Vector2(0.5f, 0);
            liquid.offsetMin = new Vector2(2, 2); liquid.offsetMax = new Vector2(-2, 0);
            liquid.sizeDelta = new Vector2(-4, 62);
            _drinkGlassLiquid = liquid.gameObject.AddComponent<Image>();
            _drinkGlassLiquid.raycastTarget = false;

            var rim = NewRect("Rim", _drinkGlass);
            Place(rim, new Vector2(0.5f, 1), new Vector2(84, 8), new Vector2(0, 0));
            var rimImg = rim.gameObject.AddComponent<Image>();
            rimImg.color = UITheme.Cyan[3]; rimImg.raycastTarget = false;

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
            bool ready = run != null && run.Phase == TycoonPhase.DayOpen
                && (_flow == null || !_flow.IsOpen)
                && (!run.ServingGlass.IsEmpty || !run.Glass.IsEmpty);

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
            _drinkGlassLiquid.color = DrinkColor();

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var mouse = Mouse.current;

            if (_glassGrabbed && (mouse == null || !mouse.leftButton.isPressed))
            {
                _glassGrabbed = false;
                int seat = SeatUnderCursor(mouse);
                if (seat >= 0 && ServeSeat(seat))
                {
                    _drinkGlass.gameObject.SetActive(false);   // handed over; a new drink re-shows it
                    _glassShown = false;
                    return;
                }
            }

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

        /// <summary>The carried drink's colour: its ingredients' style colours, blended by share.</summary>
        private Color DrinkColor()
        {
            var run = Run;
            var glass = run == null ? null
                : (!run.ServingGlass.IsEmpty ? run.ServingGlass : run.Glass);
            if (glass == null || glass.IsEmpty) return UITheme.Amber[3];
            float r = 0, g = 0, b = 0;
            foreach (var id in glass.Ingredients)
            {
                var card = run.Shelf.Find(id)?.Ingredient;
                var c = card != null ? UITheme.StyleColor(card.Info?.Style, card.Type) : UITheme.Amber[3];
                float w = (float)glass.RatioOf(id);
                r += c.r * w; g += c.g * w; b += c.b * w;
            }
            return new Color(r, g, b, 0.92f);
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

            string line = verdict.OrdersAgain ? "★ ANOTHER ROUND!"
                : verdict.Match == OrderMatch.Exact ? "PERFECT!"
                : verdict.Match == OrderMatch.Close ? "THANKS."
                : "NOT WHAT I ASKED";
            string tip = verdict.MoodTipLanded && verdict.Tip > 0 ? $"+${verdict.Total} ♪" : $"+${verdict.Total}";

            var text = NewText("React", seat.parent, _display, 15, TextAnchor.LowerCenter, tone);
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = $"{line}\n<color=#{ColorUtility.ToHtmlStringRGB(UITheme.Lime[3])}>{tip}</color>";
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(178, 44);
            var start = seat.anchoredPosition + new Vector2(-89f, 120f);   // centred over the seat

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

        /// <summary>A short notice under the top bar — refusals, mostly (GDD 24 §7).</summary>
        private void Toast(string message)
        {
            if (_toast == null) return;
            _toast.text = message;
            _toastUntil = Time.unscaledTime + 1.6f;
            _toast.gameObject.SetActive(true);
        }

        private void OnBinClicked()
        {
            var run = Run;
            if (run == null || run.Phase != TycoonPhase.DayOpen) return;
            run.DiscardGlass();
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
            _dayText.text = $"DAY {run.Day}  ·  {run.Floor.Arrived}/{run.Floor.CustomersPlanned} IN";
            _moneyText.text = $"${run.Money}";
            _moneyText.color = run.Money < 0 ? UITheme.ViceRed[3] : UITheme.Money;
            if (stage != null) stage.SetMoney($"${run.Money}");
            _crowdText.text = run.CrowdToday == WealthTier.HighRoller ? "HIGH ROLLERS"
                : run.CrowdToday == WealthTier.Broke ? "BROKE CROWD" : "REGULARS";

            float t = (float)run.Floor.AverageSatisfaction;
            _satisfactionFill.rectTransform.sizeDelta = new Vector2(Mathf.Round(236f * t), -4);
            _satisfactionFill.color = Color.Lerp(UITheme.ViceRed[3], UITheme.Lime[3], t);
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

            bool drinkReady = run.Phase == TycoonPhase.DayOpen &&
                (!run.ServingGlass.IsEmpty || !run.Glass.IsEmpty) &&
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
                        break;
                    }
                }
            }

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
                // A regular ordering again after a perfect serve gets a gold star and the
                // round count (GDD 24 §4) — the reward for reading them right, made visible.
                string star = visit.ExtraOrdersTaken > 0
                    ? $"<color=#F5C97B>★{visit.ExtraOrdersTaken + 1} </color>" : "";
                view.Name.supportRichText = true;
                view.Name.text = star + (visit.Regular?.Name ?? "Customer").ToUpperInvariant();
                view.Name.color = UITheme.TextPrimary;
                view.Order.color = UITheme.Amber[4];

                // The face at the bar. A real portrait when we have one, a neutral bust when we
                // don't; it sours (reddens) over the last of their patience — anger you can see.
                var sprite = stage != null && visit.Regular != null
                    ? stage.PortraitSpriteFor(visit.Regular.ArchetypeId) : null;
                view.Portrait.sprite = sprite;

                // The always-visible half of the read (GDD 19 §3); tap the customer for the full
                // licence (GDD 24 §5). "TAP TO READ" nudges the newcomer.
                view.Wants.text = visit.Read == null ? "TAP TO READ" :
                    (visit.Read.Direction == IntentDirection.Extinguish ? "SETTLE " : "LIFT ")
                    + visit.Read.Intent.ToString().ToUpperInvariant();

                view.Order.text = $"{visit.Order.Wanted.Name.ToUpperInvariant()}  ${visit.Order.Price}";

                float patience = (float)(visit.PatienceLeft / visit.PatienceMax);
                float tagW = view.Tag.rect.width - 12f;
                view.PatienceFill.rectTransform.sizeDelta = new Vector2(Mathf.Round(tagW * patience), -2);
                view.PatienceFill.color = patience > 0.5f ? UITheme.Lime[3]
                    : patience > 0.25f ? UITheme.Amber[3] : UITheme.ViceRed[3];

                // Sour the face over the last third of their patience.
                float mood = Mathf.Clamp01(patience / 0.35f);
                var calm = sprite != null ? Color.white : new Color(0.62f, 0.55f, 0.60f, 1f);
                view.Portrait.color = Color.Lerp(new Color(0.80f, 0.28f, 0.30f, 1f), calm, mood);

                // The tag glows cyan when a drink is built and waiting: "hand it over here".
                view.TagBg.color = drinkReady
                    ? new Color(UITheme.Selection.r, UITheme.Selection.g, UITheme.Selection.b, 0.92f)
                    : new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.86f);
            }
        }

        /// <summary>Slides a newly-seated bust in from the left and fades it up (GDD 24 §4).</summary>
        private void AdvanceWalkIn(SeatView view)
        {
            if (view.WalkT < 1f)
            {
                view.WalkT = Mathf.Min(1f, view.WalkT + Time.deltaTime / WalkDuration);
                float e = 1f - (1f - view.WalkT) * (1f - view.WalkT);   // ease-out
                float startX = view.SeatX - 200f;
                view.Root.anchoredPosition = new Vector2(Mathf.Lerp(startX, view.SeatX, e), CounterLineY);
                view.Group.alpha = view.WalkT;
            }
            else
            {
                view.Root.anchoredPosition = new Vector2(view.SeatX, CounterLineY);
                view.Group.alpha = 1f;
            }
        }

        /// <summary>Plays a customer leaving their stool (GDD 24 §4): a served patron sinks off
        /// the stool and fades; a stormed-off one shakes, then slides out fast.</summary>
        private void AdvanceExit(SeatView view)
        {
            view.ExitT += Time.deltaTime / ExitDuration;
            float k = Mathf.Clamp01(view.ExitT);

            if (view.ExitStorm)
            {
                float shake = k < 0.35f ? Mathf.Sin(k * 70f) * 9f : 0f;
                float slide = Mathf.Clamp01((k - 0.35f) / 0.65f);
                view.Root.anchoredPosition = new Vector2(
                    view.SeatX + shake - 320f * slide * slide, CounterLineY - 18f * slide);
                view.Group.alpha = 1f - slide;
            }
            else
            {
                float e = k * k;   // ease-in: gets up and steps away
                view.Root.anchoredPosition = new Vector2(view.SeatX, CounterLineY - 96f * e);
                view.Group.alpha = 1f - e;
            }

            if (view.ExitT >= 1f)
            {
                view.Exiting = false;
                view.Visit = null;
                view.Group.alpha = 1f;
                view.Root.gameObject.SetActive(false);
            }
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

            // Readability rules (GDD 24 §7): short labels, big lines, no prose.
            var sb = new StringBuilder();
            sb.AppendLine($"<b>DAY {run.Day}</b>   {served} served · {stormed} left");
            sb.AppendLine($"mood {floor.AverageSatisfaction:P0} · {CrowdName(run.CrowdToday)}");
            sb.AppendLine("<color=#9C8F80>──────────────</color>");
            sb.AppendLine($"SALES{Dots(9)}${run.DaySales}");
            sb.AppendLine($"TIPS{Dots(10)}${run.DayTips}");
            sb.AppendLine($"<color=#A62B44>RENT{Dots(10)}-${run.DayRent}</color>");
            sb.AppendLine($"<color=#A62B44>STOCK{Dots(9)}-${run.DayStock}</color>");
            sb.AppendLine($"<color=#A62B44>SHOP{Dots(10)}-${run.DayUpgrades}</color>");
            sb.AppendLine("<color=#9C8F80>──────────────</color>");
            sb.AppendLine($"<b><color=#{netColour}>NET{Dots(9)}{(net >= 0 ? "+" : "-")}${Math.Abs(net)}</color></b>");
            sb.AppendLine($"<b>TILL{Dots(8)}${run.Money}</b>");
            sb.Append(stamp);
            _invoiceText.text = sb.ToString();

            // The cards.
            foreach (Transform child in _offerRow) Destroy(child.gameObject);

            int restock = run.Shelf.RefillCost(cfg.RefillPricePerCapacity);
            AddCard("RESTOCK THE WELL", "well is full", restock, restock > 0, () =>
            {
                run.RefillShelf(); RebuildDayEnd();
            });

            for (int i = 0; i < run.MarketOffers.Count; i++)
            {
                int index = i;
                var offer = run.MarketOffers[i];
                AddCard(offer.Bottle.Name.ToUpperInvariant(), "bought", offer.Price, !offer.Sold, () =>
                {
                    run.BuyBrand(index);
                    RebuildDayEnd();
                });
            }

            AddCard($"STOOL #{run.Seats + 1}", "bar is full", cfg.SeatPrice(run.Seats),
                run.Seats < cfg.MaxSeats, () => { run.BuySeat(); ApplyBarLook(); RebuildDayEnd(); });

            AddCard($"GLASSWARE ★{run.GlasswareTier}", "top tier", cfg.GlasswarePrice(run.GlasswareTier),
                run.GlasswareTier < cfg.MaxAmbienceTier,
                () => { run.BuyGlassware(); ApplyBarLook(); RebuildDayEnd(); });
            AddCard($"COUNTER ★{run.CounterTier}", "top tier", cfg.CounterPrice(run.CounterTier),
                run.CounterTier < cfg.MaxAmbienceTier,
                () => { run.BuyCounter(); ApplyBarLook(); RebuildDayEnd(); });
            AddCard($"BACK BAR ★{run.WallTier}", "top tier", cfg.WallPrice(run.WallTier),
                run.WallTier < cfg.MaxAmbienceTier,
                () => { run.BuyWall(); ApplyBarLook(); RebuildDayEnd(); });
            AddCard("MUSICIAN", "on stage", cfg.MusicianPrice, !run.HasMusician,
                () => { run.BuyMusician(); ApplyBarLook(); RebuildDayEnd(); });
        }

        private static string CrowdName(WealthTier tier) =>
            tier == WealthTier.HighRoller ? "HIGH ROLLERS" : tier == WealthTier.Broke ? "BROKE" : "REGULARS";

        /// <summary>Leader dots so the bill columns line up in the monospace pixel font.</summary>
        private static string Dots(int n) => "<color=#9C8F80>" + new string('.', n) + "</color>";

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
            if (visit?.Read == null || visit.Regular == null) return;
            _idVisit = visit;
            var reg = visit.Regular;
            var read = visit.Read;

            if (_ledgerPanel != null) _ledgerPanel.gameObject.SetActive(false);
            _idRoot.gameObject.SetActive(true);
            _idPhoto.sprite = stage != null ? stage.PortraitSpriteFor(reg.ArchetypeId) : null;
            _idPhoto.color = _idPhoto.sprite != null ? Color.white : UITheme.Night[3];

            string demandHex = read.Demand == DemandLevel.Demanding ? "A62B44"
                : read.Demand == DemandLevel.Particular ? "8F5A1E" : "2A5926";
            _idName.text = reg.Name.ToUpperInvariant();
            _idAgeFrom.text = $"AGE {reg.Age}\nFROM {reg.Hometown.ToUpperInvariant()}";
            _idRel.text = (reg.Visits > 0
                ? $"{reg.Relationship.ToString().ToUpperInvariant()} · {reg.Visits} VISITS"
                : "NEW FACE")
                + $"  ·  <color=#{demandHex}>{Demands.Label(read.Demand).ToUpperInvariant()}</color>";

            _idIntent.text =
                (read.Direction == IntentDirection.Extinguish ? "WANTS: SETTLE " : "WANTS: LIFT ")
                + $"<b>{read.Intent.ToString().ToUpperInvariant()}</b>"
                + $"    ·    GLASS {read.FillPreference.ShortLabel.ToUpperInvariant()}";

            _idOrder.text = $"ORDER:  <b>{visit.Order.Wanted.Name.ToUpperInvariant()}</b>   ${visit.Order.Price}";
            _idGreeting.text = reg.Visits > 0
                ? "« a familiar face — you know some of this already »"
                : "« a stranger — read them as best you can »";

            foreach (var emotion in Emotions.All)
            {
                var row = _idRows[emotion];
                var reading = read[emotion];
                var ramp = UITheme.EmotionRamp[emotion];
                var band = row.Track.gameObject.transform.GetChild(0) as RectTransform;

                bool star = emotion == read.Intent;
                row.Tag.text = (star ? "★ " : "") + emotion.ToString().ToUpperInvariant();
                row.Tag.color = star ? ramp[4] : ramp[3];

                switch (reading.Tier)
                {
                    case VisibilityTier.Exact:
                        row.Value.text = reading.Low.ToString();
                        row.Value.color = ramp[4];
                        band.gameObject.SetActive(true);
                        band.anchoredPosition = new Vector2(TrackAt(reading.Low), 0);
                        band.sizeDelta = new Vector2(6, 0);
                        row.Band.color = ramp[4];
                        break;
                    case VisibilityTier.Range:
                        row.Value.text = $"{reading.Low}–{reading.High}";
                        row.Value.color = ramp[3];
                        band.gameObject.SetActive(true);
                        band.anchoredPosition = new Vector2(TrackAt(reading.Low), 0);
                        band.sizeDelta = new Vector2(Mathf.Max(6f, TrackAt(reading.High) - TrackAt(reading.Low)), 0);
                        row.Band.color = new Color(ramp[3].r, ramp[3].g, ramp[3].b, 0.65f);
                        break;
                    default:
                        row.Value.text = "??";
                        row.Value.color = UITheme.Cream[1];
                        band.gameObject.SetActive(false);
                        break;
                }
            }
        }

        private void CloseId()
        {
            _idVisit = null;
            if (_idRoot != null) _idRoot.gameObject.SetActive(false);
        }

        private static float TrackAt(int value) => IdTrackW * Mathf.Clamp01(value / 100f);

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
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(452, 588), new Vector2(0, 6));
            card.gameObject.AddComponent<Image>().color = UITheme.Cream[4];
            card.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // swallow clicks

            var header = NewRect("Header", card);
            Stretch(header, new Vector2(0, 1), Vector2.one, new Vector2(0, -30), Vector2.zero);
            header.gameObject.AddComponent<Image>().color = UITheme.ClubBlue[2];
            var htext = NewText("H", header, _body, 13, TextAnchor.MiddleCenter, UITheme.Cream[4]);
            Stretch(htext.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            htext.text = "CITY OF NEW ARDEN — PATRON ID";

            var photoFrame = NewRect("PhotoFrame", card);
            Place(photoFrame, new Vector2(0, 1), new Vector2(112, 138), new Vector2(16, -40));
            photoFrame.gameObject.AddComponent<Image>().color = UITheme.Night[1];
            var photo = NewRect("Photo", photoFrame);
            Stretch(photo, Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3));
            _idPhoto = photo.gameObject.AddComponent<Image>();
            _idPhoto.preserveAspect = true;

            _idName = NewText("Name", card, _display, 18, TextAnchor.UpperLeft, UITheme.Night[1]);
            Place(_idName.rectTransform, new Vector2(0, 1), new Vector2(300, 26), new Vector2(140, -42));
            _idAgeFrom = NewText("AgeFrom", card, _body, 13, TextAnchor.UpperLeft, UITheme.Night[2]);
            Place(_idAgeFrom.rectTransform, new Vector2(0, 1), new Vector2(300, 40), new Vector2(140, -72));
            _idRel = NewText("Rel", card, _body, 12, TextAnchor.UpperLeft, UITheme.Night[3]);
            _idRel.supportRichText = true;
            Place(_idRel.rectTransform, new Vector2(0, 1), new Vector2(300, 18), new Vector2(140, -118));

            _idOrder = NewText("Order", card, _body, 13, TextAnchor.UpperLeft, UITheme.Night[1]);
            _idOrder.supportRichText = true;
            Place(_idOrder.rectTransform, new Vector2(0, 1), new Vector2(300, 22), new Vector2(140, -142));

            // The one thing never hidden, in an amber endorsement band.
            var intentBand = NewRect("IntentBand", card);
            Place(intentBand, new Vector2(0.5f, 1), new Vector2(420, 28), new Vector2(0, -190));
            intentBand.gameObject.AddComponent<Image>().color = UITheme.Amber[3];
            _idIntent = NewText("Intent", intentBand, _body, 12, TextAnchor.MiddleCenter, UITheme.Night[1]);
            _idIntent.supportRichText = true;
            Stretch(_idIntent.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-8, 0));

            // The six readings, one big row each (GDD 24 §5 readability pass).
            float top = -232;
            for (int i = 0; i < Emotions.Count; i++)
            {
                var emotion = Emotions.All[i];
                var ramp = UITheme.EmotionRamp[emotion];
                var rowRect = NewRect($"Row{emotion}", card);
                Place(rowRect, new Vector2(0.5f, 1), new Vector2(420, 44), new Vector2(0, top - i * 46));

                var stat = new StatRow();
                stat.Tag = NewText("Tag", rowRect, _body, 13, TextAnchor.MiddleLeft, ramp[3]);
                Place(stat.Tag.rectTransform, new Vector2(0, 0.5f), new Vector2(120, 24), new Vector2(6, 0));

                stat.Track = NewRect("Track", rowRect);
                Place(stat.Track, new Vector2(0, 0.5f), new Vector2(IdTrackW, 14), new Vector2(130, 0));
                stat.Track.gameObject.AddComponent<Image>().color = UITheme.Cream[2];
                var band = NewRect("Band", stat.Track);
                band.anchorMin = new Vector2(0, 0); band.anchorMax = new Vector2(0, 1);
                band.pivot = new Vector2(0, 0.5f);
                band.sizeDelta = new Vector2(6, 0);
                stat.Band = band.gameObject.AddComponent<Image>();
                stat.Band.color = ramp[4];

                stat.Value = NewText("Val", rowRect, _display, 13, TextAnchor.MiddleRight, ramp[4]);
                Place(stat.Value.rectTransform, new Vector2(1, 0.5f), new Vector2(96, 24), new Vector2(-6, 0));

                _idRows[emotion] = stat;
            }

            _idGreeting = NewText("Greeting", card, _body, 12, TextAnchor.MiddleCenter, UITheme.Night[2]);
            Place(_idGreeting.rectTransform, new Vector2(0.5f, 0), new Vector2(420, 20), new Vector2(0, 58));

            var close = NewRect("Close", card);
            Place(close, new Vector2(0.5f, 0), new Vector2(200, 34), new Vector2(0, 16));
            close.gameObject.AddComponent<Image>().color = UITheme.ClubBlue[2];
            close.gameObject.AddComponent<Button>().onClick.AddListener(CloseId);
            var closeLabel = NewText("L", close, _body, 13, TextAnchor.MiddleCenter, UITheme.Cream[4]);
            Stretch(closeLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            closeLabel.text = "CLOSE — BACK TO THE BAR";

            _idRoot.gameObject.SetActive(false);
        }

        // ── construction ────────────────────────────────────────────────────────

        private void BuildUi()
        {
            var canvasGo = new GameObject("TycoonHud", typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            var root = (RectTransform)canvasGo.transform;

            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var es = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }

            // Top bar: day, the till, the crowd, the live satisfaction bar, actions.
            var top = Panel(root, "TopBar", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -40), Vector2.zero, new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.82f));

            _dayText = NewText("Day", top, _display, 14, TextAnchor.MiddleLeft, UITheme.TextPrimary);
            Stretch(_dayText.rectTransform, Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-800, 0));

            _moneyText = NewText("Money", top, _display, 16, TextAnchor.MiddleLeft, UITheme.Money);
            Place(_moneyText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(140, 30), new Vector2(-240, 0));

            _crowdText = NewText("Crowd", top, _body, 13, TextAnchor.MiddleLeft, UITheme.TextSecondary);
            Place(_crowdText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(180, 30), new Vector2(-90, 0));

            var satLabel = NewText("SatLabel", top, _body, 12, TextAnchor.MiddleRight, UITheme.TextSecondary);
            Place(satLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(80, 30), new Vector2(120, 0));
            satLabel.text = "TONIGHT";
            var satBg = NewRect("SatBg", top);
            Place(satBg, new Vector2(0.5f, 0.5f), new Vector2(240, 14), new Vector2(290, 0));
            satBg.gameObject.AddComponent<Image>().color = UITheme.Night[1];
            var satFill = NewRect("SatFill", satBg);
            satFill.anchorMin = new Vector2(0, 0); satFill.anchorMax = new Vector2(0, 1);
            satFill.pivot = new Vector2(0, 0.5f);
            satFill.offsetMin = new Vector2(2, 2); satFill.offsetMax = new Vector2(2, -2);
            satFill.anchoredPosition = new Vector2(2, 0);
            _satisfactionFill = satFill.gameObject.AddComponent<Image>();
            _satisfactionFill.raycastTarget = false;

            NewButton(top, "BIN GLASS", new Vector2(1, 0.5f), new Vector2(110, 30),
                new Vector2(-190, 0), UITheme.Night[3], OnBinClicked);
            NewButton(top, "NEW RUN", new Vector2(1, 0.5f), new Vector2(110, 30),
                new Vector2(-70, 0), UITheme.PrimaryAction, () => _bootstrap.StartNewRun(null));

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
                seat.Root.sizeDelta = new Vector2(BustW + 52f, BustH + 92f);
                seat.Root.anchoredPosition = new Vector2(seat.SeatX, CounterLineY);
                var hit = seat.Root.gameObject.AddComponent<Image>();
                hit.color = new Color(0, 0, 0, 0);   // invisible, but catches clicks
                var button = seat.Root.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => OnSeatClicked(index));
                seat.Group = seat.Root.gameObject.AddComponent<CanvasGroup>();

                // The bust: head-and-shoulders sitting on the counter line (bottom-centred).
                var portrait = NewRect("Portrait", seat.Root);
                portrait.anchorMin = portrait.anchorMax = new Vector2(0.5f, 0);
                portrait.pivot = new Vector2(0.5f, 0);
                portrait.sizeDelta = new Vector2(BustW, BustW);   // square portrait art
                portrait.anchoredPosition = new Vector2(0, 0);
                seat.Portrait = portrait.gameObject.AddComponent<Image>();
                seat.Portrait.preserveAspect = true;
                seat.Portrait.raycastTarget = false;

                // The order tag, floating above the head.
                seat.Tag = NewRect("Tag", seat.Root);
                seat.Tag.anchorMin = seat.Tag.anchorMax = new Vector2(0.5f, 0);
                seat.Tag.pivot = new Vector2(0.5f, 0);
                seat.Tag.sizeDelta = new Vector2(BustW + 44f, 70f);
                seat.Tag.anchoredPosition = new Vector2(0, BustW + 8f);
                seat.TagBg = seat.Tag.gameObject.AddComponent<Image>();
                seat.TagBg.raycastTarget = false;

                seat.Name = NewText("Name", seat.Tag, _body, 12, TextAnchor.UpperCenter, UITheme.TextPrimary);
                Stretch(seat.Name.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, -4));
                seat.Name.horizontalOverflow = HorizontalWrapMode.Overflow;

                seat.Wants = NewText("Wants", seat.Tag, _body, 10, TextAnchor.UpperCenter, UITheme.Cyan[4]);
                Stretch(seat.Wants.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, -20));
                seat.Wants.horizontalOverflow = HorizontalWrapMode.Overflow;

                seat.Order = NewText("Order", seat.Tag, _body, 11, TextAnchor.UpperCenter, UITheme.Amber[4]);
                Stretch(seat.Order.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, -36));
                seat.Order.horizontalOverflow = HorizontalWrapMode.Overflow;

                var clockBg = NewRect("ClockBg", seat.Tag);
                clockBg.anchorMin = new Vector2(0, 0); clockBg.anchorMax = new Vector2(1, 0);
                clockBg.offsetMin = new Vector2(6, 6); clockBg.offsetMax = new Vector2(-6, 14);
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
            BuildIdCard(root);

            // Day end: a plain invoice panel with the night's business under it.
            _dayEndPanel = NewRect("DayEnd", root);
            Place(_dayEndPanel, new Vector2(0.5f, 0.5f), new Vector2(940, 600), new Vector2(0, 10));
            var panelImg = _dayEndPanel.gameObject.AddComponent<Image>();
            panelImg.color = new Color(UITheme.Night[1].r, UITheme.Night[1].g, UITheme.Night[1].b, 0.97f);

            var title = NewText("Title", _dayEndPanel, _display, 16, TextAnchor.MiddleCenter, UITheme.PrimaryAction);
            Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -46), new Vector2(0, -8));
            title.text = "LAST CALL — THE BOOKS";

            // Left column: the itemised bill on cream card stock, like a printed receipt.
            var bill = NewRect("Bill", _dayEndPanel);
            Place(bill, new Vector2(0, 1), new Vector2(360, 470), new Vector2(24, -56));
            bill.gameObject.AddComponent<Image>().color = UITheme.Cream[4];
            _invoiceText = NewText("Invoice", bill, _body, 18, TextAnchor.UpperLeft, UITheme.Night[1]);
            Stretch(_invoiceText.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 12), new Vector2(-16, -12));
            _invoiceText.supportRichText = true;

            // Right column: the market and upgrades as a card grid (GDD 24 §7).
            var marketLabel = NewText("MarketLabel", _dayEndPanel, _body, 13, TextAnchor.UpperLeft, UITheme.TextSecondary);
            Place(marketLabel.rectTransform, new Vector2(0, 1), new Vector2(500, 20), new Vector2(408, -58));
            marketLabel.text = "THE MARKET — spend to earn";
            _offerRow = NewRect("Offers", _dayEndPanel);
            Place(_offerRow, new Vector2(0, 1), new Vector2(504, 400), new Vector2(404, -84));
            var grid = _offerRow.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(162, 62);
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

        /// <summary>One market card: title, price, and a bought/maxed/can't-afford state.
        /// Nothing sells on credit (GDD 23 §6): an unaffordable card refuses with a notice.</summary>
        private void AddCard(string title, string sub, int price, bool available, Action onBuy)
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
            var name = NewText("Name", rt, _body, 12, TextAnchor.UpperLeft,
                available ? (afford ? UITheme.TextPrimary : UITheme.Cream[1]) : UITheme.Cream[1]);
            Place(name.rectTransform, new Vector2(0, 1), new Vector2(150, 32), new Vector2(8, -6));
            name.text = title;
            var priceText = NewText("Price", rt, _body, 12, TextAnchor.LowerLeft,
                !available ? UITheme.Cream[1] : afford ? UITheme.Money : UITheme.ViceRed[3]);
            Place(priceText.rectTransform, new Vector2(0, 0), new Vector2(150, 18), new Vector2(8, 6));
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
            var text = NewText("Label", rt, _body, 12, TextAnchor.MiddleCenter,
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
