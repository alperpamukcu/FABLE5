using System;
using System.Collections.Generic;
using System.Linq;
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
        private RectTransform _openTomorrow;
        private Text _bannerText;

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
        private double _lastShakerVol = -1;
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
            CloseId();
            if (stage != null) stage.SetSoloCustomerVisible(false);
            RefreshShelf(DiegeticStage.Exit.Refresh);
            ApplyBarLook();
            RefreshGlass();
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

            if (_toast != null && _toast.gameObject.activeSelf && Time.unscaledTime > _toastUntil)
                _toast.gameObject.SetActive(false);

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
            if (visit == null || visit.State != VisitState.Waiting) return;

            // A drink in hand means you are here to serve; empty-handed, you are here to
            // read them (GDD 24 §5). Reading first, serving second — the licence is the ask.
            bool haveDrink = !run.ServingGlass.IsEmpty || !run.Glass.IsEmpty;
            if (haveDrink)
            {
                var verdict = run.ServeTo(visit);
                CloseId();
                RefreshGlass();
                // Money is celebrated (GDD 24 §10): the payment floats up from the seat.
                StartCoroutine(FloatMoney(index, verdict.Total));
            }
            else
            {
                ShowId(visit);
            }
        }

        /// <summary>A green +$N that rises from the paying seat and fades (GDD 24 §10).</summary>
        private System.Collections.IEnumerator FloatMoney(int seatIndex, int amount)
        {
            var seat = _seats[seatIndex].Root;
            var text = NewText("Float", seat.parent, _display, 18, TextAnchor.MiddleCenter, UITheme.Lime[3]);
            text.text = $"+${amount}";
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(120, 30);
            var start = seat.anchoredPosition + new Vector2(seat.sizeDelta.x * 0.5f - 60f, 100f);

            const float duration = 1.1f;
            float t = 0f;
            while (t < duration && text != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                rt.anchoredPosition = start + new Vector2(0, 46f * k);
                text.color = new Color(UITheme.Lime[3].r, UITheme.Lime[3].g, UITheme.Lime[3].b,
                    1f - k * k);
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

        /// <summary>Pushes the bought ambience upgrades onto the scene (GDD 24 §6).</summary>
        private void ApplyBarLook()
        {
            var run = Run;
            if (stage == null || run == null) return;
            stage.ApplyBarLook(run.GlasswareTier, run.CounterTier, run.WallTier, run.HasMusician);
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

            // The licence is only good while its holder is at the bar.
            if (_idVisit != null && (_idVisit.State != VisitState.Waiting || !seated.Contains(_idVisit)))
                CloseId();

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
                // A regular ordering again after a perfect serve gets a gold star and the
                // round count (GDD 24 §4) — the reward for reading them right, made visible.
                string star = visit.ExtraOrdersTaken > 0
                    ? $"<color=#F5C97B>★{visit.ExtraOrdersTaken + 1} </color>" : "";
                view.Name.supportRichText = true;
                view.Name.text = star + (visit.Regular?.Name ?? "Customer").ToUpperInvariant();
                view.Name.color = UITheme.TextPrimary;
                view.Order.color = UITheme.Amber[4];

                // The always-visible half of the read (GDD 19 §3); tap the seat for the full
                // licence (GDD 24 §5). "TAP TO READ" nudges the newcomer.
                view.Wants.text = visit.Read == null ? "TAP TO READ" :
                    (visit.Read.Direction == IntentDirection.Extinguish ? "settle " : "lift ")
                    + visit.Read.Intent.ToString().ToUpperInvariant() + "   · tap to read";

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
                    RefreshShelf(DiegeticStage.Exit.Refresh);
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
        }

        private void OnOpenTomorrow()
        {
            var run = Run;
            run.ContinueToNextDay();
            _dayEndPanel.gameObject.SetActive(false);
            if (run.Phase == TycoonPhase.DayOpen)
            {
                _lastPhase = TycoonPhase.DayOpen;
                _lastShakerVol = -1;
                RefreshShelf(DiegeticStage.Exit.Refresh);
                ApplyBarLook();
                RefreshGlass();
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
