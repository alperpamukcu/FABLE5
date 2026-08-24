using System;
using System.Collections.Generic;
using LastCall.Core;
using UnityEngine;

namespace LastCall.Game
{
    /// <summary>A parsed deck file: fresh IngredientCard instances on every parse.</summary>
    public sealed class LoadedDeck
    {
        public string DeckId { get; }
        public string Name { get; }
        public IReadOnlyList<IngredientCard> Cards { get; }

        /// <summary>
        /// Cards marked locked in the file (v5 P10): future stock, quarantined at the parse.
        /// They never reach <see cref="Cards"/>, so the shelf, the market catalogue and the
        /// simulator's tier-1 sweep all stay exactly as they were the day before the content
        /// existed — unlocking is a purchase, not a load.
        /// </summary>
        public IReadOnlyList<IngredientCard> LockedCards { get; }

        public LoadedDeck(string deckId, string name, IReadOnlyList<IngredientCard> cards,
            IReadOnlyList<IngredientCard> lockedCards = null)
        {
            DeckId = deckId;
            Name = name;
            Cards = cards;
            LockedCards = lockedCards ?? System.Array.Empty<IngredientCard>();
        }
    }

    /// <summary>A parsed fixtures file: what the room can be dressed with, and where.</summary>
    public sealed class LoadedFixtures
    {
        /// <summary>The catalogue Core sells from.</summary>
        public IReadOnlyList<FixtureDefinition> Fixtures { get; }

        /// <summary>Where the stage stands them. Presentation, so it never reaches Core.</summary>
        public IReadOnlyList<StageSlot> Slots { get; }

        public LoadedFixtures(IReadOnlyList<FixtureDefinition> fixtures, IReadOnlyList<StageSlot> slots)
        {
            Fixtures = fixtures;
            Slots = slots;
        }
    }

    /// <summary>
    /// Parses Assets/Data JSON into pure-core models, validating loudly: these files are
    /// the game's modding/content surface, so a typo must fail at load, not mid-run.
    /// </summary>
    public static class DataLoader
    {
        public static LoadedDeck ParseDeck(string json)
        {
            var dto = FromJson<DeckFileDto>(json, "deck");
            if (dto.cards == null || dto.cards.Count == 0)
                throw new FormatException("Deck file contains no cards.");

            var cards = new List<IngredientCard>(dto.cards.Count);
            var locked = new List<IngredientCard>();
            foreach (var card in dto.cards)
            {
                if (string.IsNullOrWhiteSpace(card.id))
                    throw new FormatException("Deck file has a card with an empty id.");
                if (card.flavor < 0)
                    throw new FormatException($"Card '{card.id}' has negative flavor.");
                // Branded bottles (GDD 22) carry their identity papers; older files without
                // a style are plain cards and load as before.
                IngredientInfo info = null;
                if (!string.IsNullOrEmpty(card.style))
                {
                    if (card.tier < 1)
                        throw new FormatException($"Bottle '{card.id}' has tier {card.tier}; brands start at 1.");
                    if (card.tier > 1 && card.price <= 0)
                        throw new FormatException($"Bottle '{card.id}' is a market brand but has no price.");
                    // Every branded bottle says which aisle it lives on (v5 P10).
                    if (!IngredientCategories.IsKnown(card.category))
                        throw new FormatException(
                            $"Bottle '{card.id}' has unknown category '{card.category}'.");
                    if (card.unlockStars < 0)
                        throw new FormatException(
                            $"Bottle '{card.id}' waits for {card.unlockStars} stars, which is not a rung.");
                    // NEVER BELOW THE LADDER THE BOTTLE IS ALREADY ON. Market.ForSale takes
                    // the unlock branch INSTEAD of the tier/price test, not on top of it, so
                    // an unlockStars written under a bottle's own rung would quietly LOWER
                    // its gate — a $52 reserve gin on sale at one star, and nothing anywhere
                    // would say why. The later of the two is the honest answer, and for the
                    // tier-1 mixers this whole field exists for, the ladder says 0 and the
                    // field is simply the gate.
                    if (card.tapLevel < 0)
                        throw new FormatException(
                            $"Bottle '{card.id}' asks for {card.tapLevel} draught lines.");
                    if (card.tapLevel > 0 && ParseType(card.type, card.id) != IngredientType.Beer)
                        throw new FormatException(
                            $"Bottle '{card.id}' names a tap level but is not a beer — " +
                            "only a keg comes out of a line.");
                    var stars = card.unlockStars > 0
                        ? UnlockCondition.Stars(Math.Max(
                            card.unlockStars, Market.RequiredStars(card.tier, card.price)))
                        : null;
                    // A KEG NEEDS A LINE TO COME OUT OF (2026-08-19, the author: "marketten
                    // musluğu geliştirmeden bir üst seviye fıçı bira alınmamalı"). Written
                    // as a lock rather than as a star rung because it is not one: every keg
                    // in the game is tier 1, so the ladder says zero for all three of them
                    // and the tower is the only thing that separates them.
                    var gate = card.tapLevel > 1
                        ? UnlockCondition.All(stars ?? UnlockCondition.Open,
                                              UnlockCondition.Tap(card.tapLevel))
                        : stars;
                    info = new IngredientInfo(card.style, card.tier, card.price,
                        card.origin, card.abv, card.blurb, card.category, card.carbonated, gate);
                }
                var parsed = new IngredientCard(card.id, card.name, ParseType(card.type, card.id),
                    card.flavor, info: info);
                if (card.locked) locked.Add(parsed);
                else cards.Add(parsed);
            }
            return new LoadedDeck(dto.deckId, dto.name, cards, locked);
        }

        public static IReadOnlyList<RecipeDefinition> ParseRecipes(string json)
        {
            var dto = FromJson<RecipesFileDto>(json, "recipes");
            if (dto.recipes == null || dto.recipes.Count == 0)
                throw new FormatException("Recipes file contains no recipes.");

            var recipes = new List<RecipeDefinition>(dto.recipes.Count);
            foreach (var recipe in dto.recipes)
            {
                if (string.IsNullOrWhiteSpace(recipe.id))
                    throw new FormatException("Recipes file has a recipe with an empty id.");
                bool isGroupRecipe = recipe.equalFlavorGroupSize > 0 ||
                                     recipe.ascendingFlavorGroupSize > 0 ||
                                     recipe.sameTypeGroupMin > 0;
                if (!isGroupRecipe && (recipe.requirements == null || recipe.requirements.Count == 0))
                    throw new FormatException($"Recipe '{recipe.id}' has no requirements.");

                var requirements = new List<PatternRequirement>(recipe.requirements?.Count ?? 0);
                foreach (var req in recipe.requirements ?? new List<RequirementDto>())
                {
                    if (req.types == null || req.types.Count == 0)
                        throw new FormatException($"Recipe '{recipe.id}' has a requirement with no types.");
                    var types = new IngredientType[req.types.Count];
                    for (int i = 0; i < req.types.Count; i++)
                        types[i] = ParseType(req.types[i], recipe.id);
                    requirements.Add(new PatternRequirement(req.count, types));
                }

                // Hand-authored style bands (v5 P10): "a Gin & Tonic is 30–50% gin". When the
                // file gives none, bands derive from the type pattern as before.
                List<RatioRequirement> ratios = null;
                if (recipe.ratios != null && recipe.ratios.Count > 0)
                {
                    ratios = new List<RatioRequirement>(recipe.ratios.Count);
                    foreach (var band in recipe.ratios)
                    {
                        if (string.IsNullOrWhiteSpace(band.style))
                            throw new FormatException($"Recipe '{recipe.id}' has a ratio band with no style.");
                        if (band.max < band.min || band.min < 0 || band.max > 1)
                            throw new FormatException(
                                $"Recipe '{recipe.id}' has a bad {band.style} band {band.min}–{band.max}.");
                        if (band.minTier < 0)
                            throw new FormatException(
                                $"Recipe '{recipe.id}' has a negative minTier on its {band.style} band.");
                        // 0 and 1 both mean "any bottle of the style": JsonUtility cannot express
                        // an absent int, so the file leaves the field off for ordinary bands.
                        ratios.Add(new RatioRequirement(band.style, band.min, band.max,
                            Math.Max(1, band.minTier)));
                    }
                }

                recipes.Add(new RecipeDefinition(
                    recipe.id, recipe.name, recipe.rank,
                    recipe.baseFlavor, recipe.baseMult, recipe.flavorPerLevel, recipe.multPerLevel,
                    requirements,
                    recipe.exactMixSize, recipe.minMixSize,
                    recipe.allDistinctTypes, recipe.allEqualFlavor, recipe.scoreAllMixCards,
                    recipe.equalFlavorGroupSize, recipe.ascendingFlavorGroupSize,
                    recipe.sameTypeGroupMin,
                    ratioRequirements: ratios, // null = derive from the type pattern
                    minFill: recipe.minFill,
                    locked: recipe.locked,
                    prep: ParsePrep(recipe.prepMethod, recipe.id),
                    glassId: recipe.glassId,
                    icon: recipe.icon,
                    unlock: UnlockCondition.All(
                        UnlockCondition.Stars(recipe.unlockStars),
                        string.IsNullOrWhiteSpace(recipe.unlockBeat)
                            ? UnlockCondition.Open
                            : UnlockCondition.Kept(recipe.unlockBeat)),
                    unlockBeatId: recipe.unlockBeat));
            }
            return recipes;
        }

        /// <summary>The serving glasses (v5 P10): silhouette, sprite, upgrade prices.</summary>
        public static IReadOnlyList<GlasswareDefinition> ParseGlassware(string json)
        {
            var dto = FromJson<GlasswareFileDto>(json, "glassware");
            if (dto.glasses == null || dto.glasses.Count == 0)
                throw new FormatException("Glassware file contains no glasses.");

            var seen = new HashSet<string>();
            var glasses = new List<GlasswareDefinition>(dto.glasses.Count);
            foreach (var glass in dto.glasses)
            {
                if (!seen.Add(glass.id ?? ""))
                    throw new FormatException($"Glassware file lists '{glass.id}' twice.");
                try
                {
                    glasses.Add(new GlasswareDefinition(glass.id, glass.name,
                        glass.profile?.ToArray(), glass.tierPrices?.ToArray(), glass.capacity));
                }
                catch (ArgumentException e)
                {
                    throw new FormatException($"Glassware '{glass.id}': {e.Message}");
                }
            }
            return glasses;
        }

        /// <summary>The snack bowls (v5 P10).</summary>
        public static IReadOnlyList<SnackDefinition> ParseSnacks(string json)
        {
            var dto = FromJson<SnacksFileDto>(json, "snacks");
            if (dto.snacks == null || dto.snacks.Count == 0)
                throw new FormatException("Snacks file contains no snacks.");

            var seen = new HashSet<string>();
            var snacks = new List<SnackDefinition>(dto.snacks.Count);
            foreach (var snack in dto.snacks)
            {
                if (!seen.Add(snack.id ?? ""))
                    throw new FormatException($"Snacks file lists '{snack.id}' twice.");
                try
                {
                    snacks.Add(new SnackDefinition(snack.id, snack.name, snack.price, snack.stock));
                }
                catch (Exception e) when (e is ArgumentException || e is ArgumentOutOfRangeException)
                {
                    throw new FormatException($"Snack '{snack.id}': {e.Message}");
                }
            }
            return snacks;
        }

        /// <summary>
        /// Bar dressing (2026-08-10): the modular fixtures. One catalogue entry per slot —
        /// two fixtures fighting over one hook is a content bug and fails here, at load,
        /// where a content bug belongs. The one exception is the draught tower, which is a
        /// three-rung LADDER standing in a single slot (2026-08-19); see the rule below.
        /// </summary>
        public static LoadedFixtures ParseFixtures(string json)
        {
            var dto = FromJson<FixturesFileDto>(json, "fixtures");
            if (dto.fixtures == null || dto.fixtures.Count == 0)
                throw new FormatException("Fixtures file contains no fixtures.");
            if (dto.slots == null || dto.slots.Count == 0)
                throw new FormatException("Fixtures file declares no slots to stand them in.");

            // THE SLOTS ARE CONTENT NOW (2026-08-10). They were seven hardcoded Vector2s in
            // DiegeticStage, which meant a new place to put a plant was a code change — in a
            // project whose first rule about content is that content is data. A slot that
            // nothing can find is a silent hole in the room, so an unknown slot fails HERE,
            // at load, rather than warning into the console on the night it is bought.
            var slots = new List<StageSlot>(dto.slots.Count);
            var slotIds = new HashSet<string>();
            foreach (var sl in dto.slots)
            {
                if (!slotIds.Add(sl.id ?? ""))
                    throw new FormatException($"Fixtures file declares slot '{sl.id}' twice.");
                try { slots.Add(new StageSlot(sl.id, sl.x, sl.y, sl.onCounter,
                                              sl.pairSpreadPx, sl.houseLight)); }
                catch (ArgumentException e) { throw new FormatException($"Slot '{sl.id}': {e.Message}"); }
            }

            var seenIds = new HashSet<string>();
            // ONE PIECE PER HOOK, WITH ONE EXCEPTION (2026-08-19). A slot is a place in the
            // picture and two pieces standing in it draw one inside the other — still a
            // content bug, and still refused here. But the draught tower is a LADDER: three
            // levels of the same station, of which the room ever stands ONE (the tallest
            // owned). So a slot may hold several pieces if every one of them is a tower and
            // no two run the same number of lines. Anything else, including a tower sharing
            // a hook with a fern, is the old bug and reads as the old message.
            var slotOwners = new Dictionary<string, FixtureDto>();
            var slotLevels = new Dictionary<string, HashSet<int>>();
            var fixtures = new List<FixtureDefinition>(dto.fixtures.Count);
            foreach (var f in dto.fixtures)
            {
                if (!seenIds.Add(f.id ?? ""))
                    throw new FormatException($"Fixtures file lists '{f.id}' twice.");
                if (!slotIds.Contains(f.slot ?? ""))
                    throw new FormatException(
                        $"Fixture '{f.id}' wants slot '{f.slot}', which the room does not have.");
                // A piece's rung: a tower carries it in tapLevel, anything else in level.
                // The ladder rule stopped being about beer on 2026-08-24 (the wall lamps),
                // so the exception below reads the rung, not the tap. A piece claiming both
                // fields is refused HERE, before the slot check can blame the wrong thing.
                if (f.tapLevel > 0 && f.level > 0)
                    throw new FormatException(
                        $"Fixture '{f.id}' carries both a tapLevel and a level — a tower's " +
                        "rung IS its tap level.");
                int rung = f.tapLevel > 0 ? f.tapLevel : f.level;
                if (slotOwners.TryGetValue(f.slot, out var held))
                {
                    int heldRung = held.tapLevel > 0 ? held.tapLevel : held.level;
                    if (rung <= 0 || heldRung <= 0)
                        throw new FormatException(
                            $"Fixture '{f.id}' wants slot '{f.slot}', which another fixture already has.");
                    // One slot, one KIND of ladder. A tower rung and a lamp rung sharing a
                    // hook would pass the arithmetic below and then lie to everything that
                    // reads the ladder — LadderLevel would let a tap unlock a lamp mark.
                    if ((f.tapLevel > 0) != (held.tapLevel > 0))
                        throw new FormatException(
                            $"Fixture '{f.id}' puts a different kind of ladder into slot " +
                            $"'{f.slot}' — a slot's rungs must all be towers or none of them.");
                    if (!slotLevels[f.slot].Add(rung))
                        throw new FormatException(
                            $"Fixture '{f.id}' stands on rung {rung}, and slot '{f.slot}' " +
                            "already has one — a ladder cannot have two of the same rung.");
                }
                else
                {
                    slotOwners[f.slot] = f;
                    slotLevels[f.slot] = new HashSet<int> { rung };
                }
                try
                {
                    fixtures.Add(new FixtureDefinition(f.id, f.name, f.slot, f.price,
                        f.stars, f.flavor, f.sprite,
                        f.lightR, f.lightG, f.lightB, f.lightIntensity, f.lightRadius,
                        f.startsInTheRoom, f.tapLevel, f.level));
                }
                catch (Exception e) when (e is ArgumentException || e is ArgumentOutOfRangeException)
                {
                    throw new FormatException($"Fixture '{f.id}': {e.Message}");
                }
            }
            // A LADDER WITH A MISSING RUNG IS UNREACHABLE. The market sells a tower only to
            // a bar one line short of it, so a catalogue that jumps 1 → 3 puts the triple
            // permanently out of reach — bought by nobody, ever, and nothing at runtime
            // would say so. It is a content bug, so it fails at the load.
            foreach (var pair in slotLevels)
            {
                var levels = new List<int>(pair.Value);
                levels.Sort();
                if (levels[0] <= 0) continue;                 // not a ladder at all
                for (int i = 0; i < levels.Count; i++)
                    if (levels[i] != i + 1)
                        throw new FormatException(
                            $"The ladder in slot '{pair.Key}' runs {string.Join(", ", levels)} " +
                            "— it has to climb 1, 2, 3 with no rung missing.");
            }
            return new LoadedFixtures(fixtures, slots);
        }

        /// <summary>
        /// The cast's papers (GDD 22 §3, moved out of TycoonHud on 2026-08-12): what the
        /// licence prints for each face. Loud about the two mistakes that matter — two people
        /// claiming one look, and a flag code the art cannot draw — because both of them fail
        /// silently at the far end, on a card the player is being asked to read.
        /// </summary>
        public static PatronRoster ParsePapers(string json)
        {
            var dto = FromJson<PapersFileDto>(json, "papers");
            if (dto.papers == null || dto.papers.Count == 0)
                throw new FormatException("Papers file contains nobody.");

            var people = new List<Papers>(dto.papers.Count);
            foreach (var p in dto.papers)
            {
                string iso = p.iso ?? string.Empty;
                if (iso.Length != 2 || iso != iso.ToLowerInvariant())
                    throw new FormatException(
                        $"Papers for '{p.name}' carry the flag code '{iso}'; it must be two lower-case letters (fl_{iso}.png).");
                try
                {
                    people.Add(new Papers(p.slug, p.name, p.age, p.country, iso));
                }
                catch (Exception e) when (e is ArgumentException || e is ArgumentOutOfRangeException)
                {
                    throw new FormatException($"Papers for '{p.slug}': {e.Message}");
                }
            }
            try
            {
                return new PatronRoster(people);
            }
            catch (ArgumentException e)
            {
                throw new FormatException("Papers file: " + e.Message);
            }
        }

        /// <summary>
        /// The written nights (GDD 26 §10) — `Assets/Data/story/story.json` into a
        /// <see cref="StoryArc"/>. It needs both other files in hand: the CAST, because a
        /// story character is a face plus the papers that face already carries (§8), and the
        /// RECIPES, because every ask is graded against a real one.
        ///
        /// Everything that can be checked is checked here, loudly, at load: a look nobody has
        /// papers for, a drink that does not exist, a night that is not one of the six, a
        /// guest written for a quiet night, a lesson naming a condition no code observes, a
        /// beat leading nowhere. The arc is content the player never edits — so every one of
        /// these is a mistake somebody made in a text file, and all of them would otherwise
        /// show up weeks later as a customer who never walks in.
        /// </summary>
        public static StoryArc ParseStory(string json, PatronRoster cast,
            IReadOnlyList<RecipeDefinition> recipes)
        {
            if (cast == null) throw new ArgumentNullException(nameof(cast));
            if (recipes == null) throw new ArgumentNullException(nameof(recipes));
            var dto = FromJson<StoryFileDto>(json, "story");
            if (dto.beats == null || dto.beats.Count == 0)
                throw new FormatException("Story file has no beats in it.");

            var book = new Dictionary<string, RecipeDefinition>(StringComparer.Ordinal);
            foreach (var recipe in recipes) book[recipe.Id] = recipe;

            // ── the people ───────────────────────────────────────────────────────
            var people = new Dictionary<string, StoryCharacter>(StringComparer.Ordinal);
            int hosts = 0;
            if (dto.characters == null || dto.characters.Count == 0)
                throw new FormatException("Story file has nobody in it.");
            foreach (var c in dto.characters)
            {
                if (people.ContainsKey(c.id ?? string.Empty))
                    throw new FormatException($"Story: two characters are called '{c.id}'.");
                var papers = cast.For(c.look ?? string.Empty);
                if (papers == null)
                    throw new FormatException(
                        $"Story character '{c.id}' wears the look '{c.look}', which nobody in " +
                        "papers.json has papers for.");
                bool host = string.Equals(c.role, "host", StringComparison.OrdinalIgnoreCase);
                if (!host && !string.Equals(c.role, "guest", StringComparison.OrdinalIgnoreCase))
                    throw new FormatException(
                        $"Story character '{c.id}' has role '{c.role}'; it is 'host' or 'guest'.");
                if (host) hosts++;
                if (!string.IsNullOrEmpty(c.placeholderLook) && cast.For(c.placeholderLook) == null)
                    throw new FormatException(
                        $"Story character '{c.id}' borrows the face '{c.placeholderLook}', " +
                        "which is not in papers.json.");
                try
                {
                    people[c.id] = new StoryCharacter(c.id, c.look, papers.Name, papers.Age,
                        papers.Country, host, c.blurb, c.placeholderLook);
                }
                catch (Exception e) when (e is ArgumentException || e is ArgumentOutOfRangeException)
                {
                    throw new FormatException($"Story character '{c.id}': {e.Message}");
                }
            }
            if (hosts != 1)
                throw new FormatException(
                    $"Story: the bar has {hosts} hosts; it has exactly one (GDD 26 §1b).");

            // ── the nights ───────────────────────────────────────────────────────
            var beats = new List<StoryBeat>(dto.beats.Count);
            foreach (var b in dto.beats)
            {
                if (!people.TryGetValue(b.character ?? string.Empty, out var who))
                    throw new FormatException(
                        $"Beat '{b.id}' is about '{b.character}', who is not in the story's characters.");
                var night = BarCalendar.Parse(b.night);
                if (night == null)
                    throw new FormatException(
                        $"Beat '{b.id}' happens on a '{b.night}'; the bar opens Tuesday to Sunday.");
                if (b.asks == null || b.asks.Count == 0)
                    throw new FormatException($"Beat '{b.id}' asks for nothing.");

                var asks = new List<RecipeDefinition>(b.asks.Count);
                foreach (var id in b.asks)
                {
                    if (!book.TryGetValue(id ?? string.Empty, out var drink))
                        throw new FormatException(
                            $"Beat '{b.id}' asks for '{id}', which is not a recipe.");
                    asks.Add(drink);
                }
                RequireRecipe(book, b.grantsRecipeOnAsk, b.id, "grantsRecipeOnAsk");

                // THE ASK ALWAYS NAMES WHAT IS MISSING (GDD 26 §4, the arc's first standing
                // rule). Since the drinks are revealed one at a time, the host's early
                // warning is the ONLY notice a player gets — so a beat that needs a style the
                // shelf may not have, and never says the word, is a wall pretending to be a
                // quest. Checked here because it is a rule about CONTENT, and content is
                // where it gets broken.
                if (!string.IsNullOrEmpty(b.needStyle) && !Mentions(b.hostWarning, b.needStyle))
                    throw new FormatException(
                        $"Beat '{b.id}' needs '{b.needStyle}' on the shelf and no hostWarning line " +
                        "says the word. The player has to be told, days early, in the market's " +
                        "own vocabulary (GDD 26 §4).");

                // A GATE WITHOUT WORDS IS A BUG WITH A FACE (GDD 26 §12). A guest who comes,
                // sits and orders nothing has to SAY why — that scene is the entire teaching
                // of the star track, and a beat that locks silently is a customer the player
                // will report as broken. Same shape as the rule above it: content is checked
                // where content is written.
                if (b.requiresStars > 0 && (b.shortOfGate == null || b.shortOfGate.Count == 0))
                    throw new FormatException(
                        $"Beat '{b.id}' asks for {b.requiresStars} stars and has no shortOfGate " +
                        "line. They still come and sit down when the bar is short — they have " +
                        "to be able to say what it is short of (GDD 26 §12).");

                try
                {
                    var trial = new StoryTrial(asks, b.seconds,
                        b.minFill > 0 ? b.minFill : StoryTrial.DefaultMinFill, b.allowedMistakes);
                    var lines = new StoryLines(b.ask, b.nudge, b.servedRight, b.servedWrong,
                        b.declined, b.hostBefore, b.hostAfter, b.hostWarning, b.shortOfGate);
                    beats.Add(new StoryBeat(b.id, who, trial, b.week, night.Value, lines,
                        b.needStyle, b.needTier, b.grantsRecipeOnAsk,
                        b.returnsAfterWeeks > 0 ? b.returnsAfterWeeks : 1, b.next,
                        b.requiresStars));
                }
                catch (Exception e) when (e is ArgumentException || e is ArgumentOutOfRangeException)
                {
                    throw new FormatException($"Beat '{b.id}': {e.Message}");
                }
            }

            // ── the host's lessons ───────────────────────────────────────────────
            var lessons = new List<StoryLesson>();
            if (dto.lessons != null)
                foreach (var l in dto.lessons)
                {
                    var cue = StoryCues.Parse(l.when);
                    if (cue == null)
                        throw new FormatException(
                            $"Lesson '{l.id}' fires on '{l.when}', which nothing in the game " +
                            $"watches for. It is one of: {string.Join(", ", StoryCues.Names)}.");
                    try
                    {
                        lessons.Add(new StoryLesson(l.id, cue.Value, l.say));
                    }
                    catch (ArgumentException e)
                    {
                        throw new FormatException($"Lesson '{l.id}': {e.Message}");
                    }
                }

            StoryArc arc;
            try
            {
                arc = new StoryArc(beats, lessons);
            }
            catch (ArgumentException e)
            {
                throw new FormatException("Story file: " + e.Message);
            }

            // A RECIPE MAY NAME A NIGHT, AND THE NIGHT HAS TO EXIST (GDD 26 §12.2 step 4).
            // Recipes load before the story does, so this is the first moment both halves are
            // in the same room — and it is the only moment the check is cheap. A mistyped
            // beat id would otherwise become a page that is locked forever with a sentence
            // naming a person who never comes, which is the worst failure this system has:
            // silent, permanent, and indistinguishable from content nobody wrote.
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var beat in arc.Beats) ids.Add(beat.Id);
            foreach (var recipe in recipes)
            {
                if (recipe.UnlockBeatId == null || ids.Contains(recipe.UnlockBeatId)) continue;
                throw new FormatException(
                    $"Recipe '{recipe.Id}' is locked behind story beat '{recipe.UnlockBeatId}', " +
                    "and the arc has no such night. A page nobody can ever earn is worse than " +
                    "a page nobody wrote (GDD 26 §12.2).");
            }
            return arc;
        }

        private static bool Mentions(List<string> lines, string word)
        {
            if (lines == null) return false;
            foreach (var line in lines)
                if (!string.IsNullOrEmpty(line)
                    && line.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static void RequireRecipe(Dictionary<string, RecipeDefinition> book,
            string id, string beatId, string field)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!book.ContainsKey(id))
                throw new FormatException($"Beat '{beatId}' names '{id}' as its {field}, which is not a recipe.");
        }

        private static PrepMethod ParsePrep(string raw, string context)
        {
            // Loud, not silent (2026-08-14): the method now decides whether Core will let
            // the drink out of the tin at all, so a recipe that forgets to name one is a
            // content bug, not a shrug. All 53 shipped recipes declare it; this only ever
            // fires on a new one.
            if (string.IsNullOrEmpty(raw))
                throw new FormatException(
                    $"Recipe '{context}' names no prepMethod — say Shaken, Stirred or Built.");
            return ParseEnum<PrepMethod>(raw, context, "prepMethod");
        }

        /// <summary>
        /// Customer archetypes (GDD 19 §9). Bands are addressed by emotion name rather than
        /// position so a reordered file can't silently make everyone furious.
        /// </summary>
        public static IReadOnlyList<ArchetypeDefinition> ParseArchetypes(string json)
        {
            var dto = FromJson<ArchetypesFileDto>(json, "archetypes");
            if (dto.archetypes == null || dto.archetypes.Count == 0)
                throw new FormatException("Archetypes file contains no archetypes.");

            var archetypes = new List<ArchetypeDefinition>(dto.archetypes.Count);
            foreach (var archetype in dto.archetypes)
            {
                if (string.IsNullOrWhiteSpace(archetype.id))
                    throw new FormatException("Archetypes file has an archetype with an empty id.");
                int weight = archetype.weight > 0 ? archetype.weight : 1;
                var hometowns = archetype.hometowns;
                archetypes.Add(new ArchetypeDefinition(
                    archetype.id, archetype.name, archetype.names, weight, hometowns));
            }
            return archetypes;
        }

        private static T ParseEnum<T>(string raw, string context, string field) where T : struct
        {
            if (Enum.TryParse(raw, ignoreCase: false, out T value)) return value;
            throw new FormatException($"Unknown {field} '{raw}' (in '{context}').");
        }

        private static T FromJson<T>(string json, string label) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException($"Empty {label} JSON.");
            var dto = JsonUtility.FromJson<T>(json);
            if (dto == null)
                throw new FormatException($"Could not parse {label} JSON.");
            return dto;
        }

        private static IngredientType ParseType(string raw, string context)
        {
            if (Enum.TryParse(raw, ignoreCase: false, out IngredientType type)) return type;
            throw new FormatException($"Unknown ingredient type '{raw}' (in '{context}').");
        }

#pragma warning disable 0649 // fields assigned by JsonUtility via reflection
        [Serializable]
        private sealed class CardDto
        {
            public string id;
            public string name;
            public string type;
            public int flavor;
            // Brand papers (GDD 22); style empty = plain unbranded card.
            public string style;
            public int tier;
            public int price;
            public string origin;
            public double abv;
            public string blurb;
            // v5 P10 content model.
            public string category;
            public bool carbonated;
            public bool locked;

            /// <summary>
            /// The standing this bottle waits for (2026-08-14, the author: "bazı
            /// meşrubatlarda alkoller gibi sonra açılabilir, örneğin başka yıldız
            /// seviyelerinde"). 0 or absent = for sale as soon as its tier and price allow.
            ///
            /// It exists because the tier/price ladder cannot say this: every mixer in the
            /// game is tier 1, and tier 1 means zero stars, so the only way to hold a tonic
            /// back used to be the quarantine — which made it INVISIBLE, and an invisible
            /// bottle teaches the player nothing about what is coming.
            /// </summary>
            public double unlockStars;

            /// <summary>
            /// How many draught lines the bar must run before this keg is for sale
            /// (2026-08-19). 0 or absent = no tap gate, which is every bottle in the game
            /// that is not a beer and the first keg, which pours on the tower the bar opens
            /// with. Beer only: anything else naming one is a content bug and fails at load.
            /// </summary>
            public int tapLevel;
        }

        [Serializable]
        private sealed class FixtureDto
        {
            public string id;
            public string name;
            public string slot;
            public int price;
            public double stars;
            public string flavor;
            public string sprite;
            // A lamp is a fixture whose intensity is above zero; JsonUtility cannot say
            // "absent", so unlit fixtures simply leave the light block off (0 defaults).
            public float lightR;
            public float lightG;
            public float lightB;
            public float lightIntensity;
            public float lightRadius;
            // The room opens with this one already standing in it (the first beer font).
            // Defaults false, so every entry that does not mention it is unchanged.
            public bool startsInTheRoom;

            /// <summary>How many draught lines this tower runs; 0 (absent) for anything
            /// that is not one. It replaced a plain `tap` bool on 2026-08-19 when the fonts
            /// became a three-rung ladder — JsonUtility cannot say "absent", so 0 is the
            /// not-a-tower answer and every entry that never mentioned taps is unchanged.</summary>
            public int tapLevel;

            /// <summary>This piece's rung on its slot's ladder, for ladders that are not
            /// the draught tower (2026-08-24, the wall lamps). A tower's rung stays in
            /// tapLevel; carrying both is a content bug the definition refuses.</summary>
            public int level;
        }

        [Serializable]
        private sealed class SlotDto
        {
            public string id;
            public float x;
            public float y;
            public bool onCounter;
            // A pair of mounting points this far apart, symmetric about (x, y); 0 = one
            // hook. And whether what shines here is the room's HOUSE LIGHT, run on the
            // evening's clock. Defaults keep every old entry unchanged (JsonUtility).
            public float pairSpreadPx;
            public bool houseLight;
        }

        [Serializable]
        private sealed class FixturesFileDto
        {
            public int version;
            public List<SlotDto> slots;
            public List<FixtureDto> fixtures;
        }

        [Serializable]
        private sealed class ArchetypeDto
        {
            public string id;
            public string name;
            public int weight;
            public List<string> names;
            public List<string> hometowns;
        }

        [Serializable]
        private sealed class ArchetypesFileDto
        {
            public int version;
            public List<ArchetypeDto> archetypes;
        }

        [Serializable]
        private sealed class StoryFileDto
        {
            public int version;
            public List<StoryCharacterDto> characters;
            public List<StoryBeatDto> beats;
            public List<StoryLessonDto> lessons;
        }

        [Serializable]
        private sealed class StoryCharacterDto
        {
            public string id;
            public string look;
            public string role;
            public string blurb;
            public string placeholderLook;
        }

        [Serializable]
        private sealed class StoryBeatDto
        {
            public string id;
            public string character;
            public int week;
            public string night;
            public List<string> asks;
            public double seconds;
            public double minFill;          // 0 = the house standard (0.90)
            public int allowedMistakes;
            public string needStyle;
            public int needTier;
            public string grantsRecipeOnAsk;
            public List<string> ask;
            public List<string> nudge;
            public List<string> servedRight;
            public List<string> servedWrong;
            public List<string> declined;
            public List<string> shortOfGate;
            public double requiresStars;    // 0 = anybody may be served this beat
            public List<string> hostWarning;
            public List<string> hostBefore;
            public List<string> hostAfter;
            public int returnsAfterWeeks;   // 0 = the default, one week
            public string next;
        }

        [Serializable]
        private sealed class StoryLessonDto
        {
            public string id;
            public string when;
            public List<string> say;
        }

        [Serializable]
        private sealed class PapersFileDto
        {
            public int version;
            public List<PapersDto> papers;
        }

        [Serializable]
        private sealed class PapersDto
        {
            public string slug;
            public string name;
            public int age;
            public string country;
            public string iso;
        }

        [Serializable]
        private sealed class DeckFileDto
        {
            public string deckId;
            public string name;
            public List<CardDto> cards;
        }

        [Serializable]
        private sealed class RequirementDto
        {
            public List<string> types;
            public int count;
        }

        [Serializable]
        private sealed class RecipeDto
        {
            public string id;
            public string name;
            public int rank;
            public int baseFlavor;
            public int baseMult;
            public int flavorPerLevel;
            public int multPerLevel;
            public List<RequirementDto> requirements;
            public int exactMixSize;
            public int minMixSize;
            public bool allDistinctTypes;
            public bool allEqualFlavor;
            public bool scoreAllMixCards;
            public int equalFlavorGroupSize;
            public int ascendingFlavorGroupSize;
            public int sameTypeGroupMin;
            public double minFill;
            // v5 P10 content model.
            public bool locked;
            public string prepMethod;
            public string glassId;
            public string icon;
            // THE PAGE'S OWN LOCK (GDD 26 §12.2 step 4). Absent on every drink whose gate is
            // only its rank, which is all of them today; `unlockBeat` names the written night
            // that hands it over, `unlockStars` the rung it also wants. Either may stand
            // alone — UnlockCondition.All collapses when one of them is nothing.
            public string unlockBeat;
            public double unlockStars;
            public List<RatioDto> ratios;
        }

        [Serializable]
        private sealed class RatioDto
        {
            public string style;
            public double min;
            public double max;
            /// <summary>Lowest brand rung that fills this band; absent/0 = any bottle.</summary>
            public int minTier;
        }

        [Serializable]
        private sealed class RecipesFileDto
        {
            public int version;
            public List<RecipeDto> recipes;
        }

        [Serializable]
        private sealed class GlassDto
        {
            public string id;
            public string name;
            public List<double> profile;
            public List<int> tierPrices;
            public double capacity;
        }

        [Serializable]
        private sealed class GlasswareFileDto
        {
            public int version;
            public List<GlassDto> glasses;
        }

        [Serializable]
        private sealed class SnackDto
        {
            public string id;
            public string name;
            public int price;
            public int stock;
        }

        [Serializable]
        private sealed class SnacksFileDto
        {
            public int version;
            public List<SnackDto> snacks;
        }

#pragma warning restore 0649
    }
}
