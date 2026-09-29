using System;
using System.Collections.Generic;
using System.Globalization;

namespace LastCall.Core
{
    /// <summary>
    /// A RUN, WRITTEN DOWN AT DAWN (2026-09-26, the author: "oyuna kayıt sistemi getirilsin
    /// artık"). Everything a run is between two nights, as data a JSON writer can carry:
    /// ids and numbers only — never content, which is reloaded from the same files a fresh
    /// run reads, and never Unity, which is why this lives in Core and the file I/O does not.
    ///
    /// THE SAVE POINT IS THE NIGHT'S EDGE. The one quiescent instant in the loop is inside
    /// <see cref="TycoonRun.ContinueToNextDay(Action{RunSnapshot})"/>: the old night is filed,
    /// the counters are reset, the hostess's night is settled — and the new floor is NOT yet dealt.
    /// A snapshot taken there, restored, deals the same floor from the same stream states and
    /// plays the rest of the run BIT FOR BIT as if it had never stopped (SaveTests pins it).
    /// Quitting mid-night therefore loses only that night: the resume replays it from its own
    /// morning, identically — the determinism promise wins over anti-save-scumming.
    ///
    /// The PCG stream states are carried as HEX STRINGS, never as numbers, and EVERY double
    /// rides as its IEEE-754 bits in a long: JsonUtility prints doubles at about fifteen
    /// digits, and the gold walk caught night two coming back with 0.1 for a satisfaction
    /// that was 0.09999999999999999 - a rounded book is a different run. The longs are exact.
    /// </summary>
    [Serializable]
    public sealed class RunSnapshot
    {
        /// <summary>The format's version. A reader refuses anything else — an old save is
        /// a save from a different game, and guessing at it would corrupt a run.</summary>
        public const int Version = 1;

        [Serializable] public sealed class StreamState { public string name; public string state; }
        [Serializable] public sealed class BottleState
        {
            public string id; public long capacityBits; public long pourRateBits; public int tier; public long remainingBits;
        }
        [Serializable] public sealed class GlassTierState { public string glassId; public int tier; }
        [Serializable] public sealed class WornState { public string slot; public string fixtureId; }
        [Serializable] public sealed class BestMakeState { public string recipeId; public long accuracyBits; public List<long> shareBits; }
        [Serializable] public sealed class PersonState
        {
            public string id; public string name; public string archetypeId; public int age; public string hometown;
            public int visits; public int satisfiedCount; public int satisfactionEarned; public bool barred;
            public bool hasPapers; public int trueAge; public int printedAge; public int forgery; public bool looksYoung;
        }
        [Serializable] public sealed class StoryPersonState { public string key; public PersonState person; }
        [Serializable] public sealed class LedgerDay
        {
            public int day; public int income; public int expenses; public long averageSatisfactionBits;
            public int sales; public int tips; public int rent; public int stock; public int upgrades;
            public int served; public int walkedOut; public long nightStarsBits; public int tillAfter; public bool hasDetail;
            public long serviceStarsBits; public long comfortStarsBits;
            public int fines; public int bonus; public int rightKicks; public int wrongKicks;
            public int minorsServed; public int minorsMet; public int walkOutFees; public int walkOutsCharged;
            public int questPaid;
        }
        /// <summary>The job on the bar (TycoonRun.Quests): its row by id, the drink she picked, and how far along.</summary>
        [Serializable] public sealed class QuestState
        {
            public bool has; public string id; public string recipeId; public int target; public int progress;
            public int reward; public int givenDay; public int doneDay;
        }

        public int version = Version;
        public string seed;
        /// <summary>The day about to open — the one the resume deals.</summary>
        public int day;
        public int money;
        public int seats;
        public int counterTier;
        public string counterFinish;
        public int crowdToday;
        public int declinedOrders;
        public int blowouts;
        /// <summary>The achievements' side of the run (TycoonRun.Feats, 2026-09-28): a dev verb touched it,
        /// and what the market sold it. Absent from older saves, which read as an untouched bar that bought
        /// nothing — the achievements then count from this dawn on.</summary>
        public bool devTouched;
        public int brandsBought;
        public int fittingsBought;
        public bool anyLicenceRead;

        public List<long> ratingNightBits;
        public long ratingStandingBits, ratingBestBits, ratingPreviousBestBits, ratingPreviousBits, ratingSumBits;
        public int ratingCount;

        /// <summary>The standing as a number, for the save file's head - display only.</summary>
        public double StandingValue => BitsToDouble(ratingStandingBits);

        /// <summary>A double, carried exactly (see the class remarks).</summary>
        public static long DoubleToBits(double value) => BitConverter.DoubleToInt64Bits(value);
        public static double BitsToDouble(long bits) => BitConverter.Int64BitsToDouble(bits);

        public List<LedgerDay> ledger;
        public int debtStrikes;
        public int tomorrowsCrowd;

        public List<string> menu;
        public List<string> boughtRecipes;
        public List<string> perfected;
        public List<BestMakeState> bestMakes;

        public List<BottleState> bottles;
        public List<string> catalogue;
        public List<string> lockedStock;
        public List<string> newStock;
        public List<int> iceRolls;

        public List<string> fixtures;
        public List<WornState> worn;
        public List<GlassTierState> glassTiers;

        /// <summary>
        /// THE HOSTESS'S BOOK (2026-09-27, TycoonRun.Quests), added without a version bump: 0 is a save written before
        /// the chain existed (JsonUtility leaves a missing field at its default), 1 is this layout. An old save starts
        /// the chain from its own dawn. The weekly job such a file may carry (job, jobDone, jobJustDone, jobGiver) has
        /// no field here since the job was deleted (2026-09-28), so JsonUtility skips those keys: a weekly job was paid
        /// the moment it finished, and nothing owed is lost. There is never a visit at dawn — the night cannot close
        /// under her — so none is carried.
        /// </summary>
        public int questFormat;
        public bool hasQuests;
        public int questNext, questVisitFrom, questsSkipped;
        /// <summary>Jobs finished over the run (2026-09-28, the game over's Z report). Added without a version bump
        /// either: a file written before it reads 0, and the restore counts the ledger's paid nights instead.</summary>
        public int questsDone;
        /// <summary>The next row BY ITS ID, which a data update cannot move: questNext is only the fallback (a
        /// book whose next row has gone, or a save written before 2026-09-28's review, which reads "").</summary>
        public string questNextId;
        public bool questDoneUnsaid;
        public QuestState quest;
        /// <summary>A state goal paid at this dawn, its "job done" flash not yet shown.</summary>
        public bool questJustDone;

        public bool hasStory;
        public int storyAt; public int storyDueDay; public int storyKept; public int storyMissed;
        public int storyTurnedAway; public bool storyCurrentAsked;
        public List<string> storyTaught;
        public List<string> storyKeptIds;
        public List<StoryPersonState> storyPeople;
        public List<string> lessonsQueued;
        /// <summary>The tours played (2026-09-29, TycoonRun.Tour: the house tour and the first-use lessons), so a resumed
        /// bar does not hear a lesson twice. Added without a version bump: an older file reads none.</summary>
        public List<string> toursTaught;

        public bool hasRegulars;
        public List<PersonState> people;
        public int nextSerial;

        public List<StreamState> streams;
    }

    public sealed partial class TycoonRun
    {
        /// <summary>
        /// The dawn instant, written down (see <see cref="RunSnapshot"/>). Private on purpose:
        /// the only caller is <see cref="ContinueToNextDay(Action{RunSnapshot})"/>, at the one
        /// instant the state is quiescent — no floor, no glass, nothing seated.
        /// </summary>
        private RunSnapshot BuildSnapshot()
        {
            var snap = new RunSnapshot
            {
                seed = _rng.Seed,
                day = Day,
                money = Money,
                seats = Seats,
                counterTier = CounterTier,
                counterFinish = CounterFinish,
                crowdToday = (int)CrowdToday,
                declinedOrders = DeclinedOrders,
                blowouts = Blowouts,
                devTouched = DevTouched,
                brandsBought = _brandsBought,
                fittingsBought = _fittingsBought,
                anyLicenceRead = _anyLicenceRead,
                debtStrikes = Ledger.DebtStrikes,
                tomorrowsCrowd = (int)Ledger.TomorrowsCrowd,
                menu = new List<string>(),
                boughtRecipes = new List<string>(_boughtRecipes),
                perfected = new List<string>(_perfectedRecipes),
                bestMakes = new List<RunSnapshot.BestMakeState>(),
                bottles = new List<RunSnapshot.BottleState>(),
                catalogue = new List<string>(),
                lockedStock = new List<string>(),
                newStock = new List<string>(_newStockIds),
                iceRolls = new List<int>(_iceRolls),
                fixtures = new List<string>(_fixtures),
                worn = new List<RunSnapshot.WornState>(),
                glassTiers = new List<RunSnapshot.GlassTierState>(),
                ledger = new List<RunSnapshot.LedgerDay>(),
                streams = new List<RunSnapshot.StreamState>(),
                lessonsQueued = new List<string>(),
            };

            Rating.CaptureInto(out var nights, out double standing, out double best,
                out double previousBest, out double previous, out snap.ratingCount, out double sum);
            snap.ratingStandingBits = RunSnapshot.DoubleToBits(standing);
            snap.ratingBestBits = RunSnapshot.DoubleToBits(best);
            snap.ratingPreviousBestBits = RunSnapshot.DoubleToBits(previousBest);
            snap.ratingPreviousBits = RunSnapshot.DoubleToBits(previous);
            snap.ratingSumBits = RunSnapshot.DoubleToBits(sum);
            snap.ratingNightBits = new List<long>(nights.Count);
            foreach (double night in nights) snap.ratingNightBits.Add(RunSnapshot.DoubleToBits(night));

            foreach (var row in Ledger.History)
                snap.ledger.Add(new RunSnapshot.LedgerDay
                {
                    day = row.Day, income = row.Income, expenses = row.Expenses,
                    averageSatisfactionBits = RunSnapshot.DoubleToBits(row.AverageSatisfaction),
                    sales = row.Sales, tips = row.Tips, rent = row.Rent, stock = row.Stock,
                    upgrades = row.Upgrades, served = row.Served, walkedOut = row.WalkedOut,
                    nightStarsBits = RunSnapshot.DoubleToBits(row.NightStars),
                    tillAfter = row.TillAfter, hasDetail = row.HasDetail,
                    serviceStarsBits = RunSnapshot.DoubleToBits(row.ServiceStars),
                    comfortStarsBits = RunSnapshot.DoubleToBits(row.ComfortStars),
                    fines = row.Fines, bonus = row.Bonus, rightKicks = row.RightKicks,
                    wrongKicks = row.WrongKicks, minorsServed = row.MinorsServed, minorsMet = row.MinorsMet,
                    walkOutFees = row.WalkOutFees, walkOutsCharged = row.WalkOutsCharged,
                    questPaid = row.QuestPaid,
                });

            foreach (var recipe in _recipes) snap.menu.Add(recipe.Id);
            foreach (var pair in _bestMakes)
            {
                var shareBits = new List<long>(pair.Value.Shares.Count);
                foreach (double share in pair.Value.Shares) shareBits.Add(RunSnapshot.DoubleToBits(share));
                snap.bestMakes.Add(new RunSnapshot.BestMakeState
                {
                    recipeId = pair.Key, accuracyBits = RunSnapshot.DoubleToBits(pair.Value.Accuracy),
                    shareBits = shareBits,
                });
            }

            foreach (var bottle in _shelf.Bottles)
                snap.bottles.Add(new RunSnapshot.BottleState
                {
                    id = bottle.Id, capacityBits = RunSnapshot.DoubleToBits(bottle.Capacity),
                    pourRateBits = RunSnapshot.DoubleToBits(bottle.PourRate),
                    tier = bottle.Tier, remainingBits = RunSnapshot.DoubleToBits(bottle.Remaining),
                });
            foreach (var card in _brandCatalogue) snap.catalogue.Add(card.Id);
            foreach (var card in _lockedStock) snap.lockedStock.Add(card.Id);

            foreach (var pair in _worn)
                snap.worn.Add(new RunSnapshot.WornState { slot = pair.Key, fixtureId = pair.Value });
            foreach (var pair in _glassTiers)
                snap.glassTiers.Add(new RunSnapshot.GlassTierState { glassId = pair.Key, tier = pair.Value });

            snap.questFormat = 1;
            snap.hasQuests = Quests != null;
            snap.questNext = _questNext;
            snap.questNextId = QuestNextUp?.Id ?? string.Empty;
            snap.questVisitFrom = _visitFrom;
            snap.questsSkipped = QuestsSkipped;
            snap.questsDone = QuestsDone;
            snap.questDoneUnsaid = _doneUnsaid;
            snap.quest = QuestStateOf(Quest);
            // Only ever the job on the bar: the flash is cleared at every hand-over (HearHostess).
            snap.questJustDone = QuestJustDone != null;

            snap.hasStory = Story != null;
            if (Story != null)
            {
                Story.CaptureInto(out snap.storyAt, out snap.storyDueDay, out snap.storyKept,
                    out snap.storyMissed, out snap.storyTurnedAway, out snap.storyCurrentAsked,
                    out var taught, out snap.storyKeptIds, out var storyPeople);
                snap.storyTaught = new List<string>();
                foreach (var cue in taught) snap.storyTaught.Add(cue.ToString());
                snap.storyPeople = new List<RunSnapshot.StoryPersonState>();
                foreach (var pair in storyPeople)
                    snap.storyPeople.Add(new RunSnapshot.StoryPersonState
                    {
                        key = pair.Key, person = PersonStateOf(pair.Value),
                    });
                foreach (var lesson in _lessons) snap.lessonsQueued.Add(lesson.Cue.ToString());
            }

            snap.toursTaught = ToursTaughtForSave();

            snap.hasRegulars = _regulars != null;
            if (_regulars != null)
            {
                snap.people = new List<RunSnapshot.PersonState>();
                foreach (var person in _regulars.All) snap.people.Add(PersonStateOf(person));
                snap.nextSerial = _regulars.NextSerial;
            }

            foreach (var pair in _rng.LiveStreams)
                snap.streams.Add(new RunSnapshot.StreamState
                {
                    name = pair.Key,
                    state = pair.Value.StateWord.ToString("x16", CultureInfo.InvariantCulture),
                });

            return snap;
        }

        private static RunSnapshot.QuestState QuestStateOf(ActiveQuest quest) => quest == null
            ? new RunSnapshot.QuestState()
            : new RunSnapshot.QuestState
            {
                has = true, id = quest.Id, recipeId = quest.RecipeId, target = quest.Target,
                progress = quest.Progress, reward = quest.Reward, givenDay = quest.GivenDay, doneDay = quest.DoneDay,
            };

        private static RunSnapshot.PersonState PersonStateOf(RegularState person)
        {
            var state = new RunSnapshot.PersonState
            {
                id = person.Id, name = person.Name, archetypeId = person.ArchetypeId,
                age = person.Age, hometown = person.Hometown,
                visits = person.Visits, satisfiedCount = person.SatisfiedCount,
                satisfactionEarned = person.SatisfactionEarned, barred = person.Barred,
                hasPapers = person.PapersRolled,
            };
            if (person.PapersRolled)
            {
                state.trueAge = person.Papers.TrueAge;
                state.printedAge = person.Papers.PrintedAge;
                state.forgery = (int)person.Papers.Forgery;
                state.looksYoung = person.Papers.LooksYoung;
            }
            return state;
        }

        /// <summary>
        /// A run stood back up from a snapshot, over the SAME content a fresh run loads —
        /// the snapshot carries ids and numbers only. Throws on a version it does not know
        /// or an id the content no longer has: a save that cannot be honoured exactly is
        /// refused whole, never patched (the caller turns that into "no save").
        /// The restored run stands at the open of <see cref="RunSnapshot.day"/>, its floor
        /// dealt from the restored stream states — bit for bit the run that was saved.
        /// </summary>
        public static TycoonRun Restore(RunSnapshot snap,
            IReadOnlyList<RecipeDefinition> recipes,
            IReadOnlyList<IngredientCard> allCards,
            TycoonConfig config = null,
            RegularsRegistry regulars = null,
            IReadOnlyList<GlasswareDefinition> glassware = null,
            IReadOnlyList<FixtureDefinition> fixtures = null,
            StoryArc story = null,
            QuestBook quests = null,
            TourBook tours = null)
        {
            if (snap == null) throw new ArgumentNullException(nameof(snap));
            if (snap.version != RunSnapshot.Version)
                throw new ArgumentException($"Save version {snap.version}; this game reads {RunSnapshot.Version}.");
            if (recipes == null) throw new ArgumentNullException(nameof(recipes));
            if (allCards == null) throw new ArgumentNullException(nameof(allCards));
            if (snap.hasStory != (story != null))
                throw new ArgumentException("The save and the content disagree about the story.");
            if (snap.hasRegulars != (regulars != null))
                throw new ArgumentException("The save and the content disagree about the regulars.");
            // A save from before the book (questFormat 0) lands in either kind of scene; one written since must agree.
            if (snap.questFormat >= 1 && snap.hasQuests != (quests != null))
                throw new ArgumentException("The save and the content disagree about the hostess's book.");

            var cardById = new Dictionary<string, IngredientCard>(StringComparer.Ordinal);
            foreach (var card in allCards)
                if (card != null && !cardById.ContainsKey(card.Id)) cardById[card.Id] = card;
            IngredientCard Card(string id) => cardById.TryGetValue(id ?? "", out var card)
                ? card : throw new ArgumentException($"Save names a bottle the data no longer has: '{id}'.");

            var recipeById = new Dictionary<string, RecipeDefinition>(StringComparer.Ordinal);
            foreach (var recipe in recipes)
                if (recipe != null) recipeById[recipe.Id] = recipe;
            RecipeDefinition Recipe(string id) => recipeById.TryGetValue(id ?? "", out var recipe)
                ? recipe : throw new ArgumentException($"Save names a page the data no longer has: '{id}'.");

            var bottles = new List<ShelfBottle>();
            foreach (var b in snap.bottles ?? throw new ArgumentException("Save has no shelf."))
                bottles.Add(new ShelfBottle(Card(b.id).Clone(), RunSnapshot.BitsToDouble(b.capacityBits),
                    RunSnapshot.BitsToDouble(b.pourRateBits), b.tier, RunSnapshot.BitsToDouble(b.remainingBits)));
            var catalogue = new List<IngredientCard>();
            foreach (var id in snap.catalogue ?? new List<string>()) catalogue.Add(Card(id));
            var locked = new List<IngredientCard>();
            foreach (var id in snap.lockedStock ?? new List<string>()) locked.Add(Card(id));

            // The ordinary constructor wires the content and deals a throwaway first floor;
            // everything it moved — including any stream it drew from — is overwritten below,
            // and the LAST thing restored is the streams, so nothing the constructor rolled
            // can leak into the resumed run.
            var run = new TycoonRun(new Shelf(bottles), recipes, new RunRng(snap.seed ?? string.Empty),
                config, regulars, catalogue, glassware, locked, fixtures, story, quests, tours);
            run.ApplySnapshot(snap, recipeById, Recipe);
            return run;
        }

        private void ApplySnapshot(RunSnapshot snap,
            Dictionary<string, RecipeDefinition> recipeById, Func<string, RecipeDefinition> recipe)
        {
            Day = snap.day;
            Money = snap.money;
            Seats = snap.seats;
            CounterTier = snap.counterTier;
            CounterFinish = string.IsNullOrEmpty(snap.counterFinish) ? CounterFinish : snap.counterFinish;
            CrowdToday = (WealthTier)snap.crowdToday;
            DeclinedOrders = snap.declinedOrders;
            Blowouts = snap.blowouts;
            _devTouched = snap.devTouched;
            _brandsBought = snap.brandsBought;
            _fittingsBought = snap.fittingsBought;
            _anyLicenceRead = snap.anyLicenceRead;

            var ratingNights = new List<double>(snap.ratingNightBits != null ? snap.ratingNightBits.Count : 0);
            if (snap.ratingNightBits != null)
                foreach (long bits in snap.ratingNightBits) ratingNights.Add(RunSnapshot.BitsToDouble(bits));
            Rating.RestoreFrom(ratingNights, RunSnapshot.BitsToDouble(snap.ratingStandingBits),
                RunSnapshot.BitsToDouble(snap.ratingBestBits), RunSnapshot.BitsToDouble(snap.ratingPreviousBestBits),
                RunSnapshot.BitsToDouble(snap.ratingPreviousBits), snap.ratingCount,
                RunSnapshot.BitsToDouble(snap.ratingSumBits));

            var history = new List<DayResult>();
            foreach (var row in snap.ledger ?? new List<RunSnapshot.LedgerDay>())
                history.Add(new DayResult(row.day, row.income, row.expenses,
                    RunSnapshot.BitsToDouble(row.averageSatisfactionBits),
                    row.sales, row.tips, row.rent, row.stock, row.upgrades,
                    row.served, row.walkedOut, RunSnapshot.BitsToDouble(row.nightStarsBits),
                    row.tillAfter, row.hasDetail,
                    RunSnapshot.BitsToDouble(row.serviceStarsBits), RunSnapshot.BitsToDouble(row.comfortStarsBits),
                    row.fines, row.bonus, row.rightKicks, row.wrongKicks,
                    row.minorsServed, row.minorsMet, row.walkOutFees, row.walkOutsCharged, row.questPaid));
            Ledger.RestoreFrom(history, snap.debtStrikes, (WealthTier)snap.tomorrowsCrowd);

            _recipes.Clear();
            foreach (var id in snap.menu ?? new List<string>()) _recipes.Add(recipe(id));
            _boughtRecipes.Clear();
            foreach (var id in snap.boughtRecipes ?? new List<string>()) _boughtRecipes.Add(recipe(id).Id);
            _perfectedRecipes.Clear();
            foreach (var id in snap.perfected ?? new List<string>()) _perfectedRecipes.Add(recipe(id).Id);
            _bestMakes.Clear();
            foreach (var make in snap.bestMakes ?? new List<RunSnapshot.BestMakeState>())
            {
                var shares = new List<double>(make.shareBits != null ? make.shareBits.Count : 0);
                if (make.shareBits != null)
                    foreach (long bits in make.shareBits) shares.Add(RunSnapshot.BitsToDouble(bits));
                _bestMakes[recipe(make.recipeId).Id] =
                    new RecipeBestMake(RunSnapshot.BitsToDouble(make.accuracyBits), shares);
            }

            _newStockIds.Clear();
            foreach (var id in snap.newStock ?? new List<string>()) _newStockIds.Add(id);
            _iceRolls.Clear();
            if (snap.iceRolls != null) _iceRolls.AddRange(snap.iceRolls);

            _fixtures.Clear();
            foreach (var id in snap.fixtures ?? new List<string>()) _fixtures.Add(id);
            _worn.Clear();
            foreach (var wear in snap.worn ?? new List<RunSnapshot.WornState>()) _worn[wear.slot] = wear.fixtureId;
            _glassTiers.Clear();
            foreach (var tier in snap.glassTiers ?? new List<RunSnapshot.GlassTierState>())
                _glassTiers[tier.glassId] = tier.tier;
            RoomChanged();

            if (snap.questFormat >= 1)
            {
                Quest = null;
                if (snap.quest != null && snap.quest.has)
                {
                    var def = Quests?.Find(snap.quest.id)
                              ?? throw new ArgumentException($"Save names a job the book no longer has: '{snap.quest.id}'.");
                    string recipeName = string.Empty;
                    if (!string.IsNullOrEmpty(snap.quest.recipeId)) recipeName = recipe(snap.quest.recipeId).Name;
                    Quest = new ActiveQuest(def, snap.quest.recipeId, recipeName, snap.quest.target, 0,
                        snap.quest.reward, snap.quest.givenDay);
                    Quest.Restore(snap.quest.progress, snap.quest.doneDay);
                }
                // THE NEXT ROW BY ITS ID, AND NEVER AT OR BEHIND THE JOB ON THE BAR (2026-09-28, the review): an index
                // alone points one row early once a data update puts a row in front of the job, and she would hand the
                // same job over, and pay it, twice. The id survives rows put in or taken out before it; the floor is
                // the job's own row, re-found by id above.
                var nextRow = Quests?.Find(snap.questNextId);
                _questNext = Math.Max(nextRow != null ? nextRow.Index : snap.questNext,
                    Quest != null ? Quest.Definition.Index + 1 : 0);
                _visitFrom = snap.questVisitFrom;
                _doneUnsaid = snap.questDoneUnsaid;
                QuestsSkipped = snap.questsSkipped;
                // A save from before the count (2026-09-28) reads 0: the ledger, restored above, knows which nights
                // she paid — one job a night at most, since she hands the next over no sooner than the night after a pay.
                QuestsDone = snap.questsDone;
                if (QuestsDone == 0)
                    foreach (var row in Ledger.History)
                        if (row.QuestPaid > 0) QuestsDone++;
                QuestJustDone = snap.questJustDone ? Quest : null;
            }
            else
            {
                // A SAVE FROM BEFORE THE BOOK (2026-09-27): the chain starts at this dawn, from its first row. Set
                // explicitly — the constructor scheduled her for night one, and the dawn's self-healing rule only
                // fires on an unscheduled visit, so it would never correct a run restored at night twelve.
                Quest = null;
                _questNext = 0;
                _doneUnsaid = false;
                QuestsSkipped = 0;
                QuestsDone = 0;            // the book starts at this dawn; nothing of hers was done before it
                QuestJustDone = null;
                _visitFrom = QuestsLive ? Day : 0;
            }
            HostessVisit = null;
            QuestJustGiven = null;
            _visitedTonight = false;
            _visitWaited = 0;

            RestoreToursTaught(snap.toursTaught);

            if (Story != null)
            {
                var people = new List<KeyValuePair<string, RegularState>>();
                foreach (var entry in snap.storyPeople ?? new List<RunSnapshot.StoryPersonState>())
                    people.Add(new KeyValuePair<string, RegularState>(entry.key, PersonFrom(entry.person)));
                var taught = new List<StoryCue>();
                foreach (var name in snap.storyTaught ?? new List<string>())
                    taught.Add((StoryCue)Enum.Parse(typeof(StoryCue), name));
                Story.RestoreFrom(snap.storyAt, snap.storyDueDay, snap.storyKept, snap.storyMissed,
                    snap.storyTurnedAway, snap.storyCurrentAsked, taught, snap.storyKeptIds, people);
                // The constructor's TeachAtOpen queued day one's lesson; the queue is the
                // save's now, and the reopened day queues its own below, exactly as the
                // uninterrupted run did at this same dawn.
                _lessons.Clear();
                foreach (var name in snap.lessonsQueued ?? new List<string>())
                {
                    var cue = (StoryCue)Enum.Parse(typeof(StoryCue), name);
                    StoryLesson waiting = null;
                    foreach (var lesson in Story.Arc.Lessons)
                        if (lesson.Cue == cue) { waiting = lesson; break; }
                    if (waiting == null)
                        throw new ArgumentException($"Save queues a lesson the story no longer writes: '{name}'.");
                    _lessons.Enqueue(waiting);
                }
            }

            if (_regulars != null)
            {
                var people = new List<RegularState>();
                foreach (var state in snap.people ?? new List<RunSnapshot.PersonState>())
                    people.Add(PersonFrom(state));
                _regulars.RestoreFrom(people, snap.nextSerial);
            }

            // The streams come LAST: everything above re-derives, and the constructor's own
            // throwaway deal drew from fresh streams — this puts every live stream exactly
            // where the saved run's stood at its dawn.
            foreach (var stream in snap.streams ?? new List<RunSnapshot.StreamState>())
                _rng.GetStream(stream.name).RestoreState(
                    ulong.Parse(stream.state, NumberStyles.HexNumber, CultureInfo.InvariantCulture));

            // The one repair a restore makes (spec A.3): a serve job whose drink the data update made unpourable is
            // pointed at the first page of the same rule. No stream is drawn, so the run stays the saved run.
            RepairTheQuestAfterRestore();

            // And the dawn's own tail, the same three lines ContinueToNextDay runs after the
            // save point: fresh vessels, the new floor off the restored streams, the open.
            ResetVessels();
            _marketOffers.Clear();
            _marketOffers.AddRange(Market.OffersFor(_shelf, _brandCatalogue, ShopStars, this));
            Floor = NewFloor(Rating.Average);
            Phase = TycoonPhase.DayOpen;
            TeachAtOpen();
        }

        private static RegularState PersonFrom(RunSnapshot.PersonState state)
        {
            var person = new RegularState(state.id, state.name, state.archetypeId, state.age, state.hometown);
            IdPapers papers = null;
            if (state.hasPapers)
                papers = new IdPapers(state.trueAge, state.printedAge, (Forgery)state.forgery, state.looksYoung);
            person.RestoreVisits(state.visits, state.satisfiedCount, state.satisfactionEarned, papers, state.barred);
            return person;
        }
    }
}
