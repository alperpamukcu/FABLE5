using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    // TycoonRun, part Quests: the hostess and her book (2026-09-27, spec §B).
    //
    // The author: the host is replaced by the game's lead, a hostess who comes only for the jobs and the teaching; the
    // jobs come in a WRITTEN ORDER, the same every run, up the ladder; when one is done she comes at the close of the
    // next night, between the door shutting and the last drinker leaving, says her piece and leaves the next. Core owns
    // all of it — when she comes, what she brings, what counts, what it pays — and the screen only draws it. A run
    // nobody watches (the sim, the tests) is handed the same jobs: the hand-over is a verb, HearHostess, and Core calls
    // it itself once she has stood unheard for QuestRules.HostessGraceSeconds of floor time.
    //
    // It replaced the weekly job (2026-09-04 to 2026-09-28), which is deleted: the book is the only job in town, and a
    // run built without one has no job at all.
    public sealed partial class TycoonRun
    {
        /// <summary>The hostess's book, or null for a run built without one — opt-in like the story and the regulars.</summary>
        public QuestBook Quests { get; }

        /// <summary>The job on the bar: handed over and not yet replaced. Done between its completion and the next
        /// hand-over; null before her first visit.</summary>
        public ActiveQuest Quest { get; private set; }

        /// <summary>She is in the room, and nobody has heard her out yet. Non-null only on an open night at or after
        /// closing time; the night cannot close under her.</summary>
        public HostessVisit HostessVisit { get; private set; }

        /// <summary>Jobs passed over at an arrival: a state goal already met, or a serve job with nothing to pour.</summary>
        public int QuestsSkipped { get; private set; }

        /// <summary>The part of tonight's <see cref="DayBonus"/> that was hers, so the bill can say whose money it is.</summary>
        public int DayQuestPaid { get; private set; }

        /// <summary>She still owes the finished job's thanks — said on her next visit.</summary>
        public bool QuestDoneUnsaid => _doneUnsaid;

        /// <summary>The earliest night at whose close she comes; 0 when no visit is scheduled.</summary>
        public int HostessComesOn => _visitFrom;

        /// <summary>The next row of the book to be handed over, or null when the book is spent.</summary>
        public QuestDefinition QuestNextUp => Quests != null && _questNext < Quests.Count ? Quests[_questNext] : null;

        /// <summary>The book is spent and she has said everything she owes: she stops coming, and the bubble goes.</summary>
        public bool QuestChainOver =>
            Quests != null && _questNext >= Quests.Count && !_doneUnsaid && HostessVisit == null;

        /// <summary>The job just handed over, for the room to say so — read once and cleared.</summary>
        public ActiveQuest QuestJustGiven { get; private set; }

        /// <summary>The job just finished and paid, for the room to say so — read once and cleared.</summary>
        public ActiveQuest QuestJustDone { get; private set; }

        public ActiveQuest TakeQuestJustGiven()
        {
            var got = QuestJustGiven;
            QuestJustGiven = null;
            return got;
        }

        public ActiveQuest TakeQuestJustDone()
        {
            var got = QuestJustDone;
            QuestJustDone = null;
            return got;
        }

        private int _questNext;        // the row of the book she hands over next
        private int _visitFrom;        // HostessComesOn
        private bool _doneUnsaid;      // QuestDoneUnsaid
        private bool _visitedTonight;  // one arrival a night
        private double _visitWaited;   // the backstop's clock: unheld floor-seconds since she walked in

        /// <summary>The chain plays: a book was handed in and the config has not switched it off.</summary>
        private bool QuestsLive => Quests != null && _config.Quests;

        /// <summary>
        /// THE HAND-OVER (spec B.5). She has been heard: the job she brought is on the bar and counts from this instant,
        /// the finished one's thanks are said, and the next visit is scheduled. Called by the screen on the key under her
        /// last line, and by Core itself when nobody listens. Harmless with nobody in the room.
        /// </summary>
        public void HearHostess()
        {
            var v = HostessVisit;
            if (v == null) return;
            HostessVisit = null;
            _visitWaited = 0;
            _doneUnsaid = false;
            QuestJustDone = null;          // anything unread belongs to the job being replaced
            if (v.Offered != null)
            {
                Quest = v.Offered;
                _questNext = v.Offered.Definition.Index + 1;
                QuestJustGiven = Quest;
                _visitFrom = 0;
            }
            else if (v.Finale) _visitFrom = 0;   // the book is spent; the done job stays on the bar
            else _visitFrom = Day + 1;             // "not yet": asked again at every close until the rung opens
        }

        /// <summary>
        /// WHEN SHE COMES (spec B.3), read once a tick between the floor and the last call. On a night she is due she walks
        /// in on the first tick at or after closing time — whoever is still drinking, and even into an empty room. Once
        /// she is in, only the backstop runs here: Tick never runs while a screen holds the night for her, so a listening
        /// player freezes this clock and a headless run hands the job over on its own.
        /// </summary>
        private void WatchForHostess(double seconds)
        {
            if (!QuestsLive) return;
            if (HostessVisit != null)
            {
                _visitWaited += seconds;
                if (_visitWaited >= QuestRules.HostessGraceSeconds) HearHostess();
                return;
            }
            if (_visitedTonight || _visitFrom <= 0 || Day < _visitFrom || !Floor.IsClosingTime) return;
            _visitedTonight = true;
            HostessVisit = PlanVisit();    // null: nothing to say tonight, and she stays home
            _visitWaited = 0;              // the arrival tick's own seconds never count
        }

        /// <summary>
        /// WHAT SHE BRINGS (spec B.4), decided at her arrival and never again. A rung gate makes her WAIT, never skip; a
        /// state goal the bar already meets, or a serve job with nothing on the shelf to pour, is passed over — she never
        /// hands over a done deal or an impossible one.
        /// </summary>
        private HostessVisit PlanVisit()
        {
            ActiveQuest finished = _doneUnsaid ? Quest : null;
            ActiveQuest offered = null;
            QuestDefinition gated = null;
            while (_questNext < Quests.Count)
            {
                var def = Quests[_questNext];
                if (Rank.Index < def.Rung) { gated = def; break; }
                if (def.IsStateGoal && StateGoalMet(def)) { _questNext++; QuestsSkipped++; continue; }
                offered = Offer(def);
                if (offered == null) { _questNext++; QuestsSkipped++; continue; }
                break;
            }
            bool finale = offered == null && gated == null && finished != null;
            if (finished == null && offered == null) return null;
            return new HostessVisit(Day, finished, offered, finale,
                gated != null && offered == null ? BarRank.Rungs[gated.Rung] : null);
        }

        /// <summary>A row made into the job on the bar. A serve job picks its drink now (spec A.3): the top page with no
        /// draw, or one draw on the "quest" stream, which nothing else touches. Null when there is nothing to pour.</summary>
        private ActiveQuest Offer(QuestDefinition def)
        {
            string recipeId = string.Empty, recipeName = string.Empty;
            if (def.Kind == QuestKind.Serve)
            {
                var pool = QuestRules.Pool(def.Pick, MenuRecipes, CanServe);
                if (pool.Count == 0) return null;
                var pick = def.Pick == QuestPick.Top
                    ? QuestRules.Top(pool)
                    : pool[_rng.GetStream("quest").NextInt(0, pool.Count)];
                recipeId = pick.Id;
                recipeName = pick.Name;
            }
            return new ActiveQuest(def, recipeId, recipeName, def.Target, 0, def.Reward, Day);
        }

        /// <summary>Whether a state goal holds as the bar stands: the rung (high-water), the room, the ladder.</summary>
        private bool StateGoalMet(QuestDefinition def)
        {
            switch (def.Kind)
            {
                case QuestKind.Rank: return Rank.Index >= def.GoalRung;
                case QuestKind.Comfort: return ComfortBase >= def.GoalComfort - 1e-9;
                case QuestKind.Fit: return LadderLevel(def.Slot) >= def.Level;
                default: return false;
            }
        }

        /// <summary>A live job that is not done yet — the guard every counting hook asks first.</summary>
        private bool QuestOpen => QuestsLive && Quest != null && !Quest.IsDone;

        /// <summary>
        /// WILL THE DAWN PAY HER JOB (2026-09-28, the night's TOMORROW board)? A preview asked of the rule, the way
        /// <see cref="StandingAfterTonight"/> is: a state goal still open that <see cref="CountQuestStateGoal"/> would
        /// find met if the books closed now. The rung is read where the dawn reads it - after tonight is filed, so
        /// <see cref="RankAfterTonight"/> - and the room and the ladder as they stand. False for every other job: a
        /// count pays on the spot, and a job already done has been paid. The market comes between the two: a fitting
        /// bought there can still meet a goal this read as open, which is why the board only ever says "at dawn" for a
        /// goal already met.
        /// </summary>
        public bool QuestPaysAtDawn
        {
            get
            {
                if (!QuestOpen || !Quest.Definition.IsStateGoal) return false;
                var def = Quest.Definition;
                return def.Kind == QuestKind.Rank ? RankAfterTonight.Index >= def.GoalRung : StateGoalMet(def);
            }
        }

        /// <summary>
        /// SHE SETTLES UP ON THE SPOT, out of her own pocket: income on the night it lands, in the night's bonus line
        /// beside the state's thanks — money that came from doing something rather than selling something. And she will
        /// be in at the close of the next night to say so.
        /// </summary>
        private void PayForTheQuest()
        {
            Money += Quest.Reward;
            DayBonus += Quest.Reward;
            DayQuestPaid += Quest.Reward;
            Quest.DoneDay = Day;
            QuestJustDone = Quest;
            _doneUnsaid = true;
            _visitFrom = Day + 1;
            Feat(Stats.JobsDone, 1);   // the achievements' count (TycoonRun.Feats), moved here from the weekly job
        }

        /// <summary>The serve kinds (spec B.6), asked in <see cref="ServeTo"/> once the papers are known: nothing served to
        /// somebody who should have been shown the door counts, however well it was made.</summary>
        private void CountQuestServe(CustomerVisit visit, OrderMatch matchKind, ServiceVerdict verdict,
            GlassContents delivered, IdPapers papers)
        {
            if (!QuestOpen || !Quest.Definition.CountsServes) return;
            // The guest of the house is outside the books, and so outside her book (GDD 26 §3).
            if (ReferenceEquals(visit, LastCustomer) || visit.OnTheHouse) return;
            if (papers != null && papers.ShouldBeKicked) return;
            if (!QuestRules.ServeCounts(Quest.Definition, Quest.RecipeId, matchKind, visit.OrderTruth.Wanted.Id,
                    verdict.PerfectMake, delivered != null && delivered.HasPreparation(Preparations.Draught.Id),
                    verdict.CraftLanded, visit.OrderTruth.Garnishes))
                return;
            if (Quest.Count()) PayForTheQuest();
        }

        /// <summary>The door kind: a RIGHT kick, counted in <see cref="Kick"/>.</summary>
        private void CountQuestKick()
        {
            if (!QuestOpen || Quest.Kind != QuestKind.Door) return;
            if (Quest.Count()) PayForTheQuest();
        }

        /// <summary>The clean kind, at the close: a night opened after the hand-over, nobody walked out, nothing wrong went
        /// over the bar. Paid before the slip is drawn and before the red-night lesson reads the takings.</summary>
        private void CountQuestCleanNight()
        {
            if (!QuestOpen || Quest.Kind != QuestKind.Clean || Day <= Quest.GivenDay) return;
            if (NightTally().walked != 0 || NightHadAMistake) return;
            if (Quest.Count()) PayForTheQuest();
        }

        /// <summary>The state kinds, at dawn, once the night has filed its stars (the rung is the filed high-water
        /// mark) and before the ledger closes: the pay lands in the closed night's row.</summary>
        private void CountQuestStateGoal()
        {
            if (!QuestOpen || !Quest.Definition.IsStateGoal) return;
            if (!StateGoalMet(Quest.Definition)) return;
            if (Quest.Count()) PayForTheQuest();
        }

        /// <summary>
        /// A JOB THAT COUNTS ONE DRINK IS NEVER STARVED OF IT (2026-09-28, the review). Only an order moves a serve
        /// job or a pint job, and every order comes off the night's plan, which is cut at the bar's best rung: a
        /// four-star plan gives the ground floor two covers in a hundred, so a pint asked of a fast climber — or a
        /// first page asked of an old save restored high up the ladder — was never ordered again, and the book stood
        /// behind it for ever. While such a job is open and the shelf can pour its drink tonight, the plan carries at
        /// least one cover of it (<see cref="DayPlan.Guarantee"/>). A garnish job needs no such cover: the extras are
        /// rolled for every order that is not a pint, and the rail only grows up the ladder.
        /// </summary>
        private void KeepHerDrinkOnThePlan(DayPlan plan, IReadOnlyList<RecipeDefinition> pourable)
        {
            if (!QuestOpen) return;
            Func<RecipeDefinition, bool> answers;
            if (Quest.Kind == QuestKind.Serve)
            {
                string wanted = Quest.RecipeId;
                answers = r => r.Id == wanted;
            }
            else if (Quest.Kind == QuestKind.Pints) answers = ServingSpec.IsDraught;
            else return;
            foreach (var page in pourable)
                if (answers(page)) { plan.Guarantee(page, answers); return; }
        }

        /// <summary>
        /// THE DAWN'S BOOKKEEPING (spec B.6), after the day has turned: tonight is a new night for her, and every one-shot
        /// the screen had its frame to read is cleared so the save can represent what is left. And one self-healing rule:
        /// a run with nothing on the bar and nothing owed, whose book has rows left (rows appended after a finale), is
        /// visited again from tonight.
        /// </summary>
        private void SettleQuestAtDawn()
        {
            _visitedTonight = false;
            HostessVisit = null;
            _visitWaited = 0;
            QuestJustGiven = null;
            if (QuestsLive && _visitFrom == 0 && (Quest == null || Quest.IsDone) && !_doneUnsaid
                && _questNext < Quests.Count)
                _visitFrom = Day;
        }

        /// <summary>The dev verbs move the calendar, sometimes backwards: she forgets tonight, a visit scheduled for a
        /// night the calendar has been wound back past is brought forward to tonight, and a job still open is dated no
        /// later than last night — a clean job handed over on night twenty of a bar wound back to night three would
        /// otherwise refuse to count for seventeen nights.</summary>
        private void HostessFollowsTheCalendar()
        {
            HostessVisit = null;
            _visitedTonight = false;
            _visitWaited = 0;
            if (_visitFrom > Day) _visitFrom = Day;
            if (Quest != null && !Quest.IsDone && Quest.GivenDay > Day - 1) Quest.GivenDay = Day - 1;
        }

        /// <summary>How tonight's counted leavers split: those who drank, and those who walked (a wrong kick is a walk).
        /// The faces rightly shown the door are neither. One count for the close's clean night, the books - and the
        /// night's tape (2026-09-28), which prints these two numbers rather than counting the floor itself.</summary>
        public (int served, int walked) NightTally()
        {
            int served = 0, walked = 0;
            foreach (var visit in Floor.FinishedCounted())
            {
                if (visit.OffTheBooks) continue;
                if (visit.State == VisitState.StormedOff || visit.State == VisitState.Kicked) walked++;
                else served++;
            }
            return (served, walked);
        }

        /// <summary>
        /// THE ONE UNPOURABLE CASE (spec A.3): a drink can stop being pourable inside a run only across a data update,
        /// i.e. through a restore. A serve job still owed whose drink the bar cannot pour tonight is re-pointed at the
        /// first page of the same rule — no draw, the count back to nothing, the target and the pay kept. With no page to
        /// point at, it stays as it was.
        /// </summary>
        private void RepairTheQuestAfterRestore()
        {
            if (Quest == null || Quest.IsDone || Quest.Kind != QuestKind.Serve) return;
            RecipeDefinition recipe = null;
            foreach (var r in _recipes)
                if (r.Id == Quest.RecipeId) { recipe = r; break; }
            if (recipe != null && CanServe(recipe)) return;
            var pick = QuestRules.FirstOf(Quest.Definition.Pick,
                QuestRules.Pool(Quest.Definition.Pick, MenuRecipes, CanServe));
            if (pick != null) Quest.Repick(pick.Id, pick.Name);
        }
    }
}
