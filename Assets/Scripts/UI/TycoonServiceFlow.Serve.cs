using System;
using System.Collections.Generic;
using System.Text;
using LastCall.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// The serve stage (GDD 24 §3): grab the shaker and tip it over the glass. How
    /// well the mouth lines up is the aim — off-centre spills, and what spills is
    /// gone.
    /// </summary>
    public sealed partial class TycoonServiceFlow
    {

        // The serve pour uses the same tilt model (GDD 24 §3): grab the shaker, tip it over
        // the glass. How well the mouth lines up over the glass is the aim — off-centre spills.
        private Text _serveShakerText;
        private Text _serveGlassText;
        private RectTransform _serveSurface;
        private RectTransform _serveGlass;      // the target
        private RectTransform _serveGarnishRow; // mint/olive garnishes are added here (2026-07-23)
        private RectTransform _serveShaker;     // the grabbable shaker
        private Image _serveShakerBody;
        private MetaballFluid _serveFluid;      // the metaball liquid in the serving glass
        private Splasher _serveSplash;

        private Text _aimText;
        private Vector2 _serveShakerRest;
        private bool _serveGrabbed;
        private const float ServePourRate = 0.34f;   // glass-fractions per second (slower, 2026-07-22)

        // ── the serve stage ──────────────────────────────────────────────────────

        private void RefreshServe()
        {
            var run = Run;
            _serveShakerText.text = run.Glass.IsEmpty
                ? "shaker empty"
                : $"shaker {run.Glass.FillFraction:P0} left";
            _serveGlassText.text = run.ServingGlass.IsEmpty
                ? "glass empty"
                : $"glass {run.ServingGlass.FillFraction:P0} full";
            _serveShaker.anchoredPosition = _serveShakerRest;
            _serveShaker.localRotation = Quaternion.identity;
            _serveSplash.Clear();
            _serveFluid.Clear();
            _serveFluid.SetColor(DrinkColor(run.Glass));
            PushServePool(run);
            _serveShakerBody.color = DrinkColor(run.Glass);
            _aimText.text = "GRAB THE SHAKER · TIP IT OVER THE GLASS";

            // The stocked garnishes (mint, olive), added into the drink before it is poured.
            foreach (Transform ch in _serveGarnishRow) Destroy(ch.gameObject);
            foreach (var bottle in run.Shelf.Bottles)
                if (bottle.Ingredient.Type == IngredientType.Garnish && !bottle.IsEmpty)
                    AddGarnishChip(bottle.Ingredient);
        }

        private void AddGarnishChip(IngredientCard card)
        {
            var chip = NewRect($"G_{card.Id}", _serveGarnishRow);
            chip.gameObject.AddComponent<LayoutElement>().preferredHeight = 66;
            var bg = chip.gameObject.AddComponent<Image>();
            bg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.85f);
            var icon = NewRect("Icon", chip);
            Place(icon, new Vector2(0.5f, 1), new Vector2(46, 46), new Vector2(0, -3));
            var iimg = icon.gameObject.AddComponent<Image>();
            iimg.sprite = ItemArt.Bottle(card.Info?.Style); iimg.preserveAspect = true; iimg.raycastTarget = false;
            if (iimg.sprite == null) iimg.color = UITheme.StyleColor(card.Info?.Style, card.Type);
            var name = NewText("N", chip, _body, 8, TextAnchor.LowerCenter, UITheme.TextPrimary);
            Place(name.rectTransform, new Vector2(0.5f, 0), new Vector2(92, 14), new Vector2(0, 2));
            name.text = card.Name.ToUpperInvariant();
            var btn = chip.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var c = card;
            btn.onClick.AddListener(() => { if (Run != null && !Run.Glass.IsEmpty) { Run.PourGarnish(c.Id); RefreshServe(); } });
        }

        /// <summary>
        /// One frame of the serve pour (GDD 24 §3): the shaker tips the same way the bottle
        /// did. How well the mouth lines up over the glass is the aim — dead over the glass
        /// pours clean, drifting off spills, and a full pour still drains the shaker.
        /// </summary>
        private void UpdateServeTilt(TycoonRun run)
        {
            if (Mouse.current == null) return;
            if (_serveGrabbed && !Mouse.current.leftButton.isPressed) _serveGrabbed = false;

            bool pourNow = false;
            double accuracy = 0;
            if (_serveGrabbed && !run.Glass.IsEmpty &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _serveSurface, Mouse.current.position.ReadValue(), null, out Vector2 local))
            {
                float halfW = _serveSurface.rect.width * 0.5f;
                float halfH = _serveSurface.rect.height * 0.5f;
                local.x = Mathf.Clamp(local.x, -halfW + 30f, halfW - 30f);
                local.y = Mathf.Clamp(local.y, -halfH + 20f, halfH - 20f);
                _serveShaker.anchoredPosition = local;

                float lift = Mathf.Clamp01((local.y - _serveShakerRest.y) / LiftRange);
                float tilt = lift * MaxTilt;
                _serveShaker.localRotation = Quaternion.Euler(0, 0, tilt);

                float rad = tilt * Mathf.Deg2Rad;
                Vector2 mouth = local + new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad)) * (BottleH * 0.78f);
                var opening = _serveGlass.anchoredPosition + new Vector2(0, _serveGlass.rect.height * 0.5f);

                if (tilt > 42f && mouth.y > opening.y - 30f)
                {
                    // Aim: how well the mouth is centred over the glass. Within ~half the
                    // glass width is a clean pour; beyond that it spills more the further off.
                    accuracy = Mathf.Clamp01(1f - Mathf.Abs(mouth.x - opening.x) / 90f);
                    pourNow = true;

                    // The stream falls toward where the aim sends it: dead-on it drops into the
                    // glass and melts into the drink; off-aim it drifts wide and misses the rim,
                    // falling past onto the counter — the spill you can see (GDD 24 §3).
                    var colour = DrinkColor(run.Glass);
                    _serveFluid.SetColor(colour);
                    float landX = Mathf.Lerp(mouth.x + (mouth.x - opening.x) * 1.5f, opening.x, (float)accuracy);
                    var streamVel = new Vector2((landX - mouth.x) * 1.8f, -225f);
                    _serveFluid.EmitStream(mouth, streamVel, Time.deltaTime);
                }
            }

            if (pourNow)
            {
                double before = run.ServingGlass.TotalVolume;
                run.PourIntoServingGlass(ServePourRate * Time.deltaTime, accuracy);
                if (run.Glass.IsEmpty || run.ServingGlass.FillFraction >= 1.0) _serveGrabbed = false;
                if (run.ServingGlass.TotalVolume != before) RefreshServeText(run, accuracy);
            }

            PushServePool(run);
            _serveFluid.Step(Time.deltaTime);
            _serveSplash.Step(Time.deltaTime);
        }

        /// <summary>Places the serving glass's pooled liquid from its interior and live fill.</summary>
        private void PushServePool(TycoonRun run)
        {
            if (run.ServingGlass.IsEmpty) { _serveFluid.ClearPool(); return; }
            // Fill the tumbler's INTERIOR (inset from the crystal walls) so the drink pools
            // inside the glass, not as a box behind it (2026-07-23).
            var c = _serveGlass.anchoredPosition;
            float halfW = _serveGlass.rect.width * 0.5f;
            float iw = halfW * 0.66f;
            float minX = c.x - iw;
            float maxX = c.x + iw;
            float h = _serveGlass.rect.height;
            float bottomY = c.y - h * 0.5f + h * 0.14f;
            float innerH = h * 0.6f;
            float rimY = bottomY + innerH;
            _serveFluid.SetPool(minX, maxX, bottomY, rimY, (float)run.ServingGlass.FillFraction);
        }

        private void RefreshServeText(TycoonRun run, double accuracy)
        {
            _serveShakerText.text = $"shaker {run.Glass.FillFraction:P0} left";
            _serveGlassText.text = $"glass {run.ServingGlass.FillFraction:P0} full";
            _aimText.text = accuracy > 0.8 ? "CLEAN POUR" : accuracy > 0.4 ? "SOME SPILL" : "SPILLING!";
            _aimText.color = Color.Lerp(UITheme.ViceRed[3], UITheme.Lime[3], (float)accuracy);
        }

        private void BuildServePanel()
        {
            _servePanel = NewRect("ServePanel", _root);
            Place(_servePanel, new Vector2(0.5f, 0.5f), new Vector2(1120, 640), Vector2.zero);
            _servePanel.gameObject.AddComponent<Image>().color = UITheme.Night[1];
            Swallow(_servePanel);

            var title = NewText("Title", _servePanel, _display, 16, TextAnchor.UpperCenter, UITheme.PrimaryAction);
            Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -40), new Vector2(0, -10));
            title.text = "POUR THE GLASS";

            _serveShakerText = NewText("Shaker", _servePanel, _body, 13, TextAnchor.UpperLeft, UITheme.TextSecondary);
            Place(_serveShakerText.rectTransform, new Vector2(0, 1), new Vector2(280, 24), new Vector2(20, -46));
            _serveGlassText = NewText("Glass", _servePanel, _body, 13, TextAnchor.UpperRight, UITheme.TextPrimary);
            Place(_serveGlassText.rectTransform, new Vector2(1, 1), new Vector2(280, 24), new Vector2(-20, -46));

            // Garnishes (mint, olive) live at the serve stage now — a small row down the left.
            // Add one before you pour and it goes into the drink.
            var glabel = NewText("GLabel", _servePanel, _body, 10, TextAnchor.LowerLeft, UITheme.TypeRamp[IngredientType.Garnish][3]);
            Place(glabel.rectTransform, new Vector2(0, 0.5f), new Vector2(96, 16), new Vector2(14, 118));
            glabel.text = "— GARNISH —";
            _serveGarnishRow = NewRect("Garnishes", _servePanel);
            Place(_serveGarnishRow, new Vector2(0, 0.5f), new Vector2(96, 224), new Vector2(14, 0));
            var grow = _serveGarnishRow.gameObject.AddComponent<VerticalLayoutGroup>();
            grow.spacing = 8f; grow.childControlWidth = true; grow.childForceExpandWidth = true;
            grow.childControlHeight = false; grow.childAlignment = TextAnchor.UpperCenter;

            _aimText = NewText("AimText", _servePanel, _body, 13, TextAnchor.UpperCenter, UITheme.TextSecondary);
            Stretch(_aimText.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(0, -70), new Vector2(0, -46));

            // The play surface: the glass sits centre-left, the shaker rests lower-right.
            _serveSurface = NewRect("ServeSurface", _servePanel);
            Stretch(_serveSurface, Vector2.zero, Vector2.one, new Vector2(20, 84), new Vector2(-20, -82));
            var surfImg = _serveSurface.gameObject.AddComponent<Image>();
            surfImg.color = new Color(UITheme.Night[0].r, UITheme.Night[0].g, UITheme.Night[0].b, 0.5f);
            surfImg.raycastTarget = false;

            // The serving glass: real clear-glass art (2026-07-23), transparent interior so the
            // poured drink pools behind it and shows through; the outline+rim draw in front.
            _serveGlass = NewRect("Glass", _serveSurface);
            Place(_serveGlass, new Vector2(0.5f, 0.5f), new Vector2(150, 186), new Vector2(-210, -34));
            var glassImg = _serveGlass.gameObject.AddComponent<Image>();
            glassImg.raycastTarget = false;
            if (ItemArt.Glass != null) { glassImg.sprite = ItemArt.Glass; glassImg.preserveAspect = true; glassImg.color = Color.white; }
            else
            {
                glassImg.color = UITheme.Cream[2];
                var bowl = NewRect("Bowl", _serveGlass);
                Stretch(bowl, Vector2.zero, Vector2.one, new Vector2(5, 5), new Vector2(-5, -14));
                bowl.gameObject.AddComponent<Image>().color = UITheme.Night[3];
                var rim = NewRect("Rim", _serveGlass);
                Place(rim, new Vector2(0.5f, 1), new Vector2(104, 12), new Vector2(0, 0));
                rim.gameObject.AddComponent<Image>().color = UITheme.Cyan[3];
            }

            _serveFluid = new MetaballFluid(_serveSurface);
            // The tumbler: a slightly narrower base opening out to the mouth.
            _serveFluid.SetProfile(new[] { 0.88f, 0.93f, 0.96f, 0.98f, 1.00f, 1.00f });
            _serveSplash = new Splasher(_serveSurface);
            if (ItemArt.Glass != null) _serveGlass.SetAsLastSibling();   // clear glass over the fluid

            // The grabbable steel shaker you pour from, resting lower-right.
            _serveShakerRest = new Vector2(280, -70);
            _serveShaker = NewRect("Shaker", _serveSurface);
            _serveShaker.pivot = new Vector2(0.5f, 0.22f);
            _serveShaker.sizeDelta = new Vector2(104, BottleH);
            _serveShaker.anchoredPosition = _serveShakerRest;
            _serveShakerBody = _serveShaker.gameObject.AddComponent<Image>();
            if (ItemArt.Shaker != null) { _serveShakerBody.sprite = ItemArt.Shaker; _serveShakerBody.preserveAspect = true; _serveShakerBody.color = Color.white; }
            else
            {
                _serveShakerBody.color = UITheme.Cream[3];
                var cap = NewRect("Cap", _serveShaker);
                Place(cap, new Vector2(0.5f, 1), new Vector2(40, 22), new Vector2(0, 0));
                cap.gameObject.AddComponent<Image>().color = UITheme.Cream[4];
            }
            var sgrab = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            sgrab.callback.AddListener(_ =>
            {
                if (Run != null && Run.Phase == TycoonPhase.DayOpen && !Run.Glass.IsEmpty)
                    _serveGrabbed = true;
            });
            _serveShaker.gameObject.AddComponent<EventTrigger>().triggers.Add(sgrab);

            var back = NewRect("Back", _servePanel);
            Place(back, new Vector2(0.5f, 0), new Vector2(240, 34), new Vector2(-130, 12));
            back.gameObject.AddComponent<Image>().color = UITheme.Night[3];
            back.gameObject.AddComponent<Button>().onClick.AddListener(() => GoTo(Stage.Menu));
            var backLabel = NewText("Label", back, _body, 13, TextAnchor.MiddleCenter, UITheme.TextPrimary);
            Stretch(backLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            backLabel.text = "← ADD MORE";

            var done = NewRect("Done", _servePanel);
            Place(done, new Vector2(0.5f, 0), new Vector2(240, 34), new Vector2(130, 12));
            done.gameObject.AddComponent<Image>().color = UITheme.PrimaryAction;
            done.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                // Ready to hand over: close the flow, then click a seat to deliver.
                if (!Run.ServingGlass.IsEmpty) GoTo(Stage.Closed);
            });
            var doneLabel = NewText("Label", done, _body, 13, TextAnchor.MiddleCenter, UITheme.TextOnAmber);
            Stretch(doneLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            doneLabel.text = "SERVE IT → PICK A SEAT";
        }
    }
}
