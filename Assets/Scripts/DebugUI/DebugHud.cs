using System.Collections.Generic;
using System.Linq;
using LastCall.Core;
using LastCall.Game;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastCall.DebugUI
{
    /// <summary>
    /// M1 debug screen, built entirely in code (no scene-side layout to maintain):
    /// rail as clickable cards, live recipe preview, Mix/Restock, scrolling score log,
    /// seed/target inputs. Deliberately ugly — replaced by the real UI in M4.
    /// </summary>
    [RequireComponent(typeof(GameBootstrap))]
    public sealed class DebugHud : MonoBehaviour
    {
        private static readonly Dictionary<IngredientType, Color> TypeColors = new Dictionary<IngredientType, Color>
        {
            [IngredientType.Spirit] = new Color(0.88f, 0.60f, 0.24f),
            [IngredientType.Sour] = new Color(0.48f, 0.76f, 0.29f),
            [IngredientType.Sweet] = new Color(0.91f, 0.44f, 0.66f),
            [IngredientType.Bitter] = new Color(0.82f, 0.29f, 0.29f),
            [IngredientType.Bubbly] = new Color(0.29f, 0.78f, 0.82f),
            [IngredientType.Garnish] = new Color(0.85f, 0.70f, 0.23f)
        };

        private GameBootstrap _bootstrap;
        private readonly List<IngredientCard> _selected = new List<IngredientCard>();
        private Font _font;
        private bool _uiBuilt;
        private int _roundNumber;

        private Text _infoText;
        private Text _previewText;
        private Text _bannerText;
        private Text _logText;
        private ScrollRect _logScroll;
        private RectTransform _railPanel;
        private Button _mixButton;
        private Button _restockButton;
        private InputField _seedInput;
        private InputField _targetInput;

        private RoundController Round => _bootstrap.Round;

        private void Awake()
        {
            _bootstrap = GetComponent<GameBootstrap>();
            _bootstrap.RoundStarted += OnRoundStarted;
        }

        private void OnDestroy()
        {
            if (_bootstrap != null) _bootstrap.RoundStarted -= OnRoundStarted;
        }

        private void Start()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildUi();
            _uiBuilt = true;
            if (Round != null) OnRoundStarted();
        }

        private void OnRoundStarted()
        {
            if (!_uiBuilt) return;
            _selected.Clear();
            _roundNumber++;
            AppendLog($"═══ Round {_roundNumber} — seed '{_bootstrap.CurrentSeed}', target {Round.Customer.TargetScore:0.#} ═══");
            if (_seedInput != null) _seedInput.text = _bootstrap.CurrentSeed;
            RenderAll();
        }

        // ─────────────────────────────── actions ───────────────────────────────

        private void ToggleCard(IngredientCard card)
        {
            if (Round == null || Round.Phase != RoundPhase.InProgress) return;
            if (!_selected.Remove(card))
            {
                if (_selected.Count >= Round.Config.MaxMixSelection) return;
                _selected.Add(card);
            }
            RenderAll();
        }

        private void OnMixClicked()
        {
            int mixNumber = Round.Config.MixesPerCustomer - Round.MixesRemaining + 1;
            var breakdown = Round.Mix(_selected.ToList());
            _selected.Clear();

            if (breakdown.Recipe == null)
            {
                AppendLog($"— Mix {mixNumber}: no recipe, 0 points  [total {Round.AccumulatedScore:0.#} / {Round.Customer.TargetScore:0.#}]");
            }
            else
            {
                AppendLog($"— Mix {mixNumber}: {breakdown.Recipe.Name} (Lv{breakdown.RecipeLevel}) → " +
                          $"{breakdown.TotalFlavor:0.#} × {breakdown.TotalMult:0.#} = {breakdown.FinalScore:0.#}  " +
                          $"[total {Round.AccumulatedScore:0.#} / {Round.Customer.TargetScore:0.#}]");
                foreach (var step in breakdown.Steps)
                    AppendLog($"      {step.Source}: {StepText(step)} → F {step.FlavorAfter:0.#}, M {step.MultAfter:0.#}");
            }

            if (Round.Phase == RoundPhase.Won) AppendLog("★ SATISFIED! Customer served.");
            if (Round.Phase == RoundPhase.Lost) AppendLog("✖ LAST CALL — order failed, run over.");
            RenderAll();
        }

        private void OnRestockClicked()
        {
            int count = _selected.Count;
            Round.Restock(_selected.ToList());
            _selected.Clear();
            AppendLog($"— Restock: {count} card(s) swapped ({Round.RestocksRemaining} left)");
            RenderAll();
        }

        private void OnNewRoundClicked()
        {
            double.TryParse(_targetInput.text, out double target);
            _bootstrap.StartNewRound(_seedInput.text, target);
        }

        private static string StepText(ScoreStep step)
        {
            switch (step.Op)
            {
                case EffectOp.AddFlavor: return $"+{step.Value:0.#} Flavor";
                case EffectOp.AddMult: return $"+{step.Value:0.#} Mult";
                case EffectOp.MultMult: return $"×{step.Value:0.#} Mult";
                default: return $"{step.Op} {step.Value:0.#}";
            }
        }

        // ─────────────────────────────── rendering ───────────────────────────────

        private void RenderAll()
        {
            if (Round == null) return;

            _infoText.text =
                $"Customer: {Round.Customer.Name}\n" +
                $"Target:   {Round.Customer.TargetScore:0.#}\n" +
                $"Score:    {Round.AccumulatedScore:0.#}\n\n" +
                $"Mixes:    {Round.MixesRemaining}\n" +
                $"Restocks: {Round.RestocksRemaining}\n" +
                $"Cabinet:  {Round.DeckDrawCount} draw / {Round.DeckDiscardCount} discard";

            RenderPreview();
            RenderRail();
            RenderBanner();

            bool inProgress = Round.Phase == RoundPhase.InProgress;
            bool hasSelection = _selected.Count >= 1 && _selected.Count <= Round.Config.MaxMixSelection;
            _mixButton.interactable = inProgress && hasSelection;
            _restockButton.interactable = inProgress && hasSelection && Round.RestocksRemaining > 0;
        }

        private void RenderPreview()
        {
            if (_selected.Count == 0)
            {
                _previewText.text = "Select 1–5 ingredients…";
                return;
            }
            var preview = Round.PreviewScore(_selected);
            _previewText.text = preview.Recipe == null
                ? $"{_selected.Count} selected — no recipe (scores 0)"
                : $"{preview.Recipe.Name} (Lv{preview.RecipeLevel}) — {preview.TotalFlavor:0.#} × {preview.TotalMult:0.#} = {preview.FinalScore:0.#}";
        }

        private void RenderRail()
        {
            for (int i = _railPanel.childCount - 1; i >= 0; i--)
                Destroy(_railPanel.GetChild(i).gameObject);

            foreach (var card in Round.Rail)
            {
                var captured = card;
                bool selected = _selected.Contains(card);
                var baseColor = TypeColors[card.Type];
                var color = selected ? Color.Lerp(baseColor, Color.white, 0.55f) : baseColor;
                string label = $"{(selected ? "✔ " : string.Empty)}{card.Flavor}\n{card.Type}\n{card.Name}";
                NewButton($"Card_{card.InstanceId}", _railPanel, label, color, () => ToggleCard(captured), 14);
            }
        }

        private void RenderBanner()
        {
            switch (Round.Phase)
            {
                case RoundPhase.Won:
                    _bannerText.text = $"★ SATISFIED — {Round.AccumulatedScore:0.#} / {Round.Customer.TargetScore:0.#}";
                    _bannerText.color = new Color(0.55f, 0.95f, 0.55f);
                    break;
                case RoundPhase.Lost:
                    _bannerText.text = "✖ LAST CALL — round lost";
                    _bannerText.color = new Color(0.95f, 0.45f, 0.45f);
                    break;
                default:
                    _bannerText.text = string.Empty;
                    break;
            }
        }

        private void AppendLog(string line)
        {
            _logText.text = _logText.text.Length == 0 ? line : $"{_logText.text}\n{line}";
            if (_logText.text.Length > 12000)
                _logText.text = _logText.text.Substring(_logText.text.Length - 12000);
            Canvas.ForceUpdateCanvases();
            _logScroll.verticalNormalizedPosition = 0f;
        }

        // ─────────────────────────────── UI construction ───────────────────────────────

        private void BuildUi()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("DebugCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            var root = (RectTransform)canvasGo.transform;

            // Top-left: round state.
            _infoText = NewText("Info", root, 16, TextAnchor.UpperLeft, Color.white);
            Stretch((RectTransform)_infoText.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -180), new Vector2(392, -12));

            // Top-right: seed / target / new round.
            _seedInput = NewInput("SeedInput", root, "LASTCALL-DEV");
            Place((RectTransform)_seedInput.transform, new Vector2(1, 1), new Vector2(200, 30), new Vector2(-340, -12));
            _targetInput = NewInput("TargetInput", root, "300");
            Place((RectTransform)_targetInput.transform, new Vector2(1, 1), new Vector2(90, 30), new Vector2(-132, -12));
            var newRound = NewButton("NewRound", root, "New Round", new Color(0.35f, 0.35f, 0.55f), OnNewRoundClicked, 14);
            Place((RectTransform)newRound.transform, new Vector2(1, 1), new Vector2(120, 30), new Vector2(-12, -46));

            // Right: score log.
            var logPanel = NewRect("LogPanel", root);
            Stretch(logPanel, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-372, 170), new Vector2(-12, -86));
            logPanel.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
            _logScroll = logPanel.gameObject.AddComponent<ScrollRect>();
            var viewport = NewRect("Viewport", logPanel);
            Stretch(viewport, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = new Vector2(0, 0);
            _logText = content.gameObject.AddComponent<Text>();
            ConfigureText(_logText, 13, TextAnchor.UpperLeft, new Color(0.85f, 0.85f, 0.8f));
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _logScroll.content = content;
            _logScroll.viewport = viewport;
            _logScroll.horizontal = false;
            _logScroll.movementType = ScrollRect.MovementType.Clamped;

            // Center: win/lose banner + live preview.
            _bannerText = NewText("Banner", root, 34, TextAnchor.MiddleCenter, Color.white);
            Place((RectTransform)_bannerText.transform, new Vector2(0.5f, 0.5f), new Vector2(900, 60), new Vector2(0, 60));
            _previewText = NewText("Preview", root, 20, TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.75f));
            var previewRt = (RectTransform)_previewText.transform;
            previewRt.anchorMin = new Vector2(0.5f, 0);
            previewRt.anchorMax = new Vector2(0.5f, 0);
            previewRt.pivot = new Vector2(0.5f, 0);
            previewRt.sizeDelta = new Vector2(760, 34);
            previewRt.anchoredPosition = new Vector2(0, 218);

            // Bottom: action buttons + rail.
            _mixButton = NewButton("Mix", root, "MIX", new Color(0.30f, 0.55f, 0.30f), OnMixClicked, 18);
            var mixRt = (RectTransform)_mixButton.transform;
            mixRt.anchorMin = mixRt.anchorMax = mixRt.pivot = new Vector2(0.5f, 0);
            mixRt.sizeDelta = new Vector2(170, 42);
            mixRt.anchoredPosition = new Vector2(-95, 168);
            _restockButton = NewButton("Restock", root, "RESTOCK", new Color(0.55f, 0.40f, 0.25f), OnRestockClicked, 18);
            var restockRt = (RectTransform)_restockButton.transform;
            restockRt.anchorMin = restockRt.anchorMax = restockRt.pivot = new Vector2(0.5f, 0);
            restockRt.sizeDelta = new Vector2(170, 42);
            restockRt.anchoredPosition = new Vector2(95, 168);

            _railPanel = NewRect("Rail", root);
            Stretch(_railPanel, new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 12), new Vector2(-12, 160));
            _railPanel.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.35f);
            var layout = _railPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
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

        private static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        private void ConfigureText(Text text, int size, TextAnchor anchor, Color color)
        {
            text.font = _font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private Text NewText(string name, Transform parent, int size, TextAnchor anchor, Color color)
        {
            var rt = NewRect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            ConfigureText(text, size, anchor, color);
            return text;
        }

        private Button NewButton(string name, Transform parent, string label, Color bg, UnityAction onClick, int fontSize)
        {
            var rt = NewRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = bg;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var textRt = NewRect("Text", rt);
            Stretch(textRt, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
            var text = textRt.gameObject.AddComponent<Text>();
            ConfigureText(text, fontSize, TextAnchor.MiddleCenter, Color.black);
            text.text = label;
            return button;
        }

        private InputField NewInput(string name, Transform parent, string initial)
        {
            var rt = NewRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.2f);
            var input = rt.gameObject.AddComponent<InputField>();
            input.targetGraphic = image;

            var textRt = NewRect("Text", rt);
            Stretch(textRt, Vector2.zero, Vector2.one, new Vector2(8, 4), new Vector2(-8, -4));
            var text = textRt.gameObject.AddComponent<Text>();
            ConfigureText(text, 14, TextAnchor.MiddleLeft, Color.white);
            text.supportRichText = false;

            input.textComponent = text;
            input.text = initial;
            return input;
        }
    }
}
