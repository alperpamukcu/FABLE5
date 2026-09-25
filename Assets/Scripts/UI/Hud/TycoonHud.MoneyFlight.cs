using LastCall.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    // TycoonHud, part MoneyFlight: THE BILLS FLY (2026-09-25, the author: "para harcandığında gittiğinde geldiğinde bu
    // banknotlar animasyonla uçar tek tek").
    //
    // The money mark is a stack of one bill laid down three times (Tools/money_icon.py), and that one bill is what
    // moves: when the till goes up, bills fly into it one after another from wherever the money came from - the stool
    // of the drinker who just settled, by default the middle of the room - and the till swells as each lands; when the
    // till goes down, bills leave it one after another and fall away. How many is read off the amount, not one per
    // dollar: a two-dollar soda is one bill, a night's rent a handful, a fitting at most six.
    //
    // It watches the till rather than every verb that moves money, so nothing that pays or charges has to remember to
    // call it - a new source of money flies the day it is written. A verb that knows WHERE its money comes from or goes
    // to says so first (MoneyFrom / MoneyTo) and the bills take that road; everything else takes the default one.
    public sealed partial class TycoonHud
    {
        /// <summary>Over the market (22), the bench (25) and the licence (26); with the note (28); under the pause
        /// menu (29) and the curtain (30), which is where the day's rent is billed and should not be seen flying.</summary>
        private const int MoneyFxSortingOrder = 28;

        private const float BillFlightSeconds = 0.55f, BillStagger = 0.09f, BillScale = 2f;
        private const float MoneyHintLife = 1.5f;

        private RectTransform _moneyFx;
        private TycoonRun _flightRun;   // the run the till was last read for: a new run's purse is not income
        private int _flightSeen;
        private Vector2 _moneyFromHint, _moneyToHint;
        private float _moneyFromUntil = -1f, _moneyToUntil = -1f;

        /// <summary>The next money INTO the till came from here (a screen point), for the next moment or so.</summary>
        private void MoneyFrom(Vector2 screen)
        {
            _moneyFromHint = screen;
            _moneyFromUntil = Time.unscaledTime + MoneyHintLife;
        }

        /// <summary>The next money OUT of the till goes to here (a screen point), for the next moment or so.</summary>
        private void MoneyTo(Vector2 screen)
        {
            _moneyToHint = screen;
            _moneyToUntil = Time.unscaledTime + MoneyHintLife;
        }

        private RectTransform MoneyFx()
        {
            if (_moneyFx != null || _hudRoot == null) return _moneyFx;
            _moneyFx = NewRect("MoneyFx", _hudRoot);
            Stretch(_moneyFx, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var canvas = _moneyFx.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = MoneyFxSortingOrder;
            var group = _moneyFx.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            return _moneyFx;
        }

        /// <summary>The till the player is looking at: the tablet's while the night's sheets are up, the beam's
        /// otherwise.</summary>
        private RectTransform TillForFlight()
        {
            if (_tabletTill != null && _tabletTill.gameObject.activeInHierarchy && Showing(_dayEndPanel))
                return _tabletTill.rectTransform;
            return _beamTillCard != null && _beamTillCard.gameObject.activeInHierarchy ? _beamTillCard : null;
        }

        /// <summary>How many bills an amount is worth flying: one for small change, six at most.</summary>
        private static int BillsFor(int amount)
        {
            amount = Mathf.Abs(amount);
            return amount <= 5 ? 1 : amount <= 15 ? 2 : amount <= 40 ? 3 : amount <= 100 ? 4 : amount <= 300 ? 5 : 6;
        }

        /// <summary>Once a frame: compares the till with what it was and flies the difference.</summary>
        private void StepMoneyFlight()
        {
            var run = Run;
            if (run == null) { _flightRun = null; return; }
            if (!ReferenceEquals(_flightRun, run))
            {
                _flightRun = run;
                _flightSeen = run.Money;
                return;
            }
            int delta = run.Money - _flightSeen;
            if (delta == 0) return;
            _flightSeen = run.Money;
            if (Motion.Reduced) return;

            var fx = MoneyFx();
            var till = TillForFlight();
            var art = ItemArt.MoneyBill();
            if (fx == null || till == null || art == null) return;
            if (!ToFx(fx, CentreOnScreen(till), out var tillAt)) return;

            int n = BillsFor(delta);
            var punch = till.GetComponent<UiPunch>();
            if (punch == null) punch = till.gameObject.AddComponent<UiPunch>();
            float now = Time.unscaledTime;

            if (delta > 0)
            {
                // IN: from where it came from (the settling stool), else up from the middle of the room.
                Vector2 fromScreen = now <= _moneyFromUntil ? _moneyFromHint
                    : new Vector2(Screen.width * 0.5f, Screen.height * 0.38f);
                if (!ToFx(fx, fromScreen, out var from)) return;
                for (int i = 0; i < n; i++)
                {
                    var fly = Bill(fx, art, from + Jitter(i, 10f), tillAt);
                    fly.Seconds = BillFlightSeconds;
                    fly.Delay = i * BillStagger;
                    fly.ScaleFrom = 1f; fly.ScaleTo = 0.55f;
                    fly.AlphaFrom = 1f; fly.AlphaTo = 0.85f;
                    fly.Arc = 70f + i * 6f;
                    fly.Done = () => { if (punch != null) punch.Play(); };
                }
            }
            else
            {
                // OUT: from the till to where it is going, else away and down, fading as it goes.
                bool aimed = now <= _moneyToUntil;
                Vector2 to = tillAt + new Vector2(-140f, -150f);
                if (aimed && ToFx(fx, _moneyToHint, out var hinted)) to = hinted;
                if (punch != null) punch.Play();
                for (int i = 0; i < n; i++)
                {
                    var fly = Bill(fx, art, tillAt, to + Jitter(i, aimed ? 8f : 36f));
                    fly.Seconds = BillFlightSeconds + 0.1f;
                    fly.Delay = i * BillStagger;
                    fly.ScaleFrom = 0.55f; fly.ScaleTo = aimed ? 0.7f : 0.9f;
                    fly.AlphaFrom = 1f; fly.AlphaTo = aimed ? 0.6f : 0f;
                    fly.Arc = aimed ? 50f : 30f;
                }
            }
        }

        /// <summary>One bill on the flight layer, at the house's 2x.</summary>
        private UiFlight Bill(RectTransform fx, Sprite art, Vector2 from, Vector2 to)
        {
            var rt = NewRect("Bill", fx);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(art.rect.width * BillScale, art.rect.height * BillScale);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = art;
            img.raycastTarget = false;
            var fly = rt.gameObject.AddComponent<UiFlight>();
            fly.From = from;
            fly.To = to;
            return fly;
        }

        /// <summary>A small, fixed scatter per bill, so a handful does not fly as one: no dice, the same every time.</summary>
        private static Vector2 Jitter(int i, float r)
        {
            float a = i * 2.39996f;                   // the golden angle
            float k = Mathf.Sqrt((i + 1) / 6f);
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r * k;
        }

        private static Vector2 CentreOnScreen(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            return RectTransformUtility.WorldToScreenPoint(null, (c[0] + c[2]) * 0.5f);
        }

        private static bool ToFx(RectTransform fx, Vector2 screen, out Vector2 local) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(fx, screen, null, out local);
    }
}
