using System;
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
    /// M2 debug screen, built entirely in code (no scene-side layout to maintain).
    /// Drives a full run: rail play with live recipe preview, Tools on selections,
    /// patron shelf with selling, the Back Room shop between customers, and the
    /// run win/lose flow. Deliberately ugly — replaced by the real UI in M4.
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

        private Text _infoText;
        private Text _previewText;
        private Text _bannerText;
        private Text _logText;
        private ScrollRect _logScroll;
        private RectTransform _railPanel;
        private RectTransform _patronPanel;
        private RectTransform _toolPanel;
        private RectTransform _shopPanel;
        private RectTransform _shopOffersPanel;
        private Text _shopTitle;
        private RectTransform _recipePanel;
        private Text _recipeText;
        private bool _recipesVisible;
        private Button _mixButton;
        private Button _restockButton;
        private InputField _seedInput;

        private RunController Run => _bootstrap.Run;
        private RoundController Round => Run?.CurrentRound;

        private void Awake()
        {
            _bootstrap = GetComponent<GameBootstrap>();
            _bootstrap.RunStarted += OnRunStarted;
        }

        private void OnDestroy()
        {
            if (_bootstrap != null) _bootstrap.RunStarted -= OnRunStarted;
        }

        private void Start()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildUi();
            _uiBuilt = true;
            if (Run != null) OnRunStarted();
        }

        private void OnRunStarted()
        {
            if (!_uiBuilt) return;
            _selected.Clear();
            AppendLog($"═══ New run — seed '{_bootstrap.CurrentSeed}' ═══");
            AppendLog(CustomerHeader());
            if (_seedInput != null) _seedInput.text = _bootstrap.CurrentSeed;
            RenderAll();
        }

        private string CustomerHeader() =>
            $"— {Round.Customer.Name}: target {Round.Customer.TargetScore:0.#} (wallet ${Run.Money})";

        // ─────────────────────────────── actions ───────────────────────────────

        private void ToggleCard(IngredientCard card)
        {
            if (Run == null || Run.Phase != RunPhase.CustomerRound) return;
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
            var breakdown = Run.Mix(_selected.ToList());
            _selected.Clear();

            if (breakdown.Recipe == null)
            {
                AppendLog($"— Mix {mixNumber}: no recipe, 0 points  [{Round.AccumulatedScore:0.#} / {Round.Customer.TargetScore:0.#}]");
            }
            else if (breakdown.IsVoided)
            {
                AppendLog($"— Mix {mixNumber}: {breakdown.Recipe.Name} VOIDED ({breakdown.VoidReason})  " +
                          $"[{Round.AccumulatedScore:0.#} / {Round.Customer.TargetScore:0.#}]");
            }
            else
            {
                AppendLog($"— Mix {mixNumber}: {breakdown.Recipe.Name} (Lv{breakdown.RecipeLevel}) → " +
                          $"{breakdown.TotalFlavor:0.#} × {breakdown.TotalMult:0.#} = {breakdown.FinalScore:0.#}  " +
                          $"[{Round.AccumulatedScore:0.#} / {Round.Customer.TargetScore:0.#}]");
                foreach (var step in breakdown.Steps)
                    AppendLog($"      {step.Source}: {StepText(step)} → F {step.FlavorAfter:0.#}, M {step.MultAfter:0.#}");
            }

            foreach (var card in Round.LastShatteredCards)
                AppendLog($"      ✖ {card.Name} shattered — destroyed for good");
            foreach (var copy in Round.LastDoubledCopies)
                AppendLog($"      + {copy.Name} doubled — a copy joins the deck");

            if (Round.Phase == RoundPhase.Won)
            {
                var tips = Run.LastTips;
                AppendLog($"★ Satisfied! Tips: base {tips.Base} + mixes {tips.UnusedMixBonus} + interest {tips.Interest}" +
                          $"{(tips.VipBonus > 0 ? $" + VIP {tips.VipBonus}" : "")}" +
                          $"{(tips.PatronBonus > 0 ? $" + patrons {tips.PatronBonus}" : "")}" +
                          $"{(tips.GoldenBonus > 0 ? $" + golden {tips.GoldenBonus}" : "")} = ${tips.Total} (wallet ${Run.Money})");
            }

            if (Run.Phase == RunPhase.RunWon) AppendLog("★ OPENING WEEK SURVIVED — run won!");
            if (Run.Phase == RunPhase.RunLost) AppendLog("✖ LAST CALL — order failed, run over.");
            RenderAll();
        }

        private void OnRestockClicked()
        {
            int count = _selected.Count;
            Run.Restock(_selected.ToList());
            _selected.Clear();
            AppendLog($"— Restock: {count} card(s) swapped ({Round.RestocksRemaining} left)");
            RenderAll();
        }

        private void OnUseToolClicked(ToolDefinition tool)
        {
            Guarded(() =>
            {
                Run.UseTool(tool, _selected.ToList());
                AppendLog($"— {tool.Name} used on {_selected.Count} card(s)");
                _selected.Clear();
            });
        }

        private void OnSellPatronClicked(PatronInstance patron)
        {
            Guarded(() =>
            {
                Run.SellPatron(patron);
                AppendLog($"— Sold {patron.Definition.Name} (wallet ${Run.Money})");
            });
        }

        private void OnBuyOfferClicked(int index)
        {
            Guarded(() =>
            {
                var offer = Run.Shop.Offers[index];
                Run.BuyOffer(index);
                AppendLog($"— Bought {offer.DisplayName} for ${offer.Price} (wallet ${Run.Money})");
            });
        }

        private void OnBuyVoucherClicked()
        {
            Guarded(() =>
            {
                var voucher = Run.Shop.VoucherOffer.Voucher;
                Run.BuyVoucher();
                AppendLog($"— Voucher: {voucher.Name} — {voucher.Description} (wallet ${Run.Money})");
            });
        }

        private void OnBuyPackClicked(int index)
        {
            Guarded(() =>
            {
                var offer = Run.Shop.PackOffers[index];
                Run.BuyPack(index);
                AppendLog($"— Opened {offer.DisplayName} for ${offer.Price}");
            });
        }

        private void OnPickPackOptionClicked(int optionIndex)
        {
            Guarded(() =>
            {
                var option = Run.OpenPack.Options[optionIndex];
                Run.PickFromPack(optionIndex);
                AppendLog($"— Took {option.DisplayName} from the pack");
            });
        }

        private void OnSkipPackClicked()
        {
            Guarded(() =>
            {
                Run.SkipPack();
                AppendLog("— Pack skipped");
            });
        }

        private void OnRerollClicked()
        {
            Guarded(() =>
            {
                int cost = Run.Shop.RerollCost;
                Run.RerollShop();
                AppendLog($"— Shop rerolled for ${cost}");
            });
        }

        private void OnContinueClicked()
        {
            Guarded(() =>
            {
                Run.ContinueToNextCustomer();
                _selected.Clear();
                AppendLog(CustomerHeader());
            });
        }

        private void OnNewRunClicked() => _bootstrap.StartNewRun(_seedInput.text);

        private void OnToggleRecipesClicked()
        {
            _recipesVisible = !_recipesVisible;
            RenderAll();
        }

        /// <summary>Debug UI stays alive on misuse: rule violations land in the log instead of the console.</summary>
        private void Guarded(Action action)
        {
            try { action(); }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
            {
                AppendLog($"!! {ex.Message}");
            }
            RenderAll();
        }

        private static string StepText(ScoreStep step)
        {
            switch (step.Op)
            {
                case EffectOp.AddFlavor: return $"+{step.Value:0.#} Flavor";
                case EffectOp.AddMult: return $"+{step.Value:0.#} Mult";
                case EffectOp.MultMult: return $"×{step.Value:0.#} Mult";
                case EffectOp.Retrigger: return "retrigger";
                case EffectOp.Accumulate: return $"+{step.Value:0.#} stored";
                default: return $"{step.Op} {step.Value:0.#}";
            }
        }

        // ─────────────────────────────── rendering ───────────────────────────────

        private void RenderAll()
        {
            if (Run == null) return;

            string vipLine = string.IsNullOrEmpty(Round.Customer.RuleText)
                ? string.Empty
                : $"\n<color=#FF9F5A>VIP RULE: {Round.Customer.RuleText}</color>";
            _infoText.text =
                $"Night {Run.Night}/{Run.Config.Nights} — {Run.Slot}\n" +
                $"Wallet:   ${Run.Money}\n" +
                $"Target:   {Round.Customer.TargetScore:0.#}\n" +
                $"Score:    {Round.AccumulatedScore:0.#}\n" +
                $"Mixes:    {Round.MixesRemaining}   Restocks: {Round.RestocksRemaining}\n" +
                $"Cabinet:  {Round.DeckDrawCount} draw / {Round.DeckDiscardCount} discard" +
                vipLine;

            RenderPreview();
            RenderRail();
            RenderPatrons();
            RenderTools();
            RenderShop();
            RenderRecipeBook();
            RenderBanner();

            bool inRound = Run.Phase == RunPhase.CustomerRound;
            bool hasSelection = _selected.Count >= 1 && _selected.Count <= Round.Config.MaxMixSelection;
            _mixButton.interactable = inRound && hasSelection;
            _restockButton.interactable = inRound && hasSelection && Round.RestocksRemaining > 0;
        }

        private void RenderPreview()
        {
            if (Run.Phase != RunPhase.CustomerRound)
            {
                _previewText.text = string.Empty;
                return;
            }
            if (_selected.Count == 0)
            {
                _previewText.text = "Select 1–5 ingredients…";
                return;
            }
            var preview = Round.PreviewScore(_selected);
            if (preview.Recipe == null)
                _previewText.text = $"{_selected.Count} selected — no recipe (scores 0)";
            else if (preview.IsVoided)
                _previewText.text = $"{preview.Recipe.Name} — VOIDED: {preview.VoidReason}";
            else
                _previewText.text = $"{preview.Recipe.Name} (Lv{preview.RecipeLevel}) — {preview.TotalFlavor:0.#} × {preview.TotalMult:0.#} = {preview.FinalScore:0.#}";
        }

        private void RenderRail()
        {
            ClearChildren(_railPanel);
            if (Run.Phase != RunPhase.CustomerRound) return;

            foreach (var card in Round.Rail)
            {
                var captured = card;
                bool selected = _selected.Contains(card);
                bool debuffed = Round.VipRules.DebuffedTypes.Contains(card.Type);
                var baseColor = TypeColors[card.Type];
                if (debuffed) baseColor = Color.Lerp(baseColor, Color.black, 0.6f);
                var color = selected ? Color.Lerp(baseColor, Color.white, 0.55f) : baseColor;
                string extras = string.Empty;
                if (card.Enhancement != Enhancement.None) extras += $"\n<{card.Enhancement}>";
                if (debuffed) extras += "\nDEBUFFED";
                string label = $"{(selected ? "✔ " : string.Empty)}{card.Flavor}\n{card.Type}\n{card.Name}{extras}";
                NewButton($"Card_{card.InstanceId}", _railPanel, label, color, () => ToggleCard(captured), 13);
            }
        }

        private void RenderPatrons()
        {
            ClearChildren(_patronPanel);
            foreach (var patron in Run.Patrons)
            {
                var captured = patron;
                int refund = (patron.Definition.Cost + 1) / 2;
                string stored = patron.Accumulated != 0 ? $" [{patron.Accumulated:0.#}]" : string.Empty;
                var button = NewButton($"Patron_{patron.Definition.Id}", _patronPanel,
                    $"{patron.Definition.Name}{stored} — sell ${refund}",
                    new Color(0.62f, 0.58f, 0.78f), () => OnSellPatronClicked(captured), 12);
                SetRowHeight(button, 26);
            }
        }

        private void RenderTools()
        {
            ClearChildren(_toolPanel);
            bool inRound = Run.Phase == RunPhase.CustomerRound;
            foreach (var tool in Run.ToolInventory)
            {
                var captured = tool;
                var button = NewButton($"Tool_{tool.Id}", _toolPanel,
                    $"{tool.Name} → use on selection (max {tool.MaxTargets})",
                    new Color(0.52f, 0.74f, 0.70f), () => OnUseToolClicked(captured), 12);
                SetRowHeight(button, 26);
                button.interactable = inRound && _selected.Count >= 1 && _selected.Count <= tool.MaxTargets;
            }
        }

        private void RenderShop()
        {
            bool open = Run.Phase == RunPhase.BackRoom;
            _shopPanel.gameObject.SetActive(open);
            if (!open) return;

            _shopTitle.text = $"BACK ROOM — wallet ${Run.Money}";
            ClearChildren(_shopOffersPanel);

            // An opened pack takes over the whole panel until it's resolved.
            if (Run.OpenPack != null)
            {
                var packTitle = NewText("PackTitle", _shopOffersPanel, 14, TextAnchor.MiddleCenter,
                    new Color(1f, 0.85f, 0.55f));
                packTitle.text = $"{PackCatalog.NameOf(Run.OpenPack.Kind)} — pick one:";
                var titleElement = packTitle.gameObject.AddComponent<LayoutElement>();
                titleElement.preferredHeight = 26;
                titleElement.flexibleWidth = 1;

                var packOptions = Run.OpenPack.Options;
                for (int i = 0; i < packOptions.Count; i++)
                {
                    int optionIndex = i;
                    var optionButton = NewButton($"PackOption_{i}", _shopOffersPanel,
                        packOptions[i].DisplayName, new Color(0.85f, 0.75f, 0.50f),
                        () => OnPickPackOptionClicked(optionIndex), 13);
                    SetRowHeight(optionButton, 30);
                }

                var skip = NewButton("SkipPack", _shopOffersPanel, "Skip pack",
                    new Color(0.55f, 0.45f, 0.45f), OnSkipPackClicked, 13);
                SetRowHeight(skip, 28);
                return;
            }

            var offers = Run.Shop.Offers;
            for (int i = 0; i < offers.Count; i++)
            {
                int index = i;
                var offer = offers[i];
                string label = offer.Sold
                    ? $"{offer.DisplayName} — SOLD"
                    : $"Buy {offer.DisplayName} — ${offer.Price}";
                var button = NewButton($"Offer_{i}", _shopOffersPanel, label,
                    new Color(0.80f, 0.68f, 0.42f), () => OnBuyOfferClicked(index), 13);
                SetRowHeight(button, 30);
                button.interactable = !offer.Sold && Run.Money >= offer.Price;
            }

            var voucherOffer = Run.Shop.VoucherOffer;
            if (voucherOffer != null)
            {
                string label = voucherOffer.Sold
                    ? $"{voucherOffer.DisplayName} — SOLD"
                    : $"Buy {voucherOffer.DisplayName} — ${voucherOffer.Price}\n<size=10>{voucherOffer.Voucher.Description}</size>";
                var voucherButton = NewButton("VoucherOffer", _shopOffersPanel, label,
                    new Color(0.72f, 0.52f, 0.78f), OnBuyVoucherClicked, 12);
                SetRowHeight(voucherButton, 40);
                voucherButton.interactable = !voucherOffer.Sold && Run.Money >= voucherOffer.Price;
            }

            var packs = Run.Shop.PackOffers;
            for (int i = 0; i < packs.Count; i++)
            {
                int packIndex = i;
                var pack = packs[i];
                string packLabel = pack.Sold
                    ? $"{pack.DisplayName} — SOLD"
                    : $"Open {pack.DisplayName} — ${pack.Price}";
                var packButton = NewButton($"Pack_{i}", _shopOffersPanel, packLabel,
                    new Color(0.55f, 0.70f, 0.85f), () => OnBuyPackClicked(packIndex), 13);
                SetRowHeight(packButton, 30);
                packButton.interactable = !pack.Sold && Run.Money >= pack.Price;
            }

            var reroll = NewButton("Reroll", _shopOffersPanel,
                $"Reroll — ${Run.Shop.RerollCost}", new Color(0.62f, 0.62f, 0.82f), OnRerollClicked, 13);
            SetRowHeight(reroll, 30);
            reroll.interactable = Run.Money >= Run.Shop.RerollCost;

            var next = NewButton("Continue", _shopOffersPanel,
                "Next customer →", new Color(0.30f, 0.55f, 0.30f), OnContinueClicked, 13);
            SetRowHeight(next, 30);
        }

        // ─────────────────────────────── recipe book ───────────────────────────────

        /// <summary>
        /// The casual-player teaching surface (GDD 10.2): every recipe as a color-coded
        /// Type pattern — no cocktail knowledge needed, just match the dots.
        /// </summary>
        private void RenderRecipeBook()
        {
            _recipePanel.gameObject.SetActive(_recipesVisible);
            if (!_recipesVisible) return;

            var lines = new List<string>
            {
                "<b>Legend:</b>  " + string.Join("  ",
                    TypeColors.Keys.Select(t => $"{TypeDot(t)} {t}")),
                ""
            };

            foreach (var recipe in Run.Recipes.OrderBy(r => r.Rank))
            {
                int level = Run.RecipeLevelOf(recipe.Id);
                lines.Add($"{PatternDots(recipe)}  <b>{recipe.Name}</b> (Lv{level})  " +
                          $"{recipe.FlavorAtLevel(level)} × {recipe.MultAtLevel(level)}{ConstraintText(recipe)}");
            }

            lines.Add("");
            lines.Add("Pick any 1–5 cards — the bar auto-detects the best recipe.");
            lines.Add("Cards outside the pattern still get played but add no Flavor.");
            _recipeText.text = string.Join("\n", lines);
        }

        private string TypeDot(IngredientType type) =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(TypeColors[type])}>●</color>";

        private string PatternDots(RecipeDefinition recipe)
        {
            var parts = new List<string>();
            foreach (var req in recipe.Requirements)
                for (int i = 0; i < req.Count; i++)
                    parts.Add(string.Join("/", req.Types.Select(TypeDot)));
            return string.Join(" ", parts);
        }

        private static string ConstraintText(RecipeDefinition recipe)
        {
            if (recipe.AllEqualFlavor) return "  — 5 different types, all the same number";
            if (recipe.AllDistinctTypes) return "  — exactly 5 cards, all different types";
            if (recipe.MinMixSize >= 5) return "  + any 5th card";
            if (recipe.ExactMixSize == 1) return "  — played alone";
            return string.Empty;
        }

        private void RenderBanner()
        {
            switch (Run.Phase)
            {
                case RunPhase.RunWon:
                    _bannerText.text = "★ OPENING WEEK SURVIVED";
                    _bannerText.color = new Color(0.55f, 0.95f, 0.55f);
                    break;
                case RunPhase.RunLost:
                    _bannerText.text = $"✖ LAST CALL — lost on Night {Run.Night}";
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

        private static void ClearChildren(RectTransform panel)
        {
            for (int i = panel.childCount - 1; i >= 0; i--)
                Destroy(panel.GetChild(i).gameObject);
        }

        private static void SetRowHeight(Button button, float height)
        {
            var element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.flexibleWidth = 1;
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

            // Top-left: run/round state.
            _infoText = NewText("Info", root, 15, TextAnchor.UpperLeft, Color.white);
            Stretch((RectTransform)_infoText.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -140), new Vector2(392, -12));

            // Left column: patron shelf, then tool belt.
            _patronPanel = NewSidePanel(root, "Patrons", -146, -300);
            _toolPanel = NewSidePanel(root, "Tools", -306, -400);

            // Top-right: seed + new run.
            _seedInput = NewInput("SeedInput", root, "LASTCALL-DEV");
            Place((RectTransform)_seedInput.transform, new Vector2(1, 1), new Vector2(200, 30), new Vector2(-140, -12));
            var newRun = NewButton("NewRun", root, "New Run", new Color(0.62f, 0.62f, 0.82f), OnNewRunClicked, 14);
            Place((RectTransform)newRun.transform, new Vector2(1, 1), new Vector2(120, 30), new Vector2(-12, -12));

            // Right: score log.
            var logPanel = NewRect("LogPanel", root);
            Stretch(logPanel, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-372, 170), new Vector2(-12, -52));
            logPanel.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);
            _logScroll = logPanel.gameObject.AddComponent<ScrollRect>();
            var viewport = NewRect("Viewport", logPanel);
            Stretch(viewport, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.sizeDelta = Vector2.zero;
            _logText = content.gameObject.AddComponent<Text>();
            ConfigureText(_logText, 13, TextAnchor.UpperLeft, new Color(0.85f, 0.85f, 0.8f));
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _logScroll.content = content;
            _logScroll.viewport = viewport;
            _logScroll.horizontal = false;
            _logScroll.movementType = ScrollRect.MovementType.Clamped;

            // Center: banner, shop overlay, live preview.
            _bannerText = NewText("Banner", root, 34, TextAnchor.MiddleCenter, Color.white);
            Place((RectTransform)_bannerText.transform, new Vector2(0.5f, 0.5f), new Vector2(900, 60), new Vector2(0, 150));

            _shopPanel = NewRect("ShopPanel", root);
            Place(_shopPanel, new Vector2(0.5f, 0.5f), new Vector2(460, 300), new Vector2(-60, 0));
            _shopPanel.gameObject.AddComponent<Image>().color = new Color(0.10f, 0.08f, 0.14f, 0.95f);
            _shopTitle = NewText("ShopTitle", _shopPanel, 18, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.6f));
            Stretch((RectTransform)_shopTitle.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -40), new Vector2(-8, -6));
            _shopOffersPanel = NewRect("ShopOffers", _shopPanel);
            Stretch(_shopOffersPanel, Vector2.zero, Vector2.one, new Vector2(12, 12), new Vector2(-12, -46));
            var shopLayout = _shopOffersPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            shopLayout.spacing = 6;
            shopLayout.childForceExpandHeight = false;
            shopLayout.childControlHeight = true;
            shopLayout.childControlWidth = true;
            _shopPanel.gameObject.SetActive(false);

            // Recipe Book overlay (created after the shop so it draws on top).
            var recipesButton = NewButton("RecipesToggle", root, "RECIPES",
                new Color(0.80f, 0.68f, 0.42f), OnToggleRecipesClicked, 14);
            Place((RectTransform)recipesButton.transform, new Vector2(0.5f, 1), new Vector2(120, 30), new Vector2(0, -12));

            _recipePanel = NewRect("RecipePanel", root);
            Place(_recipePanel, new Vector2(0.5f, 0.5f), new Vector2(660, 440), new Vector2(-60, 10));
            _recipePanel.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.06f, 0.12f, 0.97f);
            var recipeTitle = NewText("RecipeTitle", _recipePanel, 18, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.6f));
            recipeTitle.text = "RECIPE BOOK";
            Stretch((RectTransform)recipeTitle.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(8, -38), new Vector2(-8, -6));
            _recipeText = NewText("RecipeText", _recipePanel, 15, TextAnchor.UpperLeft, new Color(0.92f, 0.92f, 0.88f));
            Stretch((RectTransform)_recipeText.transform, Vector2.zero, Vector2.one, new Vector2(16, 10), new Vector2(-16, -44));
            _recipePanel.gameObject.SetActive(false);

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

        private RectTransform NewSidePanel(RectTransform root, string name, float top, float bottom)
        {
            var panel = NewRect(name, root);
            Stretch(panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, bottom), new Vector2(392, top));
            panel.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.30f);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            return panel;
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
