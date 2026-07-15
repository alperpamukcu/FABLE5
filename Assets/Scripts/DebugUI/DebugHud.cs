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

        // Cozy noir UI kit (LastCall/Generate UI Sprites): rounded white sprites tinted
        // at runtime, a screen vignette and the animated smoke backdrop. All optional.
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite buttonSprite;
        [SerializeField] private Sprite vignetteSprite;
        [SerializeField] private Material backgroundMaterial;

        // Generated illustration registry (LastCall/Build Art Library); optional — cards
        // and portraits fall back to flat tints when a sprite is missing.
        [SerializeField] private ArtLibrary art;

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
        private Button _skipButton;
        private Button _bouncerButton;
        private InputField _seedInput;
        private RectTransform _customerCard;
        private Image _customerPortrait;
        private Text _customerCaption;

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
            _skipButton.gameObject.SetActive(Run.CanSkipCustomerA);
            _bouncerButton.gameObject.SetActive(Run.CanRerollTonightsVip);
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
                BuildIngredientCard(_railPanel, card, _selected.Contains(card),
                    Round.VipRules.DebuffedTypes.Contains(card.Type), () => ToggleCard(captured));
            }
        }

        /// <summary>
        /// A real ingredient card per art bible §4: art window on top, a type-colour band,
        /// the Flavor value in a top-left circle, and the name on a bottom plate. Degrades
        /// to a tinted plate when the sprite is missing.
        /// </summary>
        private void BuildIngredientCard(Transform parent, IngredientCard card, bool selected,
            bool debuffed, UnityAction onClick)
        {
            var typeColor = TypeColors[card.Type];
            var root = NewRect($"Card_{card.InstanceId}", parent);

            // Card body: cream frame, tinted darker plum inside; the frame reads as one
            // object with the rounded kit sprite.
            var body = root.gameObject.AddComponent<Image>();
            body.color = selected ? CandleGlow : Cream;
            if (panelSprite != null) { body.sprite = panelSprite; body.type = Image.Type.Sliced; }
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = body;
            button.onClick.AddListener(onClick);

            // Art window (upper ~62%).
            var artRt = NewRect("Art", root);
            Stretch(artRt, new Vector2(0, 0.30f), new Vector2(1, 1), new Vector2(5, 2), new Vector2(-5, -5));
            var artImg = artRt.gameObject.AddComponent<Image>();
            artImg.preserveAspect = true;
            var sprite = art != null ? art.Ingredient(card.Id) : null;
            if (sprite != null) { artImg.sprite = sprite; artImg.color = debuffed ? new Color(0.4f, 0.4f, 0.45f) : Color.white; }
            else { artImg.color = debuffed ? Color.Lerp(typeColor, Color.black, 0.6f) : typeColor; }

            // Type colour band under the art.
            var band = NewRect("Band", root);
            Stretch(band, new Vector2(0, 0.26f), new Vector2(1, 0.30f), new Vector2(5, 0), new Vector2(-5, 0));
            band.gameObject.AddComponent<Image>().color = debuffed ? Color.Lerp(typeColor, Color.black, 0.5f) : typeColor;

            // Value circle, top-left.
            var circle = NewRect("Value", root);
            Place(circle, new Vector2(0, 1), new Vector2(28, 28), new Vector2(4, -4));
            var circleImg = circle.gameObject.AddComponent<Image>();
            circleImg.color = DeepPlum;
            var valueText = NewText("V", circle, 15, TextAnchor.MiddleCenter, CandleGlow);
            Stretch((RectTransform)valueText.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            valueText.text = card.Flavor.ToString();
            valueText.font = _headerFont;

            // Name plate (bottom).
            var name = NewText("Name", root, 11, TextAnchor.LowerCenter, PanelPlum);
            Stretch((RectTransform)name.transform, Vector2.zero, new Vector2(1, 0.28f), new Vector2(3, 3), new Vector2(-3, 0));
            string tag = card.Enhancement != Enhancement.None ? $"\n<color=#E8A33D>{card.Enhancement}</color>" : string.Empty;
            if (debuffed) tag += "\n<color=#D94D8F>DEBUFF</color>";
            name.text = card.Name + tag;

            // Selection ring.
            if (selected)
            {
                var ring = NewRect("Ring", root);
                Stretch(ring, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var ringImg = ring.gameObject.AddComponent<Image>();
                if (panelSprite != null) { ringImg.sprite = panelSprite; ringImg.type = Image.Type.Sliced; }
                ringImg.color = new Color(1f, 0.85f, 0.35f, 0.35f);
                ringImg.raycastTarget = false;
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

                var row = NewRect($"OfferRow_{i}", _shopOffersPanel);
                var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                rowLayout.spacing = 6;
                rowLayout.childControlWidth = true;
                rowLayout.childControlHeight = true;
                rowLayout.childForceExpandWidth = false;
                SetRowHeight2(row, 40);

                var thumb = NewRect("Thumb", row);
                var thumbImg = thumb.gameObject.AddComponent<Image>();
                thumbImg.preserveAspect = true;
                var sprite = SpriteForOffer(offer);
                if (sprite != null) thumbImg.sprite = sprite; else thumbImg.color = TealShadow;
                var thumbLayout = thumb.gameObject.AddComponent<LayoutElement>();
                thumbLayout.preferredWidth = 36; thumbLayout.preferredHeight = 36;

                var button = NewButton($"Offer_{i}", row, label,
                    Amber, () => OnBuyOfferClicked(index), 13);
                var btnLayout = button.gameObject.AddComponent<LayoutElement>();
                btnLayout.flexibleWidth = 1;
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
                $"Reroll — ${Run.Shop.RerollCost}", TealShadow, OnRerollClicked, 13);
            SetRowHeight(reroll, 30);
            reroll.interactable = Run.Money >= Run.Shop.RerollCost;

            var next = NewButton("Continue", _shopOffersPanel,
                "Next customer →", CandleGlow, OnContinueClicked, 13);
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

            bool show = face != null;
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

            // Cozy noir backdrop (GDD 12.1): the animated smoke swirl sits behind
            // everything; the whole kit degrades to flat colors when art is unwired.
            if (backgroundMaterial != null)
            {
                var bg = NewRect("Background", root);
                Stretch(bg, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var raw = bg.gameObject.AddComponent<RawImage>();
                raw.material = backgroundMaterial;
                raw.raycastTarget = false;
            }

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

            // Center: banner, shop overlay, live preview.
            _bannerText = NewText("Banner", root, 34, TextAnchor.MiddleCenter, Color.white);
            _bannerText.font = _headerFont; // marquee moments deserve the marquee font
            Place((RectTransform)_bannerText.transform, new Vector2(0.5f, 0.5f), new Vector2(900, 60), new Vector2(0, 150));

            // Customer portrait card: the VIP under the bar light, upper centre so it
            // clears the live preview text and the action buttons below.
            _customerCard = NewRect("CustomerCard", root);
            Place(_customerCard, new Vector2(0.5f, 1f), new Vector2(230, 300), new Vector2(-40, -128));
            StylePanel(_customerCard, WithAlpha(DeepPlum, 0.55f));
            var portraitRt = NewRect("Portrait", _customerCard);
            Stretch(portraitRt, new Vector2(0, 0.16f), new Vector2(1, 1), new Vector2(10, 6), new Vector2(-10, -10));
            _customerPortrait = portraitRt.gameObject.AddComponent<Image>();
            _customerPortrait.preserveAspect = true;
            _customerCaption = NewText("Caption", _customerCard, 16, TextAnchor.UpperCenter, Cream);
            _customerCaption.font = _headerFont;
            Stretch((RectTransform)_customerCaption.transform, Vector2.zero, new Vector2(1, 0.16f), new Vector2(4, 4), new Vector2(-4, -2));
            _customerCard.gameObject.SetActive(false);

            // Tall enough for the full GDD 7 layout: 2 card slots + voucher + 2 packs
            // + reroll + continue (~260px of rows) with headroom for SOLD relabels.
            _shopPanel = NewRect("ShopPanel", root);
            Place(_shopPanel, new Vector2(0.5f, 0.5f), new Vector2(460, 440), new Vector2(-60, 0));
            StylePanel(_shopPanel, WithAlpha(PanelPlum, 0.97f));
            _shopTitle = NewText("ShopTitle", _shopPanel, 18, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.6f));
            _shopTitle.font = _headerFont;
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
                Amber, OnToggleRecipesClicked, 14);
            Place((RectTransform)recipesButton.transform, new Vector2(0.5f, 1), new Vector2(120, 30), new Vector2(0, -12));

            _recipePanel = NewRect("RecipePanel", root);
            Place(_recipePanel, new Vector2(0.5f, 0.5f), new Vector2(660, 440), new Vector2(-60, 10));
            StylePanel(_recipePanel, WithAlpha(DeepPlum, 0.97f));
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
            _mixButton = NewButton("Mix", root, "MIX", Amber, OnMixClicked, 18);
            var mixRt = (RectTransform)_mixButton.transform;
            mixRt.anchorMin = mixRt.anchorMax = mixRt.pivot = new Vector2(0.5f, 0);
            mixRt.sizeDelta = new Vector2(170, 42);
            mixRt.anchoredPosition = new Vector2(-95, 168);
            _restockButton = NewButton("Restock", root, "RESTOCK", WoodBrown, OnRestockClicked, 18);
            var restockRt = (RectTransform)_restockButton.transform;
            restockRt.anchorMin = restockRt.anchorMax = restockRt.pivot = new Vector2(0.5f, 0);
            restockRt.sizeDelta = new Vector2(170, 42);
            restockRt.anchoredPosition = new Vector2(95, 168);

            // Only shows on an untouched Customer A round (GDD 5.2).
            _skipButton = NewButton("SkipA", root, "SKIP → FAVOR", new Color(0.60f, 0.50f, 0.75f), OnSkipCustomerAClicked, 14);
            var skipRt = (RectTransform)_skipButton.transform;
            skipRt.anchorMin = skipRt.anchorMax = skipRt.pivot = new Vector2(0.5f, 0);
            skipRt.sizeDelta = new Vector2(150, 42);
            skipRt.anchoredPosition = new Vector2(265, 168);

            // Bouncer voucher: visible only while tonight's VIP can still be rerolled.
            _bouncerButton = NewButton("Bouncer", root, "BOUNCER: NEW VIP", NeonMagenta, OnBouncerClicked, 13);
            var bouncerRt = (RectTransform)_bouncerButton.transform;
            bouncerRt.anchorMin = bouncerRt.anchorMax = bouncerRt.pivot = new Vector2(0.5f, 0);
            bouncerRt.sizeDelta = new Vector2(150, 34);
            bouncerRt.anchoredPosition = new Vector2(265, 216);

            _railPanel = NewRect("Rail", root);
            Stretch(_railPanel, new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 12), new Vector2(-12, 160));
            StylePanel(_railPanel, WithAlpha(PanelPlum, 0.72f));
            var layout = _railPanel.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

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
            var rt = NewRect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = bg;
            if (buttonSprite != null)
            {
                image.sprite = buttonSprite;
                image.type = Image.Type.Sliced;
            }
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var textRt = NewRect("Text", rt);
            Stretch(textRt, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -4));
            var text = textRt.gameObject.AddComponent<Text>();
            // Art bible: cream on dark, plum on light — never pure black/white.
            float luminance = bg.r * 0.299f + bg.g * 0.587f + bg.b * 0.114f;
            ConfigureText(text, fontSize, TextAnchor.MiddleCenter, luminance < 0.45f ? Cream : PanelPlum);
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
