using System;
using System.Collections.Generic;
using System.Globalization;

namespace LastCall.Core
{
    /// <summary>
    /// WHAT THE HOSTESS CAN ASK FOR (2026-09-27, the author: "görevler yazılı bir sırayla gelsin ... mekanik basit ve
    /// sağlam kalsın"). Nine kinds, and every one of them is something Core already measures: what went out and how
    /// well (four serve kinds), how the night ended (clean), what the door did (door), and three things the bar IS
    /// rather than does (rank, comfort, fit), which are read at dawn.
    /// </summary>
    public enum QuestKind
    {
        /// <summary>N of the drink picked when she arrived, exactly as ordered.</summary>
        Serve,
        /// <summary>N drinks poured inside every perfect window, whatever they were.</summary>
        Perfect,
        /// <summary>N pints with the head in the good band.</summary>
        Pints,
        /// <summary>N drinks whose order asked for one of her preparations, served with every ask on the glass.</summary>
        Garnish,
        /// <summary>N nights with nobody walking out and nothing wrong served.</summary>
        Clean,
        /// <summary>N faces rightly shown the door.</summary>
        Door,
        /// <summary>The ladder's rung reached (the high-water mark, so it never falls back).</summary>
        Rank,
        /// <summary>The room's comfort, as its fittings stand.</summary>
        Comfort,
        /// <summary>A rung of one slot's ladder owned.</summary>
        Fit,
    }

    /// <summary>How a <see cref="QuestKind.Serve"/> quest picks its drink when she arrives (spec A.3).</summary>
    public enum QuestPick
    {
        /// <summary>Any pourable authored page, drawn on the "quest" stream.</summary>
        Any,
        /// <summary>A pourable stirred page, drawn on the "quest" stream.</summary>
        Stirred,
        /// <summary>The pourable page of the highest rank, ties to the earliest on the menu; no draw.</summary>
        Top,
    }

    /// <summary>
    /// One row of the hostess's book (<c>Resources/Data/quests.json</c>), immutable. Everything a row can get wrong
    /// that can be checked without the fixture catalogue is refused HERE, in Core's words, so a bench test can ask the
    /// same question the loader asks (the loader wraps it in a <see cref="FormatException"/> naming the row). The one
    /// rule that needs the catalogue — the piece a <see cref="QuestKind.Fit"/> row names — is
    /// <see cref="QuestRules.ResolveFit"/>.
    /// </summary>
    public sealed class QuestDefinition
    {
        public string Id { get; }

        /// <summary>Its place in the book, 0-based; set when the book is bound.</summary>
        public int Index { get; internal set; } = -1;

        public QuestKind Kind { get; }

        /// <summary>The ladder rung the bar must stand on before she hands it over (0-6).</summary>
        public int Rung { get; }

        /// <summary>How many — 1 for the state kinds, which are met or not.</summary>
        public int Target { get; }

        /// <summary>What finishing it pays, in dollars, written in the data.</summary>
        public int Reward { get; }

        /// <summary>The job's name on the bubble (<c>data.quest.&lt;id&gt;.title</c>).</summary>
        public string Title { get; }

        /// <summary>What she says when she hands it over. The LAST line is the one the bubble quotes.</summary>
        public IReadOnlyList<string> HandOver { get; }

        /// <summary>What she says the night after it is done.</summary>
        public IReadOnlyList<string> Done { get; }

        /// <summary>Serve only: how the drink is picked.</summary>
        public QuestPick Pick { get; }

        /// <summary>Garnish only: the one or two preparations an order has to have asked for.</summary>
        public IReadOnlyList<PreparationDefinition> Preps { get; }

        /// <summary>Rank only: the rung to reach (1-6).</summary>
        public int GoalRung { get; }

        /// <summary>Comfort only: the room's comfort to reach, (0, 5].</summary>
        public double GoalComfort { get; }

        /// <summary>Fit only: the slot, the rung on its ladder, and the catalogue piece standing on that rung.</summary>
        public string Slot { get; }
        public int Level { get; }
        public string FixtureId { get; }

        /// <summary>Met or not, read at dawn: nothing is counted towards them.</summary>
        public bool IsStateGoal => QuestRules.IsStateGoal(Kind);

        /// <summary>Counted on the drink that goes over the bar.</summary>
        public bool CountsServes => QuestRules.CountsServes(Kind);

        public QuestDefinition(string id, QuestKind kind, int rung, int target, int reward, string title,
            IReadOnlyList<string> handOver, IReadOnlyList<string> done,
            QuestPick pick = QuestPick.Any, IReadOnlyList<PreparationDefinition> preps = null,
            int goalRung = 0, double goalComfort = 0, string slot = null, int level = 0, string fixtureId = null)
        {
            if (string.IsNullOrEmpty(id) || !QuestRules.IsLegalId(id))
                throw new ArgumentException(
                    $"a job's id is lower-case letters, digits and underscores (the loc key's shape), not '{id}'.", nameof(id));
            Id = id;
            string who = $"Job '{id}'";
            if (rung < 0 || rung >= BarRank.Rungs.Count)
                throw new ArgumentOutOfRangeException(nameof(rung), $"{who} stands on rung {rung}; the ladder runs 0-{BarRank.Rungs.Count - 1}.");
            if (reward < 1 || reward > QuestRules.MaxReward)
                throw new ArgumentOutOfRangeException(nameof(reward), $"{who} pays ${reward}; a job pays $1-${QuestRules.MaxReward}.");
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException($"{who} has no title for the bubble to print.", nameof(title));
            if (title.Length > QuestRules.MaxTitleLength)
                throw new ArgumentException($"{who}'s title '{title}' is {title.Length} letters; the bubble prints {QuestRules.MaxTitleLength}.", nameof(title));
            if (title.IndexOf('{') >= 0 || title.IndexOf('}') >= 0)
                throw new ArgumentException($"{who}'s title '{title}' carries a brace; nothing is filled into a title.", nameof(title));

            Kind = kind;
            Rung = rung;
            Reward = reward;
            Title = title;

            // THE COUNT KINDS ARE COUNTED ON PIPS (the bubble's reach is twelve); the state kinds are met or not.
            if (QuestRules.IsStateGoal(kind))
            {
                if (target != 0 && target != 1)
                    throw new ArgumentOutOfRangeException(nameof(target), $"{who} is a {QuestRules.KindName(kind)} job, met or not; its target is 0 or 1, not {target}.");
                Target = 1;
            }
            else
            {
                if (target < 1 || target > QuestRules.MaxCountTarget)
                    throw new ArgumentOutOfRangeException(nameof(target), $"{who} asks for {target}; a count is 1-{QuestRules.MaxCountTarget}.");
                Target = target;
            }

            HandOver = Lines(handOver, 1, 4, who, "handOver");
            if (HandOver[HandOver.Count - 1].Length > QuestRules.MaxQuotedLineLength)
                throw new ArgumentException(
                    $"{who}'s last handOver line is {HandOver[HandOver.Count - 1].Length} letters; the bubble quotes it and holds {QuestRules.MaxQuotedLineLength}.", nameof(handOver));
            Done = Lines(done, 1, 2, who, "done");
            foreach (var line in HandOver) CheckSlots(line, kind, who, "handOver");
            foreach (var line in Done) CheckSlots(line, kind, who, "done");

            // ── what belongs to one kind only ────────────────────────────────────────
            Pick = kind == QuestKind.Serve ? pick : QuestPick.Any;
            if (kind == QuestKind.Serve && pick == QuestPick.Stirred)
            {
                int spoon = BarRank.Granting(Feature.Spoon).Index;
                if (rung < spoon)
                    throw new ArgumentException($"{who} asks for a stirred drink on rung {rung}; the spoon is on rung {spoon}.", nameof(rung));
            }

            if (kind == QuestKind.Garnish)
            {
                if (preps == null || preps.Count < 1 || preps.Count > 2)
                    throw new ArgumentException($"{who} is a garnish job and names {preps?.Count ?? 0} preparations; it names one or two.", nameof(preps));
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in preps)
                {
                    if (p == null) throw new ArgumentException($"{who} names an empty preparation.", nameof(preps));
                    if (!seen.Add(p.Id)) throw new ArgumentException($"{who} names '{p.Id}' twice.", nameof(preps));
                    // A JAR THE SHOP CAN WITHHOLD MAKES THE JOB IMPOSSIBLE: olives and mint are on the rail only while the
                    // market's jar is on the shelf, so a job asking for them could stand on the bar with no way to do it.
                    if (!QuestRules.GarnishAllowed(p))
                        throw new ArgumentException(
                            $"{who} asks for '{p.Id}'; a garnish job asks for ice, lemon_twist, salt_rim or sugar_rim (a jar the shop can withhold would make it impossible).", nameof(preps));
                    int needs = QuestRules.RungOf(p);
                    if (rung < needs)
                        throw new ArgumentException($"{who} asks for '{p.Id}' on rung {rung}; the counter has it from rung {needs}.", nameof(rung));
                }
                Preps = new List<PreparationDefinition>(preps);
            }
            else Preps = Array.Empty<PreparationDefinition>();

            if (kind == QuestKind.Door)
            {
                int door = BarRank.Granting(Feature.Door).Index;
                if (rung < door)
                    throw new ArgumentException($"{who} asks for the door on rung {rung}; the door is the bar's from rung {door}.", nameof(rung));
            }

            if (kind == QuestKind.Rank)
            {
                if (goalRung < 1 || goalRung >= BarRank.Rungs.Count)
                    throw new ArgumentOutOfRangeException(nameof(goalRung), $"{who} aims for rung {goalRung}; a rank job aims for 1-{BarRank.Rungs.Count - 1}.");
                if (goalRung <= rung)
                    throw new ArgumentException($"{who} is handed over on rung {rung} and aims for rung {goalRung}; it would be done before it was given.", nameof(goalRung));
                GoalRung = goalRung;
            }

            if (kind == QuestKind.Comfort)
            {
                if (!(goalComfort > 0) || goalComfort > VenueComfort.MaxComfort + 1e-9)
                    throw new ArgumentOutOfRangeException(nameof(goalComfort), $"{who} aims the room at {goalComfort}; comfort runs above 0 to {VenueComfort.MaxComfort}.");
                GoalComfort = goalComfort;
            }

            if (kind == QuestKind.Fit)
            {
                if (string.IsNullOrWhiteSpace(slot))
                    throw new ArgumentException($"{who} is a fit job and names no slot.", nameof(slot));
                if (level < 1)
                    throw new ArgumentOutOfRangeException(nameof(level), $"{who} asks for rung {level} of '{slot}'; a ladder starts at 1.");
                if (string.IsNullOrWhiteSpace(fixtureId))
                    throw new ArgumentException($"{who} names no piece for rung {level} of '{slot}'.", nameof(fixtureId));
                Slot = slot;
                Level = level;
                FixtureId = fixtureId;
            }
        }

        private static IReadOnlyList<string> Lines(IReadOnlyList<string> lines, int min, int max, string who, string field)
        {
            if (lines == null || lines.Count < min || lines.Count > max)
                throw new ArgumentException($"{who} has {lines?.Count ?? 0} {field} lines; it has {min}-{max}.", field);
            foreach (var line in lines)
                if (string.IsNullOrWhiteSpace(line))
                    throw new ArgumentException($"{who} has a blank {field} line.", field);
            return new List<string>(lines);
        }

        /// <summary>The slots a line may carry: her name anywhere, the drink on a serve job, the count on a count job.</summary>
        private static void CheckSlots(string line, QuestKind kind, string who, string field)
        {
            foreach (var slot in QuestRules.SlotsIn(line, who, field))
            {
                if (slot == "name") continue;
                if (slot == "drink" && kind == QuestKind.Serve) continue;
                if (slot == "n" && !QuestRules.IsStateGoal(kind)) continue;
                throw new ArgumentException(
                    $"{who}'s {field} line \"{line}\" carries {{{slot}}}; a {QuestRules.KindName(kind)} job's lines may use " +
                    (kind == QuestKind.Serve ? "{name}, {drink} and {n}." : QuestRules.IsStateGoal(kind) ? "{name} only." : "{name} and {n}."),
                    field);
            }
        }

        public override string ToString() => $"{Id} ({QuestRules.KindName(Kind)}, rung {Rung})";
    }

    /// <summary>
    /// THE HOSTESS'S BOOK: every job, in the order she hands them over — the same order every run — and the two
    /// things she says that belong to no job (the finale, and "not yet" when the next job waits on a rung).
    /// </summary>
    public sealed class QuestBook
    {
        public IReadOnlyList<QuestDefinition> Quests { get; }
        public int Count => Quests.Count;
        public QuestDefinition this[int index] => Quests[index];

        /// <summary>What she says after the last job is done (visits.finale).</summary>
        public IReadOnlyList<string> Finale { get; }

        /// <summary>What she says when the next job waits on a rung the bar has not reached (visits.not_yet); carries {rung}.</summary>
        public string NotYet { get; }

        public QuestBook(IReadOnlyList<QuestDefinition> quests, IReadOnlyList<string> finale, string notYet)
        {
            if (quests == null || quests.Count < 1 || quests.Count > QuestRules.MaxQuests)
                throw new ArgumentException($"The book holds {quests?.Count ?? 0} jobs; it holds 1-{QuestRules.MaxQuests}.", nameof(quests));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int lastRung = 0;
            for (int i = 0; i < quests.Count; i++)
            {
                var q = quests[i] ?? throw new ArgumentException($"The book has an empty row at {i}.", nameof(quests));
                if (!ids.Add(q.Id))
                    throw new ArgumentException($"Job '{q.Id}' is in the book twice.", nameof(quests));
                // THE LADDER IS CLIMBED IN ORDER: a job that asks for less of the bar than the one before it would be
                // handed over after a rung-gated wait it never needed.
                if (q.Rung < lastRung)
                    throw new ArgumentException(
                        $"Job '{q.Id}' stands on rung {q.Rung} after a rung-{lastRung} job; rungs never go down the book.", nameof(quests));
                lastRung = q.Rung;
                if (q.Index >= 0 && q.Index != i)
                    throw new ArgumentException($"Job '{q.Id}' is already row {q.Index} of another book.", nameof(quests));
            }

            if (finale == null || finale.Count < 1 || finale.Count > 3)
                throw new ArgumentException($"The finale has {finale?.Count ?? 0} lines; it has 1-3.", nameof(finale));
            foreach (var line in finale)
            {
                if (string.IsNullOrWhiteSpace(line)) throw new ArgumentException("The finale has a blank line.", nameof(finale));
                foreach (var slot in QuestRules.SlotsIn(line, "The finale", "say"))
                    if (slot != "name")
                        throw new ArgumentException($"The finale line \"{line}\" carries {{{slot}}}; it may use {{name}} only.", nameof(finale));
            }
            if (string.IsNullOrWhiteSpace(notYet))
                throw new ArgumentException("The book has no \"not yet\" line.", nameof(notYet));
            bool rungSlot = false;
            foreach (var slot in QuestRules.SlotsIn(notYet, "The \"not yet\" line", "say"))
            {
                if (slot == "rung") rungSlot = true;
                else if (slot != "name")
                    throw new ArgumentException($"The \"not yet\" line carries {{{slot}}}; it may use {{name}} and {{rung}}.", nameof(notYet));
            }
            if (!rungSlot)
                throw new ArgumentException("The \"not yet\" line never says {rung}: she has to say what the bar is short of.", nameof(notYet));

            for (int i = 0; i < quests.Count; i++) quests[i].Index = i;
            Quests = new List<QuestDefinition>(quests);
            Finale = new List<string>(finale);
            NotYet = notYet;
        }

        /// <summary>The row with this id, or null.</summary>
        public QuestDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var q in Quests)
                if (q.Id == id) return q;
            return null;
        }
    }

    /// <summary>
    /// THE JOB ON THE BAR: one row of the book as she handed it over — the drink she picked for it, if it names one,
    /// and how far along it is. It stands until the next hand-over replaces it, done or not.
    /// </summary>
    public sealed class ActiveQuest
    {
        public QuestDefinition Definition { get; }
        public string Id => Definition.Id;
        public QuestKind Kind => Definition.Kind;

        /// <summary>The drink a serve job asks for; empty for every other kind.</summary>
        public string RecipeId { get; private set; }

        /// <summary>Its name, kept so a caller needs no catalogue; empty for every other kind.</summary>
        public string RecipeName { get; private set; }

        public int Target { get; }
        public int Progress { get; private set; }
        public int Reward { get; }

        /// <summary>The night she handed it over (a dev verb that winds the calendar back brings it back with it).</summary>
        public int GivenDay { get; internal set; }

        /// <summary>The night it was done (for a state goal, the night the dawn filed); 0 while it is not.</summary>
        public int DoneDay { get; internal set; }

        public bool IsDone => Progress >= Target;

        /// <summary>What is still owed, never below zero.</summary>
        public int Left => Math.Max(0, Target - Progress);

        public ActiveQuest(QuestDefinition definition, string recipeId, string recipeName, int target, int progress,
            int reward, int givenDay)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (definition.Kind == QuestKind.Serve && string.IsNullOrWhiteSpace(recipeId))
                throw new ArgumentException($"Job '{definition.Id}' is a serve job and names no drink.", nameof(recipeId));
            if (target < 1) throw new ArgumentOutOfRangeException(nameof(target));
            if (reward < 0) throw new ArgumentOutOfRangeException(nameof(reward));
            RecipeId = definition.Kind == QuestKind.Serve ? recipeId : string.Empty;
            RecipeName = definition.Kind == QuestKind.Serve
                ? (string.IsNullOrWhiteSpace(recipeName) ? recipeId : recipeName)
                : string.Empty;
            Target = target;
            Reward = reward;
            GivenDay = givenDay;
            Progress = Math.Max(0, Math.Min(target, progress));
        }

        /// <summary>One more towards it. True on the unit that finishes it; a done job counts nothing.</summary>
        internal bool Count()
        {
            if (IsDone) return false;
            Progress++;
            return IsDone;
        }

        /// <summary>The save layer: the count and the night it was done, put back without replaying them.</summary>
        internal void Restore(int progress, int doneDay)
        {
            Progress = Math.Max(0, Math.Min(Target, progress));
            DoneDay = Math.Max(0, doneDay);
        }

        /// <summary>THE RESTORE-TIME REPAIR (spec A.3), and nothing else: a drink that stopped being pourable across a
        /// data update is swapped for the first page of the same rule, and the count starts again.</summary>
        internal void Repick(string recipeId, string recipeName)
        {
            RecipeId = recipeId ?? string.Empty;
            RecipeName = string.IsNullOrWhiteSpace(recipeName) ? RecipeId : recipeName;
            Progress = 0;
            DoneDay = 0;
        }

        /// <summary>
        /// What is still owed, as a string-table line: an instruction, not a scoreboard ("3 MORE NEGRONI" — the drink
        /// is its page's name, never a plural; "REACH THE TALK OF THE TOWN"). Data names ride as their data lines, so
        /// render it in capitals to read as the table does.
        /// </summary>
        public Line OwedLine()
        {
            var def = Definition;
            int left = Left;
            switch (def.Kind)
            {
                case QuestKind.Serve:
                    return Line.Of("quest.owed.serve").Counting("n", left)
                        .With("drink", Line.Of("data.recipe." + RecipeId + ".name"));
                case QuestKind.Garnish:
                    return def.Preps.Count > 1
                        ? Line.Of("quest.owed.garnish2").Counting("n", left)
                            .With("prep", def.Preps[0].NameLine).With("prep2", def.Preps[1].NameLine)
                        : Line.Of("quest.owed.garnish").Counting("n", left).With("prep", def.Preps[0].NameLine);
                case QuestKind.Rank:
                    return Line.Of("quest.owed.rank").With("title", BarRank.Rungs[def.GoalRung].Title);
                case QuestKind.Comfort:
                    return Line.Of("quest.owed.comfort")
                        .With("goal", def.GoalComfort.ToString("0.00", CultureInfo.InvariantCulture));
                case QuestKind.Fit:
                    return Line.Of("quest.owed.fit").With("piece", Line.Of("data.fixture." + def.FixtureId + ".name"));
                default:
                    return Line.Of("quest.owed." + QuestRules.KindName(def.Kind)).Counting("n", left);
            }
        }

        public override string ToString() =>
            $"{Id} {Progress}/{Target}" + (Kind == QuestKind.Serve ? $" ({RecipeId})" : "") + (IsDone ? " done" : "");
    }

    /// <summary>
    /// SHE IS IN THE ROOM (spec B.3): what she came to say, decided the moment she walked in and never re-rolled. Non-null
    /// on the run from her arrival at closing time until somebody has heard her out (<see cref="TycoonRun.HearHostess"/>).
    /// </summary>
    public sealed class HostessVisit
    {
        /// <summary>The night she came.</summary>
        public int Day { get; }

        /// <summary>The job she is here to thank the bar for, or null.</summary>
        public ActiveQuest Finished { get; }

        /// <summary>The job she brings, or null.</summary>
        public ActiveQuest Offered { get; }

        /// <summary>The book is done: this is the last visit with anything in it.</summary>
        public bool Finale { get; }

        /// <summary>The rung the next job waits on, when that is why she brings nothing; else null.</summary>
        public Rung WaitingFor { get; }

        /// <summary>One visit, one plate: the UI builds her lines once per key.</summary>
        public string Key => $"{Day}:{Finished?.Id}:{Offered?.Id}:{Finale}";

        internal HostessVisit(int day, ActiveQuest finished, ActiveQuest offered, bool finale, Rung waitingFor)
        {
            Day = day;
            Finished = finished;
            Offered = offered;
            Finale = finale;
            WaitingFor = waitingFor;
        }
    }

    /// <summary>The chain's rules that need no run, in one place so the tests can pin them without one.</summary>
    public static class QuestRules
    {
        /// <summary>The night at whose close she first comes. A run that opens on the house tour (TycoonRun.Tour) has her
        /// hand this first job over as the tour ends instead - the same visit, said early, on the same night.</summary>
        public const int FirstVisitNight = 1;

        /// <summary>Floor-seconds, unheld, before Core hands the job over by itself: a run nobody watches still gets it.</summary>
        public const double HostessGraceSeconds = 4.0;

        public const int MaxQuests = 40;
        public const int MaxCountTarget = 12;
        public const int MaxReward = 1000;
        public const int MaxTitleLength = 24;
        public const int MaxQuotedLineLength = 120;

        private static readonly string[] KindNames =
            { "serve", "perfect", "pints", "garnish", "clean", "door", "rank", "comfort", "fit" };

        /// <summary>The nine kinds as the data file writes them.</summary>
        public static IReadOnlyList<string> KindNamesInData => KindNames;

        public static string KindName(QuestKind kind) => KindNames[(int)kind];

        /// <summary>The kind the data file names, or null for a word it does not know. Lower case only.</summary>
        public static QuestKind? ParseKind(string raw)
        {
            for (int i = 0; i < KindNames.Length; i++)
                if (string.Equals(raw, KindNames[i], StringComparison.Ordinal)) return (QuestKind)i;
            return null;
        }

        /// <summary>A serve job's pick as the data file writes it: "" or "any", "stirred", "top"; null for anything else.</summary>
        public static QuestPick? ParsePick(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw == "any") return QuestPick.Any;
            if (raw == "stirred") return QuestPick.Stirred;
            if (raw == "top") return QuestPick.Top;
            return null;
        }

        public static bool IsStateGoal(QuestKind kind) =>
            kind == QuestKind.Rank || kind == QuestKind.Comfort || kind == QuestKind.Fit;

        public static bool CountsServes(QuestKind kind) =>
            kind == QuestKind.Serve || kind == QuestKind.Perfect || kind == QuestKind.Pints || kind == QuestKind.Garnish;

        /// <summary>The loc key's shape: lower-case letters, digits, underscores.</summary>
        public static bool IsLegalId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (char c in id)
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_') return false;
            return true;
        }

        /// <summary>The four the counter always carries once its rung is reached: never a jar the shop can withhold.</summary>
        public static bool GarnishAllowed(PreparationDefinition p) =>
            p != null && (p.Id == "ice" || p.Id == "lemon_twist" || p.Id == "salt_rim" || p.Id == "sugar_rim");

        /// <summary>The rung a preparation opens on (0 for one the ladder never gated).</summary>
        public static int RungOf(PreparationDefinition p)
        {
            var gate = BarRank.Gating(p);
            return gate == null ? 0 : BarRank.Granting(gate.Value).Index;
        }

        /// <summary>
        /// The {slots} in a line, in order. A brace that opens no slot, or a slot that never closes, is refused — the
        /// line would print its own punctuation on the plate.
        /// </summary>
        public static List<string> SlotsIn(string line, string who, string field)
        {
            var slots = new List<string>();
            if (string.IsNullOrEmpty(line)) return slots;
            int i = 0;
            while (i < line.Length)
            {
                char c = line[i];
                if (c == '}')
                    throw new ArgumentException($"{who}'s {field} line \"{line}\" closes a brace it never opened.", field);
                if (c != '{') { i++; continue; }
                int close = line.IndexOf('}', i + 1);
                int reopen = line.IndexOf('{', i + 1);
                if (close < 0 || (reopen >= 0 && reopen < close))
                    throw new ArgumentException($"{who}'s {field} line \"{line}\" opens a brace it never closes.", field);
                string slot = line.Substring(i + 1, close - i - 1);
                if (slot.Length == 0)
                    throw new ArgumentException($"{who}'s {field} line \"{line}\" has an empty {{}}.", field);
                slots.Add(slot);
                i = close + 1;
            }
            return slots;
        }

        /// <summary>
        /// THE PIECE A FIT JOB NAMES (spec A.1): the catalogue piece on rung <paramref name="level"/> of
        /// <paramref name="slot"/>'s ladder — which the shop must sell to a bar standing on the job's rung, or the job
        /// could stand on the bar with nothing to buy. Throws, naming the job, when either is not so.
        /// </summary>
        public static FixtureDefinition ResolveFit(IReadOnlyList<FixtureDefinition> fixtures, string questId,
            string slot, int level, int rung)
        {
            string who = $"Job '{questId}'";
            if (string.IsNullOrWhiteSpace(slot))
                throw new ArgumentException($"{who} is a fit job and names no slot.", nameof(slot));
            bool slotSeen = false;
            FixtureDefinition piece = null;
            if (fixtures != null)
                foreach (var f in fixtures)
                {
                    if (f == null || f.Slot != slot) continue;
                    slotSeen = true;
                    if (f.Level == level) { piece = f; break; }
                }
            if (!slotSeen)
                throw new ArgumentException($"{who} asks for slot '{slot}', which no piece in fixtures.json stands in.", nameof(slot));
            if (piece == null)
                throw new ArgumentException($"{who} asks for rung {level} of '{slot}', and that ladder has no such rung.", nameof(level));
            if (rung < 0 || rung >= BarRank.Rungs.Count)
                throw new ArgumentOutOfRangeException(nameof(rung), $"{who} stands on rung {rung}.");
            if (piece.Stars > BarRank.Rungs[rung].Stars + BarRank.Epsilon)
                throw new ArgumentException(
                    $"{who} asks for '{piece.Id}' ({piece.Stars:0.0} stars) on rung {rung}; the shop sells it from {piece.Stars:0.0} stars.", nameof(rung));
            return piece;
        }

        /// <summary>
        /// DOES THIS SERVE COUNT (spec A.2)? Exact only, always — a job is never filled by a near miss — and then the
        /// kind's own question: the picked drink; a perfect make; a pint whose head landed; an order that asked for one
        /// of her preparations and got every ask on the glass the way the book says. Pure, so the truth table is a test.
        /// </summary>
        public static bool ServeCounts(QuestDefinition def, string questRecipeId, OrderMatch match,
            string wantedId, bool perfectMake, bool pint, bool craftLanded,
            IReadOnlyList<PreparationDefinition> asked)
        {
            if (def == null || match != OrderMatch.Exact) return false;
            switch (def.Kind)
            {
                case QuestKind.Serve:
                    return !string.IsNullOrEmpty(questRecipeId) && wantedId == questRecipeId;
                case QuestKind.Perfect:
                    return perfectMake;
                case QuestKind.Pints:
                    return pint && craftLanded;
                case QuestKind.Garnish:
                    if (pint || !craftLanded || asked == null) return false;
                    foreach (var a in asked)
                        foreach (var p in def.Preps)
                            if (a != null && a.Id == p.Id) return true;
                    return false;
                default:
                    return false;
            }
        }

        /// <summary>
        /// THE DRINKS A SERVE JOB MAY NAME (spec A.3): the menu, in menu order, authored pages the shelf can pour
        /// tonight (the question the night's plan asks), and for <see cref="QuestPick.Stirred"/> only the stirred ones.
        /// </summary>
        public static List<RecipeDefinition> Pool(QuestPick pick, IReadOnlyList<RecipeDefinition> menu,
            Func<RecipeDefinition, bool> canServe)
        {
            var pool = new List<RecipeDefinition>();
            if (menu == null) return pool;
            foreach (var r in menu)
            {
                if (r == null || !r.HasAuthoredRatios) continue;
                if (canServe != null && !canServe(r)) continue;
                if (pick == QuestPick.Stirred && r.Prep != PrepMethod.Stirred) continue;
                pool.Add(r);
            }
            return pool;
        }

        /// <summary>The page of the highest rank in <paramref name="pool"/>, ties to the earliest; null for an empty pool.</summary>
        public static RecipeDefinition Top(IReadOnlyList<RecipeDefinition> pool)
        {
            RecipeDefinition best = null;
            if (pool == null) return null;
            foreach (var r in pool)
                if (best == null || r.Rank > best.Rank) best = r;
            return best;
        }

        /// <summary>The same rule with no draw: <see cref="Top"/> for a top job, the first page for any other.</summary>
        public static RecipeDefinition FirstOf(QuestPick pick, IReadOnlyList<RecipeDefinition> pool) =>
            pool == null || pool.Count == 0 ? null : pick == QuestPick.Top ? Top(pool) : pool[0];
    }
}
