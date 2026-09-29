using System.Collections.Generic;

namespace LastCall.Core
{
    // TycoonRun, part Tour: the first night opens with the hostess showing the new owner round the bar - and every new
    // thing to do after it is shown the first time it is needed.
    //
    // The author, 2026-09-28: "Oyunda bir öğreticimiz olmalı. Oyundaki ilk gün aslında scripted tüm ekranı neleri nasıl
    // yapabileceğini anlatan ve tarif eden bir öğretici olmalı. Bu öğreticide sahneye ana karakterimiz gelir konuşarak
    // sırayla temelden detaya doğru öğretir ekranı ve neler yapıldığını."
    // And 2026-09-29: "oyuna yeni oynanış mekaniği geldiğinde örneğin kimlik kontrol etme yaş kontrol etme, alkolleri
    // karıştırma çalkalama. İçerisine garnish koyma. Bunların da öğreticileri oyuncu ilk defa açtığında ve kullanmak
    // zorunda olduğunda otomatik başlamalı."
    //
    // The tutorial the 2026-08-07 sweep deleted comes back as her walk round the room on the night it opens - not as a
    // night of its own: the first night IS the tour, and it is the first night the whole way through (its money, its
    // stars, its job at the close). Core owns the tour's clock and its one guest: while a step is up the DOOR is held -
    // the night's clock stands, nobody walks in, nobody's patience runs and the counter's marks do not age - but the room
    // is not frozen, so the guest can walk in, make up their mind, drink and leave while she talks the player through each
    // of those. The guest is the night's first planned cover, seated by the step that asks for them; the verbs are the
    // player's own, and what each step waits for is read off the run (or, for the few things only the screen can see,
    // reported by it through TourSaw). The words are data (Resources/Data/tour.json).
    //
    // THE FIRST-USE LESSONS are tours too, from the same book: the door the first time a card is read after the door is
    // the bar's, the shake and the stir the first time a drink that needs one stands unmixed in the tin, the garnish and
    // the rim the first time a drink in the glass is waiting on one. Each starts by itself, holds the door the same way,
    // is taught on the guest it was about (TourSubject) and plays once a run - the set is saved with the run.
    //
    // Opt-in exactly like the story and the book: a run built without a tour book plays every tick as it did before there
    // was one - the sim, the bench tests, a bar resumed from a save written before the book.
    public sealed partial class TycoonRun
    {
        private TourBook _tours;
        private TourScript _tour;
        private int _tourAt;
        private readonly HashSet<string> _toursTaught = new HashSet<string>();
        private readonly HashSet<CustomerVisit> _cardsSeenRead = new HashSet<CustomerVisit>();

        /// <summary>A tour is on: a step is up and the door is held. The house tour only ever on the first night - a dev
        /// verb that moves the calendar takes it with it.</summary>
        public bool TourRunning => _tour != null && _tourAt < _tour.Steps.Count && (!_tour.IsHouse || Day == 1);

        /// <summary>The house tour is the one on (her walk round the room), not a first-use lesson.</summary>
        public bool TourIsHouse => TourRunning && _tour.IsHouse;

        /// <summary>The tour on, by its id ("house", "door"...), or null.</summary>
        public string TourId => TourRunning ? _tour.Id : null;

        /// <summary>The step she is on, or null once the tour is over (or there never was one).</summary>
        public TourStep TourStep => TourRunning ? _tour.Steps[_tourAt] : null;

        /// <summary>Where the tour has got to, 0-based, and how long it is - for the screen's "3 / 20".</summary>
        public int TourStepIndex => _tourAt;
        public int TourStepCount => _tour?.Steps.Count ?? 0;

        /// <summary>The one guest the house tour seats and teaches on. An ordinary visit in every way the books can see.</summary>
        public CustomerVisit TourGuest { get; private set; }

        /// <summary>The guest a first-use lesson is about (the card just read, the drink waiting on its garnish).</summary>
        public CustomerVisit TourSubject { get; private set; }

        /// <summary>The guest the tour on is working on: the house tour's own, or the lesson's.</summary>
        public CustomerVisit TourFocus => TourIsHouse ? TourGuest : TourSubject;

        /// <summary>The garnish a garnish or rim lesson is about, or null.</summary>
        public PreparationDefinition TourGarnish { get; private set; }

        /// <summary>The player said they know the ropes.</summary>
        public bool TourSkipped { get; private set; }

        /// <summary>The tours this run has played (saved with it), so no lesson says itself twice.</summary>
        public IReadOnlyCollection<string> ToursTaught => _toursTaught;

        /// <summary>The door waits on the tour: the floor ticks with the door held (BarDay.Tick).</summary>
        private bool TourHoldsTheDoor => TourRunning;

        /// <summary>
        /// Takes the book, from the constructor, and starts the house tour on the run's first night. The two lessons it says
        /// itself - the first night's and the licence's - are spent silently, so the host does not say them twice.
        /// </summary>
        private void StartTour(TourBook tours)
        {
            _tours = tours;
            var house = tours?.For(TourCue.FirstNight);
            if (house == null || Day != 1 || Phase != TycoonPhase.DayOpen) return;
            Begin(house, null);
            Story?.Learn(StoryCue.FirstNight);
            Story?.Learn(StoryCue.FirstLicence);
        }

        private void Begin(TourScript tour, CustomerVisit subject)
        {
            _tour = tour;
            _tourAt = 0;
            TourSubject = subject;
            _toursTaught.Add(tour.Id);
        }

        /// <summary>The plate's key on a thing said: the next step. Harmless on any other step.</summary>
        public void HearTour()
        {
            var step = TourStep;
            if (step == null || step.Wait != TourWait.Heard) return;
            bool house = _tour.IsHouse;
            NextTourStep();
            if (!TourRunning) Over(house);
        }

        /// <summary>
        /// The screen saw something only it can see (a card put away, the cellar open, the lid on, the book open or
        /// shut, the recipe card up or pinned). It moves the tour on only when that is what the step is waiting for.
        /// </summary>
        public void TourSaw(TourWait what)
        {
            var step = TourStep;
            if (step == null || step.Wait != what || !TourWaits.IsScreen(what)) return;
            bool house = _tour.IsHouse;
            NextTourStep();
            if (!TourRunning) Over(house);
        }

        /// <summary>
        /// "I know the ropes." The tour ends where it stands and the night opens: a guest already seated becomes an
        /// ordinary drinker whose patience starts now, and anything half made stays in the tin. A lesson skipped is a
        /// lesson played - it does not come back.
        /// </summary>
        public void SkipTour()
        {
            if (!TourRunning) return;
            bool house = _tour.IsHouse;
            if (house) TourSkipped = true;
            _tourAt = _tour.Steps.Count;
            Over(house);
        }

        private void Over(bool house)
        {
            TourSubject = null;
            TourGarnish = null;
            if (house) StaysForTheFirstJob();
        }

        /// <summary>
        /// THE FIRST JOB AT THE END OF THE TOUR. She is in the room already, so the book's first hand-over is said
        /// here rather than at the close: the job is on the bar for the night she showed the player round, and she does
        /// not walk in again at closing time. Her visit is the one WatchForHostess would have made (PlanVisit, once a
        /// night), so everything after it - the plate, the key, Core's own backstop - is the visit as it always was.
        /// </summary>
        private void StaysForTheFirstJob()
        {
            if (!QuestsLive || HostessVisit != null || _visitedTonight || _visitFrom <= 0 || Day < _visitFrom) return;
            _visitedTonight = true;
            HostessVisit = PlanVisit();
            _visitWaited = 0;
        }

        private void NextTourStep()
        {
            _tourAt++;
            CatchUpTheTour();
        }

        /// <summary>
        /// Once a tick, straight after the floor: starts a first-use lesson whose moment has come, seats the house
        /// tour's guest when their step comes up, and moves past every step whose wait is already true. Returns who sat
        /// down, for the screen to walk in.
        /// </summary>
        private IReadOnlyList<CustomerVisit> TourTick(IReadOnlyList<CustomerVisit> seated)
        {
            if (!TourRunning) WatchForLessonTours();
            if (!TourRunning) return seated;
            if (TourStep.Wait == TourWait.GuestReady && TourGuest == null)
            {
                TourGuest = NextArrival();
                Floor.SeatGuest(TourGuest);
                var withGuest = new List<CustomerVisit>(seated) { TourGuest };
                seated = withGuest;
            }
            CatchUpTheTour();
            if (!TourRunning) Over(false);
            return seated;
        }

        // ── the first-use lessons ───────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The moments a lesson is for, in the order a night meets them. Only while no tour is on, never over the
        /// hostess, and each at most once a run. Nothing here draws a number: the conditions read the floor and the tin.
        /// </summary>
        private void WatchForLessonTours()
        {
            if (_tours == null || Phase != TycoonPhase.DayOpen || HostessVisit != null) return;

            // A card read THIS tick (the screen reads it the moment the licence opens), with the door the bar's.
            CustomerVisit justRead = null;
            foreach (var v in Floor.Seated)
                if (v.IdInspected && _cardsSeenRead.Add(v) && v.State == VisitState.Waiting && !v.OnTheHouse) justRead = v;
            if (justRead != null && Has(Feature.Door) && TryLesson(TourCue.FirstDoor, justRead)) return;

            // An unmixed drink in the tin that wants its method, with nothing pouring.
            if (!Glass.IsEmpty && !IsMixed && PouringId == null)
            {
                var method = TinMethod;
                if (method == PrepMethod.Shaken && TryLesson(TourCue.FirstShake, null))
                {
                    Story?.Learn(StoryCue.TwoSpiritsInTheTin);
                    return;
                }
                if (method == PrepMethod.Stirred && SpoonUnlocked && TryLesson(TourCue.FirstStir, null))
                {
                    Story?.Learn(StoryCue.TwoSpiritsInTheTin);
                    return;
                }
            }

            // A drink in the glass whose guest (card read) asked for a garnish it has not got yet.
            if (DrinkReady)
            {
                foreach (var v in Floor.Seated)
                {
                    if (v.State != VisitState.Waiting || !v.IdInspected || v.OnTheHouse) continue;
                    var asked = v.Order.Garnishes;
                    if (asked == null) continue;
                    foreach (var prep in asked)
                    {
                        if (ServingGlass.HasPreparation(prep.Id)) continue;
                        var cue = IsRim(prep) ? TourCue.FirstRim : TourCue.FirstGarnish;
                        TourGarnish = prep;   // before the lesson begins: its first catch-up reads it
                        if (TryLesson(cue, v)) return;
                        TourGarnish = null;
                    }
                }
            }
        }

        private bool TryLesson(TourCue cue, CustomerVisit subject)
        {
            var tour = _tours.For(cue);
            if (tour == null || _toursTaught.Contains(tour.Id)) return false;
            Begin(tour, subject);
            CatchUpTheTour();
            return true;
        }

        private static bool IsRim(PreparationDefinition prep) =>
            prep != null && (prep.Id == Preparations.SaltRim.Id || prep.Id == Preparations.SugarRim.Id);

        // ── the steps ───────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Moves past the step on screen while its wait is true - and, when the player has run ahead of her, straight
        /// to where they are: past the furthest PROGRESS wait already true (a drink served while she was still on the
        /// cellar is served, and what she had to say about making it is moot). Only from a step that waits for the
        /// player: a thing she is saying is always said. Steps that are true the moment they come up (no mark was left,
        /// so there is nothing to wipe) pass without a word. A lesson whose thing has gone (the drink binned, the guest
        /// served or gone) is over.
        /// </summary>
        private void CatchUpTheTour()
        {
            RewindABinnedDrink();
            if (LessonIsMoot()) { _tourAt = _tour.Steps.Count; return; }
            while (TourRunning)
            {
                var step = TourStep;
                if (step.Wait == TourWait.Heard) return;
                int ahead = -1;
                for (int i = _tourAt + 1; i < _tour.Steps.Count; i++)
                    if (TourWaits.IsProgress(_tour.Steps[i].Wait) && TourWaitMet(_tour.Steps[i].Wait)) ahead = i;
                if (ahead >= 0) { _tourAt = ahead + 1; continue; }
                if (TourWaits.IsScreen(step.Wait) || !TourWaitMet(step.Wait)) return;
                _tourAt++;
            }
        }

        /// <summary>A lesson with nothing left to teach on: the mix lessons without a drink in the tin to mix, the garnish
        /// lessons without their guest waiting on a drink in the glass. The door lesson is only words and never moot.</summary>
        private bool LessonIsMoot()
        {
            if (!TourRunning || _tour.IsHouse) return false;
            switch (_tour.When)
            {
                case TourCue.FirstShake:
                case TourCue.FirstStir:
                    return Glass.IsEmpty && !IsMixed && ServingGlass.IsEmpty;
                case TourCue.FirstGarnish:
                case TourCue.FirstRim:
                    var v = TourSubject;
                    return v == null || v.State != VisitState.Waiting || v.DrinkServed || !DrinkReady;
                default:
                    return false;
            }
        }

        /// <summary>
        /// A DRINK LOST ON THE WAY IS STARTED AGAIN: binned, or tipped away. Past the first pour and before the serve, a
        /// tin and a glass that are both empty - nothing pouring, the guest still waiting for it - mean there is no drink
        /// any more, and the steps ahead would wait for one forever. Back to the cellar (the screen passes it at once if
        /// the drawer is still out). The house tour's own drink only.
        /// </summary>
        private void RewindABinnedDrink()
        {
            if (!TourRunning || !_tour.IsHouse) return;
            var step = TourStep;
            var guest = TourGuest;
            if (step == null || guest == null || guest.DrinkServed || guest.State != VisitState.Waiting) return;
            if (step.Wait != TourWait.TinBuilt && step.Wait != TourWait.Capped && step.Wait != TourWait.InTheGlass
                && step.Wait != TourWait.OnTheCounter) return;
            if (!Glass.IsEmpty || !ServingGlass.IsEmpty || PouringId != null) return;
            for (int i = 0; i < _tourAt; i++)
                if (_tour.Steps[i].Wait == TourWait.CellarOpen) { _tourAt = i; return; }
        }

        /// <summary>The run's own reading of a wait. The screen's waits are never true here.</summary>
        private bool TourWaitMet(TourWait wait)
        {
            var guest = TourFocus;
            bool gone = guest != null && guest.State != VisitState.Waiting && guest.State != VisitState.Drinking;
            switch (wait)
            {
                case TourWait.GuestReady: return guest != null && guest.HasOrdered;
                case TourWait.CardRead: return guest != null && guest.IdInspected;
                case TourWait.FirstPour: return PouringId == null && !Glass.IsEmpty;
                case TourWait.TinBuilt: return PouringId == null && TourNextBottle == null && !Glass.IsEmpty;
                case TourWait.InTheGlass: return !ServingGlass.IsEmpty && Glass.IsEmpty;
                case TourWait.Served: return guest != null && guest.DrinkServed;
                case TourWait.GuestGone: return gone;
                case TourWait.GlassCollected: return gone && Floor.House.GlassesOnCounter == 0;
                case TourWait.CounterWiped:
                    if (!gone) return false;
                    foreach (var mess in Floor.House.Messes) if (mess.Smudged) return false;
                    return true;
                case TourWait.Shaken: return IsShaken;
                case TourWait.Stirred: return IsStirred;
                case TourWait.Garnished: return TourGarnish != null && ServingGlass.HasPreparation(TourGarnish.Id);
                default: return false;
            }
        }

        /// <summary>
        /// The next bottle the guest's drink wants that is not in the tin yet, by the page's own order - the one the
        /// cellar step points at. Null before the card is read (the order lives behind it), when the tin already has
        /// everything the page names, or when the page names a family rather than a bottle.
        /// </summary>
        public string TourNextBottle
        {
            get
            {
                var guest = TourFocus;
                if (guest == null || !guest.IdInspected || guest.State != VisitState.Waiting) return null;
                var recipe = guest.Order.Wanted;
                if (recipe?.RatioRequirements == null) return null;
                foreach (var band in recipe.RatioRequirements)
                {
                    if (!band.IsStyleBand) continue;
                    bool inTheTin = false;
                    foreach (var id in Glass.Ingredients)
                    {
                        var card = FindShelfCard(id);
                        if (card?.Info?.Style == band.Style) { inTheTin = true; break; }
                    }
                    if (inTheTin) continue;
                    var bottle = Market.FindByStyle(_shelf, band.Style);
                    if (bottle != null) return bottle.Ingredient.Id;
                }
                return null;
            }
        }

        private IngredientCard FindShelfCard(string id)
        {
            foreach (var bottle in _shelf.Bottles)
                if (bottle.Ingredient.Id == id) return bottle.Ingredient;
            return null;
        }

        // ── the save ────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The tours played, for the snapshot.</summary>
        internal List<string> ToursTaughtForSave() => new List<string>(_toursTaught);

        /// <summary>A restored bar remembers the lessons it has had (an older save has none, and hears them again).</summary>
        internal void RestoreToursTaught(IEnumerable<string> ids)
        {
            _toursTaught.Clear();
            if (ids == null) return;
            foreach (var id in ids) if (!string.IsNullOrEmpty(id)) _toursTaught.Add(id);
        }
    }
}
