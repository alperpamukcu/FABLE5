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

        // Theme fonts (GDD 9 art direction): Limelight for marquee headers, Barlow for
        // body/UI. Wired by DebugSceneCreator; missing references fall back to the
        // built-in LegacyRuntime font so the HUD never breaks.
        [SerializeField] private Font displayFont;
        [SerializeField] private Font bodyFont;
        [SerializeField] private Font pixelFont;   // v2 pixel font for buttons/controls (Silkscreen)

        // Cozy noir UI kit (LastCall/Generate UI Sprites): rounded white sprites tinted
        // at runtime, a screen vignette and the animated smoke backdrop. All optional.
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite buttonSprite;
        [SerializeField] private Sprite vignetteSprite;
        [SerializeField] private Material backgroundMaterial;

        // Generated illustration registry (LastCall/Build Art Library); optional — cards
        // and portraits fall back to flat tints when a sprite is missing.
        [SerializeField] private ArtLibrary art;

        // The diegetic night-club stage behind this overlay (v2). When present, the rail
        // is rendered as bottles on the counter and the smoke backdrop is suppressed.
        [SerializeField] private DiegeticStage stage;

        // Art bible §2 locked palette (Docs/GDD/14_art_bible.md) — every UI tint
        // must come from these tokens so the whole screen shares one language.
        private static readonly Color DeepPlum = Hex(0x1A1023);
        private static readonly Color PanelPlum = Hex(0x241830);
        private static readonly Color Amber = Hex(0xE8A33D);
        private static readonly Color CandleGlow = Hex(0xF5C97B);
        private static readonly Color WoodBrown = Hex(0x6B4226);
        private static readonly Color TealShadow = Hex(0x1E4D4A);
        private static readonly Color Cream = Hex(0xF2E8D5);
        private static readonly Color NeonMagenta = Hex(0xD94D8F);

        private static Color Hex(int rgb) => new Color(
            ((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private GameBootstrap _bootstrap;
        private readonly List<IngredientCard> _selected = new List<IngredientCard>();
        private Font _font;
        private Font _headerFont;
        private Font _pixelFont;
        private bool _uiBuilt;

        private Text _infoText;
        private Text _previewText;
        private Text _bannerText;
        private Text _logText;
        private ScrollRect _logScroll;
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
        private Button _skipButton;
        private Button _bouncerButton;
        private InputField _seedInput;
        private RectTransform _customerCard;
        private Image _customerPortrait;
        private Text _customerCaption;
        private RectTransform _actionBar;
        private RectTransform _scrim;
        private GameObject _logPanel;
        private DiegeticStage.Exit _pendingExit = DiegeticStage.Exit.Refresh;

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
            var fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _font = bodyFont != null ? bodyFont : fallback;
            _headerFont = displayFont != null ? displayFont : _font;
            _pixelFont = pixelFont != null ? pixelFont : _font;
            BuildUi();
            _uiBuilt = true;
            if (_logPanel != null) _logPanel.SetActive(false); // debug log off in the game view
            if (Run != null) OnRunStarted();
        }

        private void Update()
        {
            // F1 toggles the debug score log (kept off the game view by default).
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame && _logPanel != null)
                _logPanel.SetActive(!_logPanel.activeSelf);
        }

        private void OnRunStarted()
        {
            if (!_uiBuilt) return;
            _selected.Clear();
            _pendingExit = DiegeticStage.Exit.Refresh;
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
            _pendingExit = DiegeticStage.Exit.Mix; // mixed bottles pop forward-up then slide out
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
                          $"{(tips.GoldenBonus > 0 ? $" + golden {tips.GoldenBonus}" : "")}" +
                          $"{(tips.FavorBonus > 0 ? $" + favor {tips.FavorBonus}" : "")} = ${tips.Total} (wallet ${Run.Money})");
            }

            if (Run.Phase == RunPhase.RunWon) AppendLog("★ OPENING WEEK SURVIVED — run won!");
            if (Run.Phase == RunPhase.RunLost) AppendLog("✖ LAST CALL — order failed, run over.");
            RenderAll();
        }

        private void OnRestockClicked()
        {
            int count = _selected.Count;
            _pendingExit = DiegeticStage.Exit.Restock; // restocked bottles slide left out
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

        private void OnSkipCustomerAClicked()
        {
            Guarded(() =>
            {
                Run.SkipCustomerA();
                AppendLog($"— {Run.LastFavorText}");
                AppendLog(CustomerHeader());
            });
        }

        private void OnBouncerClicked()
        {
            Guarded(() =>
            {
                Run.RerollTonightsVip();
                AppendLog($"— Bouncer: tonight's VIP is now {Run.TonightsVip.Name}");
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
                _pendingExit = DiegeticStage.Exit.Refresh; // whole counter refreshes in one wave
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
            // GDD 5.5: the Night's VIP is public knowledge from Customer A on.
            string tonightLine = Run.TonightsVip != null && Run.Slot != CustomerSlot.Vip
                ? $"\n<color=#C9A0E8>Tonight's VIP: {Run.TonightsVip.Name}</color>"
                : string.Empty;
            string tagsLine = Run.FavorTags.Count > 0
                ? $"\n<color=#8FD4A8>Tags: {string.Join(", ", Run.FavorTags)}</color>"
                : string.Empty;
            _infoText.text =
                $"Night {Run.Night}/{Run.Config.Nights} — {Run.Slot}\n" +
                $"Wallet:   ${Run.Money}\n" +
                $"Target:   {Round.Customer.TargetScore:0.#}\n" +
                $"Score:    {Round.AccumulatedScore:0.#}\n" +
                $"Mixes:    {Round.MixesRemaining}   Restocks: {Round.RestocksRemaining}\n" +
                $"Cabinet:  {Round.DeckDrawCount} draw / {Round.DeckDiscardCount} discard" +
                vipLine + tonightLine + tagsLine;

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
            _skipButton.gameObject.SetActive(inRound && Run.CanSkipCustomerA);
            _bouncerButton.gameObject.SetActive(inRound && Run.CanRerollTonightsVip);

            // Only the current context's controls are on screen — the action bar during a
            // round, the Back Room modal (over the scrim) between customers. The diegetic
            // rail is gated inside RenderRail.
            bool recipesOpen = _recipesVisible;
            _actionBar.gameObject.SetActive(inRound && !recipesOpen);
            _scrim.gameObject.SetActive(Run.Phase == RunPhase.BackRoom || recipesOpen);
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
            // The diegetic stage renders the rail as bottles on the counter (v2). The old
            // UI-card rail is retired; when no stage is wired we simply show nothing.
            if (stage == null) return;
            bool inRound = Run.Phase == RunPhase.CustomerRound;
            stage.SetRailVisible(inRound);
            if (!inRound) return;
            stage.SetBottles(Round.Rail, _selected, Round.VipRules.DebuffedTypes, ToggleCard, _pendingExit);
            _pendingExit = DiegeticStage.Exit.None; // consumed; selection re-renders don't re-wave
        }

        /// <summary>A small caps header + optional empty hint at the top of a shelf.</summary>
        private void ShelfHeader(RectTransform panel, string title, bool empty)
        {
            var header = NewText($"{title}_Header", panel, 13, TextAnchor.MiddleLeft, CandleGlow);
            header.font = _headerFont;
            header.text = title;
            var hl = header.gameObject.AddComponent<LayoutElement>();
            hl.preferredHeight = 20; hl.flexibleWidth = 1;
            if (empty)
            {
                var hint = NewText($"{title}_Empty", panel, 12, TextAnchor.MiddleLeft,
                    new Color(0.7f, 0.66f, 0.6f, 0.6f));
                hint.text = "— none yet —";
                var el = hint.gameObject.AddComponent<LayoutElement>();
                el.preferredHeight = 18; el.flexibleWidth = 1;
            }
        }

        private void RenderPatrons()
        {
            ClearChildren(_patronPanel);
            ShelfHeader(_patronPanel, "PATRONS", Run.Patrons.Count == 0);
            foreach (var patron in Run.Patrons)
            {
                var captured = patron;
                int refund = (patron.Definition.Cost + 1) / 2;
                string stored = patron.Accumulated != 0 ? $" [{patron.Accumulated:0.#}]" : string.Empty;

                // Row: portrait thumbnail + name/sell button.
                var row = NewRect($"PatronRow_{patron.Definition.Id}", _patronPanel);
                var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                rowLayout.spacing = 4;
                rowLayout.childControlWidth = true;
                rowLayout.childControlHeight = true;
                rowLayout.childForceExpandWidth = false;
                SetRowHeight2(row, 40);

                var thumb = NewRect("Thumb", row);
                var thumbImg = thumb.gameObject.AddComponent<Image>();
                var portrait = art != null ? art.Portrait(patron.Definition.Id) : null;
                thumbImg.preserveAspect = true;
                if (portrait != null) thumbImg.sprite = portrait;
                else thumbImg.color = TealShadow;
                var thumbLayout = thumb.gameObject.AddComponent<LayoutElement>();
                thumbLayout.preferredWidth = 34;
                thumbLayout.preferredHeight = 38;

                var button = NewButton($"Patron_{patron.Definition.Id}", row,
                    $"{patron.Definition.Name}{stored}\nsell ${refund}",
                    PanelPlum, () => OnSellPatronClicked(captured), 11);
                var btnLayout = button.gameObject.AddComponent<LayoutElement>();
                btnLayout.flexibleWidth = 1;
            }
        }

        /// <summary>
        /// A fixed-height shop row: a button with an optional left-anchored thumbnail
        /// inside it. Avoids nested layout groups so every row is exactly its height.
        /// </summary>
        private Button ShopButton(string name, string label, Sprite thumb, Color bg,
            UnityAction onClick, float height, int fontSize)
        {
            var button = NewButton(name, _shopOffersPanel, label, bg, onClick, fontSize);
            SetRowHeight(button, height);
            if (thumb != null)
            {
                var t = NewRect("Thumb", (RectTransform)button.transform);
                Place(t, new Vector2(0, 0.5f), new Vector2(height - 8, height - 8), new Vector2(6, 0));
                var img = t.gameObject.AddComponent<Image>();
                img.sprite = thumb;
                img.preserveAspect = true;
                img.raycastTarget = false;
            }
            return button;
        }

        /// <summary>The catalogue thumbnail for a shop offer, by kind.</summary>
        private Sprite SpriteForOffer(ShopOffer offer)
        {
            if (art == null) return null;
            switch (offer.Kind)
            {
                case ShopOfferKind.Patron: return art.Portrait(offer.Patron.Id);
                case ShopOfferKind.Tool: return art.Tool(offer.Tool.Id);
                case ShopOfferKind.Book: return art.Icon("book_recipe");
                default: return null;
            }
        }

        /// <summary>Layout-element row height for a RectTransform (SetRowHeight targets Button).</summary>
        private static void SetRowHeight2(RectTransform rt, float height)
        {
            var element = rt.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.flexibleWidth = 1;
        }

        private void RenderTools()
        {
            ClearChildren(_toolPanel);
            ShelfHeader(_toolPanel, "TOOLS", Run.ToolInventory.Count == 0);
            bool inRound = Run.Phase == RunPhase.CustomerRound;
            foreach (var tool in Run.ToolInventory)
            {
                var captured = tool;
                var row = NewRect($"ToolRow_{tool.Id}", _toolPanel);
                var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                rowLayout.spacing = 4;
                rowLayout.childControlWidth = true;
                rowLayout.childControlHeight = true;
                rowLayout.childForceExpandWidth = false;
                SetRowHeight2(row, 38);

                var thumb = NewRect("Thumb", row);
                var thumbImg = thumb.gameObject.AddComponent<Image>();
                thumbImg.preserveAspect = true;
                var sprite = art != null ? art.Tool(tool.Id) : null;
                if (sprite != null) thumbImg.sprite = sprite; else thumbImg.color = TealShadow;
                var thumbLayout = thumb.gameObject.AddComponent<LayoutElement>();
                thumbLayout.preferredWidth = 34; thumbLayout.preferredHeight = 34;

                var button = NewButton($"Tool_{tool.Id}", row,
                    $"{tool.Name}\nuse (max {tool.MaxTargets})",
                    PanelPlum, () => OnUseToolClicked(captured), 11);
                var btnLayout = button.gameObject.AddComponent<LayoutElement>();
                btnLayout.flexibleWidth = 1;
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

                var button = ShopButton($"Offer_{i}", label, SpriteForOffer(offer),
                    Amber, () => OnBuyOfferClicked(index), 46, 13);
                button.interactable = !offer.Sold && Run.Money >= offer.Price;
            }

            var voucherOffer = Run.Shop.VoucherOffer;
            if (voucherOffer != null)
            {
                string label = voucherOffer.Sold
                    ? $"{voucherOffer.DisplayName} — SOLD"
                    : $"Buy {voucherOffer.DisplayName} — ${voucherOffer.Price}\n<size=10>{voucherOffer.Voucher.Description}</size>";
                var voucherButton = ShopButton("VoucherOffer", label,
                    art != null ? art.Icon("book_recipe") : null,
                    new Color(0.46f, 0.34f, 0.52f), OnBuyVoucherClicked, 46, 12);
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
                var packButton = ShopButton($"Pack_{i}", packLabel, null,
                    new Color(0.32f, 0.46f, 0.52f), () => OnBuyPackClicked(packIndex), 38, 13);
                packButton.interactable = !pack.Sold && Run.Money >= pack.Price;
            }

            // Footer: a thin divider, then reroll (subtle) and continue (primary).
            var spacer = NewRect("Spacer", _shopOffersPanel);
            var spacerLayout = spacer.gameObject.AddComponent<LayoutElement>();
            spacerLayout.preferredHeight = 6; spacerLayout.flexibleWidth = 1;

            var reroll = NewButton("Reroll", _shopOffersPanel,
                $"Reroll — ${Run.Shop.RerollCost}", new Color(0.26f, 0.20f, 0.34f), OnRerollClicked, 13);
            SetRowHeight(reroll, 34);
            reroll.interactable = Run.Money >= Run.Shop.RerollCost;

            var next = NewButton("Continue", _shopOffersPanel,
                "Next customer →", Amber, OnContinueClicked, 15);
            SetRowHeight(next, 42);
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
            if (recipe.EqualFlavorGroupSize > 0)
                return $"  — any {recipe.EqualFlavorGroupSize} cards with the SAME number";
            if (recipe.AscendingFlavorGroupSize > 0)
                return $"  — any {recipe.AscendingFlavorGroupSize} cards, all DIFFERENT numbers";
            if (recipe.SameTypeGroupMin > 0)
                return $"  — {recipe.SameTypeGroupMin}+ cards of one color";
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
            RenderCustomer();
            if (stage != null && Run != null) stage.SetMoney($"${Run.Money}");
        }

        /// <summary>Shows the current customer's face: the VIP under the light on a VIP
        /// round, or tonight's revealed VIP as a preview during the warm-up customers.</summary>
        private void RenderCustomer()
        {
            Sprite face = null;
            string caption = null;
            if (art != null && Run.Phase == RunPhase.CustomerRound)
            {
                if (Run.Slot == CustomerSlot.Vip && Run.CurrentVip != null)
                {
                    face = art.Vip(Run.CurrentVip.Id);
                    caption = $"<color=#D94D8F>VIP</color>\n{Run.CurrentVip.Name}";
                }
                else if (Run.TonightsVip != null)
                {
                    face = art.Vip(Run.TonightsVip.Id);
                    caption = $"tonight's VIP\n{Run.TonightsVip.Name}";
                }
            }

            // With the diegetic stage present, the VIP is the in-scene pixel sprite leaning
            // on the bar — suppress the legacy painterly portrait card.
            bool show = face != null && stage == null;
            _customerCard.gameObject.SetActive(show);
            if (show)
            {
                _customerPortrait.sprite = face;
                _customerCaption.text = caption;
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

            // Legacy cozy-noir backdrop. Suppressed when the diegetic stage is present
            // (the stage's night-club BackgroundLayers replace it in v2).
            if (backgroundMaterial != null && stage == null)
            {
                var bg = NewRect("Background", root);
                Stretch(bg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var raw = bg.gameObject.AddComponent<RawImage>();
                raw.material = backgroundMaterial;
                raw.raycastTarget = false;
            }

            // Top-left: run/round state on a framed panel (v2 professional HUD).
            var infoPanel = NewRect("InfoPanel", root);
            Stretch(infoPanel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -150), new Vector2(404, -8));
            StylePanel(infoPanel, WithAlpha(DeepPlum, 0.82f));
            _infoText = NewText("Info", infoPanel, 15, TextAnchor.UpperLeft, Cream);
            Stretch((RectTransform)_infoText.transform, Vector2.zero, Vector2.one, new Vector2(12, 10), new Vector2(-10, -10));

            // Left column: patron shelf, then tool belt.
            _patronPanel = NewSidePanel(root, "Patrons", -146, -300);
            _toolPanel = NewSidePanel(root, "Tools", -306, -400);

            // Top-right: seed + new run.
            _seedInput = NewInput("SeedInput", root, "LASTCALL-DEV");
            Place((RectTransform)_seedInput.transform, new Vector2(1, 1), new Vector2(200, 30), new Vector2(-140, -12));
            var newRun = NewButton("NewRun", root, "New Run", new Color(0.62f, 0.62f, 0.82f), OnNewRunClicked, 14);
            Place((RectTransform)newRun.transform, new Vector2(1, 1), new Vector2(120, 30), new Vector2(-12, -12));

            // Right: score log — hidden by default in the game view, toggled with F1.
            var logPanel = NewRect("LogPanel", root);
            _logPanel = logPanel.gameObject;
            Stretch(logPanel, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-372, 170), new Vector2(-12, -52));
            StylePanel(logPanel, WithAlpha(DeepPlum, 0.80f));
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

            // Recipes toggle: top centre, under the marquee.
            var recipesButton = NewButton("RecipesToggle", root, "RECIPES",
                WithAlpha(PanelPlum, 0.95f), OnToggleRecipesClicked, 13);
            Place((RectTransform)recipesButton.transform, new Vector2(0.5f, 1), new Vector2(128, 32), new Vector2(0, -12));

            // Customer portrait card: the VIP across the bar, upper centre so it sits
            // above the bottle rail (which owns the bottom band).
            _customerCard = NewRect("CustomerCard", root);
            Place(_customerCard, new Vector2(0.5f, 1f), new Vector2(200, 260), new Vector2(0, -40));
            StylePanel(_customerCard, WithAlpha(DeepPlum, 0.55f));
            var portraitRt = NewRect("Portrait", _customerCard);
            Stretch(portraitRt, new Vector2(0, 0.16f), new Vector2(1, 1), new Vector2(10, 6), new Vector2(-10, -10));
            _customerPortrait = portraitRt.gameObject.AddComponent<Image>();
            _customerPortrait.preserveAspect = true;
            _customerCaption = NewText("Caption", _customerCard, 16, TextAnchor.UpperCenter, Cream);
            _customerCaption.font = _headerFont;
            Stretch((RectTransform)_customerCaption.transform, Vector2.zero, new Vector2(1, 0.16f), new Vector2(4, 4), new Vector2(-4, -2));
            _customerCard.gameObject.SetActive(false);

            // The rail is now diegetic (bottles on the counter, DiegeticStage). No UI rail.

            // Action bar: a right-side vertical stack (the log is F1-hidden, so the right
            // is free) plus the live preview line just above the bottle rail. Grouped so
            // one toggle hides the cluster when the Back Room modal takes the screen.
            _actionBar = NewRect("ActionBar", root);
            Stretch(_actionBar, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Preview line sits in the gap between the customer (upper centre) and the
            // bottle rail (bottom band).
            _previewText = NewText("Preview", _actionBar, 20, TextAnchor.MiddleCenter, CandleGlow);
            var previewRt = (RectTransform)_previewText.transform;
            previewRt.anchorMin = previewRt.anchorMax = previewRt.pivot = new Vector2(0.5f, 0);
            previewRt.sizeDelta = new Vector2(680, 34);
            previewRt.anchoredPosition = new Vector2(0, 440);  // in the gap above the rail + value chips

            // Action buttons: a top-right vertical stack, clear of the bottle rail below.
            _mixButton = NewButton("Mix", _actionBar, "MIX", Amber, OnMixClicked, 18);
            var mixRt = (RectTransform)_mixButton.transform;
            mixRt.anchorMin = mixRt.anchorMax = mixRt.pivot = new Vector2(1, 1);
            mixRt.sizeDelta = new Vector2(196, 48);
            mixRt.anchoredPosition = new Vector2(-24, -56);
            _restockButton = NewButton("Restock", _actionBar, "RESTOCK", WithAlpha(WoodBrown, 0.95f), OnRestockClicked, 18);
            var restockRt = (RectTransform)_restockButton.transform;
            restockRt.anchorMin = restockRt.anchorMax = restockRt.pivot = new Vector2(1, 1);
            restockRt.sizeDelta = new Vector2(196, 48);
            restockRt.anchoredPosition = new Vector2(-24, -110);

            // Only shows on an untouched Customer A round (GDD 5.2).
            _skipButton = NewButton("SkipA", _actionBar, "SKIP → FAVOR", WithAlpha(TealShadow, 0.95f), OnSkipCustomerAClicked, 13);
            var skipRt = (RectTransform)_skipButton.transform;
            skipRt.anchorMin = skipRt.anchorMax = skipRt.pivot = new Vector2(1, 1);
            skipRt.sizeDelta = new Vector2(196, 42);
            skipRt.anchoredPosition = new Vector2(-24, -164);

            // Bouncer voucher: visible only while tonight's VIP can still be rerolled.
            _bouncerButton = NewButton("Bouncer", _actionBar, "BOUNCER: NEW VIP", NeonMagenta, OnBouncerClicked, 12);
            var bouncerRt = (RectTransform)_bouncerButton.transform;
            bouncerRt.anchorMin = bouncerRt.anchorMax = bouncerRt.pivot = new Vector2(1, 1);
            bouncerRt.sizeDelta = new Vector2(196, 36);
            bouncerRt.anchoredPosition = new Vector2(-24, -212);

            // Modal scrim: dims and blocks the game behind shop / recipe overlays.
            _scrim = NewRect("Scrim", root);
            Stretch(_scrim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _scrim.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.01f, 0.04f, 0.62f);
            _scrim.gameObject.SetActive(false);

            // Back Room modal (drawn above the scrim).
            _shopPanel = NewRect("ShopPanel", root);
            Place(_shopPanel, new Vector2(0.5f, 0.5f), new Vector2(520, 470), new Vector2(0, 0));
            StylePanel(_shopPanel, PanelPlum);
            _shopTitle = NewText("ShopTitle", _shopPanel, 20, TextAnchor.MiddleCenter, CandleGlow);
            _shopTitle.font = _headerFont;
            Stretch((RectTransform)_shopTitle.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -46), new Vector2(-12, -10));
            _shopOffersPanel = NewRect("ShopOffers", _shopPanel);
            Stretch(_shopOffersPanel, Vector2.zero, Vector2.one, new Vector2(16, 16), new Vector2(-16, -52));
            var shopLayout = _shopOffersPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            shopLayout.spacing = 8;
            shopLayout.childForceExpandHeight = false;
            shopLayout.childControlHeight = true;
            shopLayout.childControlWidth = true;
            _shopPanel.gameObject.SetActive(false);

            // Recipe Book modal (drawn above the scrim).
            _recipePanel = NewRect("RecipePanel", root);
            Place(_recipePanel, new Vector2(0.5f, 0.5f), new Vector2(700, 520), new Vector2(0, 0));
            StylePanel(_recipePanel, PanelPlum);
            var recipeTitle = NewText("RecipeTitle", _recipePanel, 20, TextAnchor.MiddleCenter, CandleGlow);
            recipeTitle.font = _headerFont;
            recipeTitle.text = "RECIPE BOOK";
            Stretch((RectTransform)recipeTitle.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -44), new Vector2(-12, -10));
            _recipeText = NewText("RecipeText", _recipePanel, 15, TextAnchor.UpperLeft, Cream);
            Stretch((RectTransform)_recipeText.transform, Vector2.zero, Vector2.one, new Vector2(20, 12), new Vector2(-20, -50));
            _recipePanel.gameObject.SetActive(false);

            // Banner (win/lose marquee) above the modals.
            _bannerText = NewText("Banner", root, 40, TextAnchor.MiddleCenter, Color.white);
            _bannerText.font = _headerFont;
            Place((RectTransform)_bannerText.transform, new Vector2(0.5f, 0.5f), new Vector2(1000, 70), new Vector2(0, 150));

            // Soft darkness pooling at the screen edges, over everything, never clickable.
            if (vignetteSprite != null)
            {
                var vin = NewRect("Vignette", root);
                Stretch(vin, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var image = vin.gameObject.AddComponent<Image>();
                image.sprite = vignetteSprite;
                image.raycastTarget = false;
            }
        }

        /// <summary>Panel background: the rounded kit sprite when wired, a flat tint otherwise.</summary>
        private Image StylePanel(RectTransform rt, Color color)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            if (panelSprite != null)
            {
                image.sprite = panelSprite;
                image.type = Image.Type.Sliced;
            }
            return image;
        }

        private RectTransform NewSidePanel(RectTransform root, string name, float top, float bottom)
        {
            var panel = NewRect(name, root);
            Stretch(panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, bottom), new Vector2(392, top));
            StylePanel(panel, WithAlpha(PanelPlum, 0.72f));
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
            // v2 pixel button: a flat palette fill inside a 2px dark border (no rounded
            // procedural sprite), pixel font label. The border is the click/tint target.
            var rt = NewRect(name, parent);
            var border = rt.gameObject.AddComponent<Image>();
            border.color = DeepPlum;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = border;
            button.onClick.AddListener(onClick);

            var fillRt = NewRect("Fill", rt);
            Stretch(fillRt, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2));
            var fill = fillRt.gameObject.AddComponent<Image>();
            fill.color = bg;
            fill.raycastTarget = false;

            // Pixel feedback: brighten the border ring on hover, sink on press, dim when off.
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.35f, 1.30f, 1.15f);
            colors.pressedColor = new Color(0.75f, 0.72f, 0.80f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.5f, 0.7f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            var textRt = NewRect("Text", rt);
            Stretch(textRt, Vector2.zero, Vector2.one, new Vector2(8, 4), new Vector2(-8, -4));
            var text = textRt.gameObject.AddComponent<Text>();
            // Cream on dark, plum on light — never pure black/white.
            float luminance = bg.r * 0.299f + bg.g * 0.587f + bg.b * 0.114f;
            text.font = _pixelFont;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = luminance < 0.5f ? Cream : DeepPlum;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
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
