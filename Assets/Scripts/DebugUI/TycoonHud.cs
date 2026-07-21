using System;
using System.Collections.Generic;
using System.Text;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;
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

        // seat row
        private const int SeatSlots = 6;
        private sealed class SeatView
        {
            public RectTransform Root;
            public Image Frame;
            public Text Name;
            public Text Wants;
            public Text Order;
            public Image PatienceFill;
            public CustomerVisit Visit;
        }
        private readonly List<SeatView> _seats = new List<SeatView>();

        // day end
        private RectTransform _dayEndPanel;
        private Text _invoiceText;
        private RectTransform _offerRow;
        private Text _bannerText;

        private TycoonServiceFlow _flow;
        private TycoonPhase _lastPhase = TycoonPhase.DayOpen;
        private double _lastShakerVol = -1;

        private void Awake()
        {
            var legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _body = bodyFont != null ? bodyFont : legacy;
            _display = displayFont != null ? displayFont : legacy;

            _bootstrap = GetComponent<GameBootstrap>();
            if (_bootstrap != null) _bootstrap.RunStarted += OnRunStarted;
            _flow = GetComponent<TycoonServiceFlow>();

            BuildUi();
        }

        private void OnDestroy()
        {
            if (_bootstrap != null) _bootstrap.RunStarted -= OnRunStarted;
        }

        private void OnRunStarted()
        {
            _lastPhase = TycoonPhase.DayOpen;
            _lastShakerVol = -1;
            _dayEndPanel.gameObject.SetActive(false);
            _bannerText.gameObject.SetActive(false);
            _flow?.CloseFlow();
            if (stage != null) stage.SetSoloCustomerVisible(false);
            RefreshShelf(DiegeticStage.Exit.Refresh);
            RefreshGlass();
        }

        private void Update()
        {
            var run = Run;
            if (run == null) return;

            if (run.Phase == TycoonPhase.DayOpen) run.Tick(Time.deltaTime);

            // Mirror the shaker onto the stage's top-left glass, but only when it actually
            // changed — SetGlass rebuilds its layers, so a per-frame call would churn.
            if (run.Glass.TotalVolume != _lastShakerVol)
            {
                _lastShakerVol = run.Glass.TotalVolume;
                RefreshGlass();
            }

            if (run.Phase != _lastPhase)
            {
                _lastPhase = run.Phase;
                if (run.Phase == TycoonPhase.DayEnd) ShowDayEnd();
                if (run.Phase == TycoonPhase.Closed) ShowClosed();
            }

            RefreshTopBar();
            RefreshSeats();
        }

        // ── the floor ───────────────────────────────────────────────────────────

        private void OnMenuClicked() => _flow?.Open();

        private void OnSeatClicked(int index)
        {
            var run = Run;
            if (run == null || run.Phase != TycoonPhase.DayOpen) return;
            if (_flow != null && _flow.IsOpen) return;   // finish the build first
            var visit = _seats[index].Visit;
            bool haveDrink = !run.ServingGlass.IsEmpty || !run.Glass.IsEmpty;
            if (visit == null || visit.State != VisitState.Waiting || !haveDrink) return;

            run.ServeTo(visit);
            RefreshGlass();
        }

        private void OnBinClicked()
        {
            var run = Run;
            if (run == null || run.Phase != TycoonPhase.DayOpen) return;
            run.DiscardGlass();
            RefreshGlass();
        }

        // ── refresh ─────────────────────────────────────────────────────────────

        private void RefreshShelf(DiegeticStage.Exit exit)
        {
            if (stage == null || Run == null) return;
            // The bottles are scenery now (GDD 24 §1): building goes through the menu, so
            // the shelf takes no pour callbacks.
            stage.SetShelf(Run.Shelf, null, null, null, exit);
        }

        private void RefreshGlass()
        {
            if (stage == null || Run == null) return;
            stage.SetGlass(Run.Glass, id => Run.Shelf.Find(id)?.Ingredient);
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

            for (int i = 0; i < _seats.Count; i++)
            {
                var view = _seats[i];
                view.Visit = i < seated.Count ? seated[i] : null;
                bool locked = i >= run.Seats;

                if (locked)
                {
                    view.Frame.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.85f);
                    view.Name.text = "";
                    view.Wants.text = "";
                    view.Order.text = "LOCKED STOOL";
                    view.Order.color = UITheme.Cream[1];
                    view.PatienceFill.rectTransform.sizeDelta = new Vector2(0, -2);
                    continue;
                }

                if (view.Visit == null)
                {
                    view.Frame.color = new Color(UITheme.Night[1].r, UITheme.Night[1].g, UITheme.Night[1].b, 0.92f);
                    view.Name.text = "EMPTY STOOL";
                    view.Name.color = UITheme.Cream[2];
                    view.Wants.text = "";
                    view.Order.text = "";
                    view.PatienceFill.rectTransform.sizeDelta = new Vector2(0, -2);
                    continue;
                }

                var visit = view.Visit;
                view.Name.text = (visit.Regular?.Name ?? "Customer").ToUpperInvariant();
                view.Name.color = UITheme.TextPrimary;
                view.Order.color = UITheme.Amber[4];

                // The always-visible half of the read (GDD 19 §3) rides on the seat until
                // the per-seat licence lands in P6.
                view.Wants.text = visit.Read == null ? "" :
                    (visit.Read.Direction == IntentDirection.Extinguish ? "settle " : "lift ")
                    + visit.Read.Intent.ToString().ToUpperInvariant();

                view.Order.text = $"{visit.Order.Wanted.Name.ToUpperInvariant()}  ${visit.Order.Price}";

                float patience = (float)(visit.PatienceLeft / visit.PatienceMax);
                view.PatienceFill.rectTransform.sizeDelta = new Vector2(Mathf.Round(186f * patience), -2);
                view.PatienceFill.color = patience > 0.5f ? UITheme.Lime[3]
                    : patience > 0.25f ? UITheme.Amber[3] : UITheme.ViceRed[3];

                // The frame heats up as patience drains — anger readable at a glance. When a
                // drink is built and waiting, every seat glows cyan: "hand it over here".
                bool drinkReady = run.Phase == TycoonPhase.DayOpen &&
                    (!run.ServingGlass.IsEmpty || !run.Glass.IsEmpty) &&
                    (_flow == null || !_flow.IsOpen);
                if (drinkReady)
                {
                    view.Frame.color = new Color(UITheme.Selection.r, UITheme.Selection.g,
                        UITheme.Selection.b, 0.85f);
                }
                else
                {
                    var heat = Color.Lerp(UITheme.ViceRed[1], UITheme.Night[3], patience);
                    view.Frame.color = new Color(heat.r, heat.g, heat.b, 0.94f);
                }
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

            var sb = new StringBuilder();
            sb.AppendLine($"— DAY {run.Day} CLOSES —");
            sb.AppendLine($"served {served}   walked out {stormed}");
            sb.AppendLine($"satisfaction {floor.AverageSatisfaction:P0}");
            sb.AppendLine();
            sb.AppendLine($"rent due          ${run.Config.Rent(run.Day)}");
            sb.AppendLine($"restock the well  ${run.Shelf.RefillCost(run.Config.RefillPricePerCapacity)}");
            sb.AppendLine($"till              ${run.Money}");
            sb.AppendLine($"debt strikes      {run.Ledger.DebtStrikes}/{DayLedger.StrikesToClose}");
            _invoiceText.text = sb.ToString();

            // Rebuild the offer buttons: refill, market brands, a stool, tomorrow.
            foreach (Transform child in _offerRow) Destroy(child.gameObject);

            AddOfferButton($"RESTOCK ${run.Shelf.RefillCost(run.Config.RefillPricePerCapacity)}", () =>
            {
                if (run.Shelf.RefillCost(run.Config.RefillPricePerCapacity) > 0) run.RefillShelf();
                RebuildDayEnd();
            });

            for (int i = 0; i < run.MarketOffers.Count; i++)
            {
                int index = i;
                var offer = run.MarketOffers[i];
                if (offer.Sold) continue;
                AddOfferButton($"{offer.Bottle.Name.ToUpperInvariant()} ${offer.Price}", () =>
                {
                    run.BuyBrand(index);
                    RefreshShelf(DiegeticStage.Exit.Refresh);
                    RebuildDayEnd();
                });
            }

            if (run.Seats < run.Config.MaxSeats)
                AddOfferButton($"STOOL #{run.Seats + 1} ${run.Config.SeatPrice(run.Seats)}", () =>
                {
                    run.BuySeat();
                    RebuildDayEnd();
                });

            AddOfferButton("OPEN TOMORROW →", () =>
            {
                run.ContinueToNextDay();
                _dayEndPanel.gameObject.SetActive(false);
                if (run.Phase == TycoonPhase.DayOpen)
                {
                    _lastPhase = TycoonPhase.DayOpen;
                    RefreshGlass();
                }
            }, primary: true);
        }

        private void ShowClosed()
        {
            _dayEndPanel.gameObject.SetActive(false);
            _bannerText.gameObject.SetActive(true);
            _bannerText.text = "✖ THE BAR IS CLOSED\nthree days in the red — NEW RUN to try again";
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

            // Seat row along the bottom: six stools, click to serve.
            for (int i = 0; i < SeatSlots; i++)
            {
                int index = i;
                var seat = new SeatView();
                seat.Root = NewRect($"Seat{i}", root);
                seat.Root.anchorMin = seat.Root.anchorMax = new Vector2(0, 0);
                seat.Root.pivot = new Vector2(0, 0);
                seat.Root.sizeDelta = new Vector2(202, 96);
                seat.Root.anchoredPosition = new Vector2(8 + i * 212, 8);

                seat.Frame = seat.Root.gameObject.AddComponent<Image>();
                var button = seat.Root.gameObject.AddComponent<Button>();
                button.targetGraphic = seat.Frame;
                button.onClick.AddListener(() => OnSeatClicked(index));

                seat.Name = NewText("Name", seat.Root, _body, 13, TextAnchor.UpperLeft, UITheme.TextPrimary);
                Stretch(seat.Name.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-8, -6));

                seat.Wants = NewText("Wants", seat.Root, _body, 12, TextAnchor.UpperLeft, UITheme.Cyan[4]);
                Stretch(seat.Wants.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-8, -26));

                seat.Order = NewText("Order", seat.Root, _body, 12, TextAnchor.UpperLeft, UITheme.Amber[4]);
                Stretch(seat.Order.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-8, -46));

                var clockBg = NewRect("ClockBg", seat.Root);
                clockBg.anchorMin = new Vector2(0, 0); clockBg.anchorMax = new Vector2(1, 0);
                clockBg.offsetMin = new Vector2(8, 8); clockBg.offsetMax = new Vector2(-8, 18);
                clockBg.gameObject.AddComponent<Image>().color = UITheme.Night[0];
                var clockFill = NewRect("ClockFill", clockBg);
                clockFill.anchorMin = new Vector2(0, 0); clockFill.anchorMax = new Vector2(0, 1);
                clockFill.pivot = new Vector2(0, 0.5f);
                clockFill.offsetMin = new Vector2(1, 1); clockFill.offsetMax = new Vector2(1, -1);
                clockFill.anchoredPosition = new Vector2(1, 0);
                seat.PatienceFill = clockFill.gameObject.AddComponent<Image>();
                seat.PatienceFill.raycastTarget = false;

                _seats.Add(seat);
            }

            // The primary action: open the menu to build a drink (GDD 24 §1). Just above
            // the seat row, centred — the counter's "order pad".
            NewButton(root, "▸  MENU — MAKE A DRINK", new Vector2(0.5f, 0),
                new Vector2(300, 40), new Vector2(0, 112), UITheme.PrimaryAction, OnMenuClicked);

            // Day end: a plain invoice panel with the night's business under it.
            _dayEndPanel = NewRect("DayEnd", root);
            Place(_dayEndPanel, new Vector2(0.5f, 0.5f), new Vector2(560, 480), new Vector2(0, 40));
            var panelImg = _dayEndPanel.gameObject.AddComponent<Image>();
            panelImg.color = new Color(UITheme.Night[1].r, UITheme.Night[1].g, UITheme.Night[1].b, 0.96f);

            var title = NewText("Title", _dayEndPanel, _display, 16, TextAnchor.MiddleCenter, UITheme.PrimaryAction);
            Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -44), Vector2.zero);
            title.text = "LAST CALL — THE BOOKS";

            _invoiceText = NewText("Invoice", _dayEndPanel, _body, 14, TextAnchor.UpperLeft, UITheme.TextPrimary);
            Stretch(_invoiceText.rectTransform, new Vector2(0, 0), Vector2.one, new Vector2(28, 220), new Vector2(-28, -56));

            _offerRow = NewRect("Offers", _dayEndPanel);
            Stretch(_offerRow, Vector2.zero, new Vector2(1, 0), new Vector2(20, 16), new Vector2(-20, 212));
            var layout = _offerRow.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            _dayEndPanel.gameObject.SetActive(false);

            _bannerText = NewText("Closed", root, _display, 22, TextAnchor.MiddleCenter, UITheme.ViceRed[3]);
            Place(_bannerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(900, 120), new Vector2(0, 60));
            _bannerText.gameObject.SetActive(false);
        }

        private void AddOfferButton(string label, Action onClick, bool primary = false)
        {
            var rt = NewRect("Offer", _offerRow);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = primary ? UITheme.PrimaryAction : UITheme.Night[3];
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.AddListener(() => onClick());
            var text = NewText("Label", rt, _body, 13, TextAnchor.MiddleCenter,
                primary ? UITheme.TextOnAmber : UITheme.TextPrimary);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.text = label;
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
