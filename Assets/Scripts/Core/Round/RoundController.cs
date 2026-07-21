using System;
using System.Collections.Generic;
using System.Linq;

namespace LastCall.Core
{
    public enum RoundPhase
    {
        InProgress,

        /// <summary>The order was filled — the score target was reached.</summary>
        Won,

        /// <summary>
        /// The visit ended without filling the order. Not a loss: the customer leaves with
        /// whatever satisfaction the night earned them (fork B — only the week's quota can
        /// end a run). See <c>Docs/PLAN_emotion_pivot.md</c> D2.
        /// </summary>
        Closed
    }

    /// <summary>
    /// One customer round under the pour system (GDD 21). The player holds bottles from the
    /// <see cref="Shelf"/> to build a drink in the glass, then serves it. Each serve scores,
    /// moves the customer's emotions, and counts against the drinks they will accept.
    ///
    /// Replaces the card-era round: there is no deck, no rail and no Restock. Scarcity comes
    /// from bottles running dry (run-level) and from spilling (round-level).
    /// This class never touches randomness.
    /// Patron slot order = roster list order (gameplay-relevant, GDD 08).
    /// </summary>
    public sealed class RoundController
    {
        private readonly Shelf _shelf;
        private readonly IReadOnlyList<RecipeDefinition> _recipes;
        private readonly IReadOnlyDictionary<string, int> _recipeLevels;
        private readonly IReadOnlyList<PatronInstance> _patrons;
        private readonly HashSet<string> _servedRecipeIds = new HashSet<string>();
        private readonly int _night;
        private int _chatsSpent;

        public CustomerOrder Customer { get; }
        public RoundConfig Config { get; }

        /// <summary>Everything behind the bar; always available, and it drains.</summary>
        public Shelf Shelf => _shelf;

        /// <summary>What is in the glass right now (GDD 21 §3).</summary>
        public GlassContents Glass { get; private set; }

        /// <summary>The bottle currently being poured, or null.</summary>
        public string PouringId { get; private set; }

        /// <summary>Drinks this customer will still accept.</summary>
        public int DrinksRemaining { get; private set; }

        public double AccumulatedScore { get; private set; }
        public RoundPhase Phase { get; private set; }

        public int DrinksServed => Config.DrinksPerCustomer - DrinksRemaining;

        /// <summary>Kept under its old name because patron conditions read it (GDD 13).</summary>
        public int MixesUsed => DrinksServed;

        /// <summary>Glasses spilled this visit. The pour system's own mistake counter.</summary>
        public int Spills { get; private set; }

        public IReadOnlyList<PatronInstance> Patrons => _patrons;

        /// <summary>Chats left this visit (GDD 19 §8); each one costs a drink slot.</summary>
        public int ChatsRemaining { get; private set; }

        /// <summary>Verdict on the most recent serve; null before the first one.</summary>
        public ResonanceResult LastResonance { get; private set; }

        /// <summary>Satisfaction this visit has earned toward the week's quota (GDD 19 §10).</summary>
        public int SatisfactionEarned { get; private set; }

        /// <summary>Money owed by OnCustomerEnd patron effects; set once when the round is won.</summary>
        public double PatronPayout { get; private set; }

        /// <summary>The active VIP rules; <see cref="VipRuleSet.Empty"/> for regular customers.</summary>
        public VipRuleSet VipRules { get; }

        public RoundController(Shelf shelf, IReadOnlyList<RecipeDefinition> recipes,
            CustomerOrder customer, RoundConfig config = null,
            IReadOnlyDictionary<string, int> recipeLevels = null,
            IReadOnlyList<PatronInstance> patrons = null,
            VipRuleSet vipRules = null, int night = 1)
        {
            _night = night;
            _shelf = shelf ?? throw new ArgumentNullException(nameof(shelf));
            _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
            Customer = customer ?? throw new ArgumentNullException(nameof(customer));
            Config = config ?? RoundConfig.Default;
            _recipeLevels = recipeLevels;
            _patrons = patrons ?? Array.Empty<PatronInstance>();
            VipRules = vipRules ?? VipRuleSet.Empty;

            DrinksRemaining = Config.DrinksPerCustomer;
            ChatsRemaining = Customer.HasEmotion ? Config.ChatsPerCustomer : 0;
            Phase = RoundPhase.InProgress;
            Glass = new GlassContents(Config.GlassCapacity);
        }

        // ── pouring ──────────────────────────────────────────────────────────────

        /// <summary>Starts pouring a bottle. Holding a second bottle switches to it.</summary>
        public void BeginPour(string ingredientId)
        {
            EnsureRoundInProgress();
            if (_shelf.Find(ingredientId) == null)
                throw new ArgumentException($"No '{ingredientId}' on the shelf.", nameof(ingredientId));
            PouringId = ingredientId;
        }

        /// <summary>
        /// Advances the current pour by <paramref name="seconds"/>. Returns the volume that
        /// actually went in — less than asked when the bottle runs dry, which is not a
        /// failure (PLAN P7). Pouring past the brim spills; the glass keeps the overflow so
        /// the UI can show it, and <see cref="Serve"/> refuses it.
        /// </summary>
        public double PourTick(double seconds)
        {
            EnsureRoundInProgress();
            if (PouringId == null || seconds <= 0) return 0;

            var bottle = _shelf.Find(PouringId);
            double poured = _shelf.PourInto(Glass, PouringId, bottle.PourRate * seconds);
            // Nothing left to give (dry bottle) or to take (full glass): release the hold.
            if (poured <= 0 || bottle.IsEmpty) PouringId = null;
            return poured;
        }

        public void EndPour() => PouringId = null;

        /// <summary>
        /// Pours an exact measure in one go. This is the tap-to-measure input mode
        /// (GDD 21 §10) — hold-to-pour is unusable for some players, so a discrete measure is
        /// a required alternative, not a convenience. Returns the volume that went in.
        /// </summary>
        public double PourMeasure(string ingredientId, double volume)
        {
            EnsureRoundInProgress();
            if (_shelf.Find(ingredientId) == null)
                throw new ArgumentException($"No '{ingredientId}' on the shelf.", nameof(ingredientId));
            if (volume <= 0) return 0;
            return _shelf.PourInto(Glass, ingredientId, volume);
        }

        /// <summary>One garnish tap = this share of the glass (GDD 21 §3, 2026-07-20).</summary>
        public const double GarnishClickFraction = 0.05;

        /// <summary>
        /// Garnishes go in by the pinch, not the stream: one tap drops a fixed 5% of the
        /// glass. A pinch is deliberate — trickling out 1% slivers by timing a held jar
        /// was busywork with no read behind it.
        /// </summary>
        public double PourGarnish(string ingredientId) =>
            PourMeasure(ingredientId, GarnishClickFraction * Config.GlassCapacity);

        /// <summary>Bins the glass. The volume is gone; a spill is cleared this way too.</summary>
        public void Discard()
        {
            EnsureRoundInProgress();
            // The glass can no longer overflow, so "spills" now count what goes down the
            // drain on purpose: binning anything with liquid in it is the waste that
            // NoSpillsThisCustomer patrons care about.
            if (!Glass.IsEmpty) Spills++;
            PouringId = null;
            Glass = new GlassContents(Config.GlassCapacity);
        }

        // ── previews ─────────────────────────────────────────────────────────────

        /// <summary>Non-consuming lookup for the UI's live "this would be a Martini" label.</summary>
        public RecipeMatch PreviewMatch() =>
            RatioRecipeMatcher.Match(Glass, _recipes, IngredientOf);

        /// <summary>
        /// Non-consuming full score preview. Runs the same engine path as Serve, but scaling
        /// patrons must not grow from previews, so accumulator changes are rolled back.
        /// </summary>
        public ScoreBreakdown PreviewScore()
        {
            if (Glass.IsEmpty) return ScoreBreakdown.NoRecipe;

            var match = PreviewMatch();
            if (match != null && IsVoidedByVipRule(match, out string voidReason))
                return ScoreBreakdown.Voided(match.Recipe, LevelOf(match), voidReason);

            var accumulatedBefore = new double[_patrons.Count];
            for (int i = 0; i < _patrons.Count; i++) accumulatedBefore[i] = _patrons[i].Accumulated;

            var breakdown = match == null
                ? HousePourScore(PreviewResonance())
                : ScoringEngine.Score(match, LevelOf(match), _patrons,
                    BuildContext(match), PreviewResonance());

            for (int i = 0; i < _patrons.Count; i++)
            {
                double delta = accumulatedBefore[i] - _patrons[i].Accumulated;
                if (delta != 0) _patrons[i].Accumulate(delta);
            }
            return breakdown;
        }

        /// <summary>
        /// Projection of what the glass would do to the customer, for the pre-commit readout
        /// (GDD 19 §5). Raw and unclamped: the UI has to be able to show an overshoot before
        /// it happens.
        /// </summary>
        public EmotionDelta PreviewCharges()
        {
            if (!Customer.HasEmotion || Glass.IsEmpty) return EmotionDelta.Empty;

            var match = PreviewMatch();
            if (match != null && IsVoidedByVipRule(match, out _)) return EmotionDelta.Empty;
            return TotalDelta(match);
        }

        /// <summary>Non-consuming preview of the verdict this glass would earn.</summary>
        public ResonanceResult PreviewResonance()
        {
            if (!Customer.HasEmotion) return ResonanceResult.None;
            var delta = PreviewCharges();
            return delta.IsEmpty
                ? ResonanceResult.None
                : ResonanceJudge.Judge(Customer.Regular.Stats, delta, Customer.Read);
        }

        /// <summary>
        /// The score for a glass that matches nothing (GDD 21 §9, 2026-07-20): its
        /// volume-weighted Flavor at ×1. Any honest pour pays a little — experimenting
        /// should feel like practice, not punishment — while a real recipe's Flavor × Mult
        /// pays an order of magnitude more, which is the reward ladder.
        /// </summary>
        private ScoreBreakdown HousePourScore(ResonanceResult resonance)
        {
            double flavor = 0;
            var cards = new List<IngredientCard>();
            foreach (var id in Glass.Ingredients)
            {
                var card = IngredientOf(id);
                if (card == null) continue;
                cards.Add(card);
                flavor += card.Flavor * (Glass.VolumeOf(id) / Glass.Capacity);
            }
            return ScoreBreakdown.HousePour(Math.Round(flavor, 1), cards, resonance);
        }

        // ── serving ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Hands the glass over. Scores it, moves the customer, and empties the glass.
        /// </summary>
        public ScoreBreakdown Serve()
        {
            EnsureRoundInProgress();
            if (Glass.IsEmpty) throw new InvalidOperationException("Nothing in the glass.");

            PouringId = null;
            var match = PreviewMatch();
            ScoreBreakdown breakdown;

            if (match != null && IsVoidedByVipRule(match, out string voidReason))
            {
                // Voided serves still cost the drink and the volume — the rule is on the
                // ticket; ignoring it is the player's mistake (GDD 6). The drink never
                // reaches the customer, so it says nothing to them either.
                breakdown = ScoreBreakdown.Voided(match.Recipe, LevelOf(match), voidReason);
                LastResonance = null;
            }
            else
            {
                var resonance = JudgeServe(match);
                LastResonance = resonance;
                breakdown = match == null
                    ? HousePourScore(resonance)
                    : ScoringEngine.Score(match, LevelOf(match), _patrons, BuildContext(match), resonance);
                CommitServe(resonance);
            }

            if (match != null) _servedRecipeIds.Add(match.Recipe.Id);
            AccumulatedScore += breakdown.FinalScore;

            Glass = new GlassContents(Config.GlassCapacity);
            DrinksRemaining--;

            if (AccumulatedScore >= Customer.TargetScore)
            {
                Phase = RoundPhase.Won;
                var endContext = new EffectContext(null, null, DrinksServed, 0,
                    noSpills: Spills == 0);
                PatronTriggers.ResolveAccumulation(EffectTrigger.OnCustomerEnd, _patrons, endContext);
                PatronPayout = PatronTriggers.ResolveMoney(EffectTrigger.OnCustomerEnd, _patrons, endContext);
            }
            else if (DrinksRemaining == 0)
            {
                Phase = RoundPhase.Closed;
            }

            return breakdown;
        }

        // ── listening ────────────────────────────────────────────────────────────

        /// <summary>
        /// Ask instead of pour (GDD 19 §8). Costs a drink slot: the customer only has so much
        /// time for you, and spending it talking is the trade.
        /// </summary>
        public StatReading Chat(Emotion emotion)
        {
            EnsureRoundInProgress();
            if (!Customer.HasEmotion)
                throw new InvalidOperationException("This customer has no emotional read.");
            if (ChatsRemaining <= 0) throw new InvalidOperationException("No Chats remaining.");
            if (DrinksRemaining <= 1)
                throw new InvalidOperationException("No time left to talk — they want their drink.");

            ChatsRemaining--;
            DrinksRemaining--;
            _chatsSpent++;

            NarrowReading(emotion);
            return Customer.Read[emotion];
        }

        /// <summary>
        /// Tightens one reading by a step, free of charge. The paid-for entry points (Chat,
        /// Eavesdrop, the information patrons) all funnel through here so there is one place
        /// that decides how much a piece of information is worth.
        /// </summary>
        public void NarrowReading(Emotion emotion)
        {
            if (!Customer.HasEmotion) return;
            int truth = Customer.Regular.Stats[emotion];
            int halfWidth = CustomerReadFactory.HalfWidthFor(_night, Customer.Regular.Relationship);
            Customer.Learn(Customer.Read.Narrowing(emotion, truth, halfWidth));
        }

        /// <summary>
        /// Tightens whichever reading the bartender is most in the dark about. Returns false
        /// when the licence is already fully legible and there is nothing left to learn.
        /// </summary>
        public bool NarrowDarkestReading()
        {
            if (!Customer.HasEmotion) return false;
            if (!Customer.Read.TryPickDarkest(out var darkest)) return false;
            NarrowReading(darkest);
            return true;
        }

        // ── internals ────────────────────────────────────────────────────────────

        private IngredientCard IngredientOf(string ingredientId) =>
            _shelf.Find(ingredientId)?.Ingredient;

        /// <summary>
        /// Everything the glass does to the customer: the ingredients themselves, plus the
        /// bonus for handing over the kind of drink they wanted to be holding (GDD 21 §5).
        /// </summary>
        private EmotionDelta TotalDelta(RecipeMatch match)
        {
            var delta = PourResolver.Resolve(Glass, match, IngredientOf);
            if (Customer.Read != null)
            {
                var preference = Customer.Read.FillPreference;
                delta.Add(PourResolver.FillBonus(Glass, preference,
                    DirectionFor(preference.Serves)));
            }
            return delta;
        }

        /// <summary>
        /// Which way the fill bonus should push the stat it serves. It follows the customer's
        /// stated intent when it happens to name the same stat, and otherwise settles it —
        /// handing someone the right glass calms, it does not stir.
        /// </summary>
        private IntentDirection DirectionFor(Emotion emotion) =>
            Customer.Read != null && Customer.Read.Intent == emotion
                ? Customer.Read.Direction
                : IntentDirection.Extinguish;

        private ResonanceResult JudgeServe(RecipeMatch match)
        {
            if (!Customer.HasEmotion) return null;
            var delta = TotalDelta(match);
            return ResonanceJudge.Judge(Customer.Regular.Stats, delta, Customer.Read);
        }

        /// <summary>
        /// Writes the verdict through to the person. A bust commits nothing — pushing someone
        /// too far doesn't leave them further along, it just doesn't land.
        /// </summary>
        private void CommitServe(ResonanceResult resonance)
        {
            if (resonance == null || !Customer.HasEmotion) return;
            Customer.Regular.Stats.Apply(resonance.CommittedDelta);
            SatisfactionEarned += resonance.Satisfaction;
        }

        private EffectContext BuildContext(RecipeMatch match)
        {
            var contents = match?.ScoredCards ??
                RatioRecipeMatcher.ScoredContents(Glass, IngredientOf).cards;
            return new EffectContext(contents, match?.Recipe, DrinksServed, 0,
                VipRules.DebuffedTypes, noSpills: Spills == 0);
        }

        private bool IsVoidedByVipRule(RecipeMatch match, out string reason)
        {
            if (VipRules.OnlyFirstMixScores && DrinksServed > 0)
            {
                reason = "only the first drink counts";
                return true;
            }
            if (VipRules.MinRecipeLevel > 0 && LevelOf(match) < VipRules.MinRecipeLevel)
            {
                reason = $"only Recipes level {VipRules.MinRecipeLevel}+ score";
                return true;
            }
            if (VipRules.EachMixDifferentRecipe && _servedRecipeIds.Contains(match.Recipe.Id))
            {
                reason = "every drink must be a different Recipe";
                return true;
            }
            reason = string.Empty;
            return false;
        }

        private int LevelOf(RecipeMatch match) =>
            match != null && _recipeLevels != null && _recipeLevels.TryGetValue(match.Recipe.Id, out int level)
                ? level
                : 1;

        private void EnsureRoundInProgress()
        {
            if (Phase != RoundPhase.InProgress)
                throw new InvalidOperationException($"Round is over ({Phase}).");
        }
    }
}
