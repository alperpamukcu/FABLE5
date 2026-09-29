using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// What a step of the house tour waits for before the next one (2026-09-28, TycoonRun.Tour). A fixed vocabulary,
    /// like <see cref="StoryCue"/>: the data picks one BY NAME and supplies the words, and a name nobody implements is
    /// refused when the file loads. Core reads the ones the run can see for itself; the ones marked SCREEN are things
    /// only the screen knows (a card put away, a lid on, a book open) and are reported through
    /// <see cref="TycoonRun.TourSaw"/>.
    /// </summary>
    public enum TourWait
    {
        /// <summary>A thing said: the plate's key moves it on (<see cref="TycoonRun.HearTour"/>).</summary>
        Heard,
        /// <summary>The tour's guest is seated (Core seats them when this step comes up) and has made up their mind.</summary>
        GuestReady,
        /// <summary>The guest's licence has been read.</summary>
        CardRead,
        /// <summary>SCREEN: the card is put away again.</summary>
        CardPutAway,
        /// <summary>SCREEN: the cellar under the counter is open.</summary>
        CellarOpen,
        /// <summary>SCREEN: a bottle from the cellar is in the hand, on a bench.</summary>
        BottleInHand,
        /// <summary>Something of the order is in the tin and nothing is pouring.</summary>
        FirstPour,
        /// <summary>Every bottle the order names is in the tin and nothing is pouring.</summary>
        TinBuilt,
        /// <summary>SCREEN: the lid is on the tin.</summary>
        Capped,
        /// <summary>The tin has been poured out into the serving glass.</summary>
        InTheGlass,
        /// <summary>SCREEN: the bench is shut and the glass stands on the counter.</summary>
        OnTheCounter,
        /// <summary>The guest has been handed a drink.</summary>
        Served,
        /// <summary>The guest has finished and gone.</summary>
        GuestGone,
        /// <summary>The glass they left is off the counter (or they left none).</summary>
        GlassCollected,
        /// <summary>No mark left on the wood (a step that is already true when it comes up is passed unsaid).</summary>
        CounterWiped,
        /// <summary>SCREEN: the recipe book is open.</summary>
        BookOpen,
        /// <summary>SCREEN: the recipe book is shut again.</summary>
        BookShut,
        /// <summary>SCREEN: the recipe card is up over the licence's order.</summary>
        RecipeShown,
        /// <summary>SCREEN: a recipe is pinned to the screen (the post-it).</summary>
        RecipePinned,
        /// <summary>The tin has been shaken.</summary>
        Shaken,
        /// <summary>The open tin has been stirred.</summary>
        Stirred,
        /// <summary>The glass carries the garnish the lesson is about.</summary>
        Garnished,
    }

    public static class TourWaits
    {
        private static readonly Dictionary<string, TourWait> ByName = new Dictionary<string, TourWait>
        {
            { "heard", TourWait.Heard },
            { "guest_ready", TourWait.GuestReady },
            { "card_read", TourWait.CardRead },
            { "card_put_away", TourWait.CardPutAway },
            { "cellar_open", TourWait.CellarOpen },
            { "bottle_in_hand", TourWait.BottleInHand },
            { "first_pour", TourWait.FirstPour },
            { "tin_built", TourWait.TinBuilt },
            { "capped", TourWait.Capped },
            { "in_the_glass", TourWait.InTheGlass },
            { "on_the_counter", TourWait.OnTheCounter },
            { "served", TourWait.Served },
            { "guest_gone", TourWait.GuestGone },
            { "glass_collected", TourWait.GlassCollected },
            { "counter_wiped", TourWait.CounterWiped },
            { "book_open", TourWait.BookOpen },
            { "book_shut", TourWait.BookShut },
            { "recipe_shown", TourWait.RecipeShown },
            { "recipe_pinned", TourWait.RecipePinned },
            { "shaken", TourWait.Shaken },
            { "stirred", TourWait.Stirred },
            { "garnished", TourWait.Garnished },
        };

        /// <summary>Reads a wait by the name the data uses; null for anything else.</summary>
        public static TourWait? Parse(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return ByName.TryGetValue(name.Trim().ToLowerInvariant(), out var w) ? w : (TourWait?)null;
        }

        /// <summary>Every name the data may use, for a failure message worth reading.</summary>
        public static IEnumerable<string> Names => ByName.Keys;

        /// <summary>
        /// The waits that are PROGRESS through the one drink the tour makes: once true they stay true for the rest of
        /// the tour, so a player who has run ahead of her can be caught up with (<see cref="TycoonRun.TourSaw"/>'s
        /// look-ahead) instead of being told to do what is already done.
        /// </summary>
        public static bool IsProgress(TourWait w) =>
            w == TourWait.CardRead || w == TourWait.InTheGlass || w == TourWait.Served ||
            w == TourWait.GuestGone || w == TourWait.GlassCollected;

        /// <summary>The waits only the screen can see.</summary>
        public static bool IsScreen(TourWait w) =>
            w == TourWait.CardPutAway || w == TourWait.CellarOpen || w == TourWait.BottleInHand || w == TourWait.Capped ||
            w == TourWait.OnTheCounter || w == TourWait.BookOpen || w == TourWait.BookShut ||
            w == TourWait.RecipeShown || w == TourWait.RecipePinned;
    }

    /// <summary>
    /// WHEN A TOUR PLAYS (2026-09-29, the author: "oyuna yeni oynanış mekaniği geldiğinde örneğin kimlik kontrol etme yaş
    /// kontrol etme, alkolleri karıştırma çalkalama. İçerisine garnish koyma. Bunların da öğreticileri oyuncu ilk defa
    /// açtığında ve kullanmak zorunda olduğunda otomatik başlamalı"). The house tour opens the first night; every other
    /// is a short lesson that starts by itself the first time its mechanic is both open and needed. Each plays once a run.
    /// </summary>
    public enum TourCue
    {
        /// <summary>The run's first night: the house tour.</summary>
        FirstNight,
        /// <summary>The door is the bar's (1 star) and a licence has just been read: ages, fakes and the KICK key.</summary>
        FirstDoor,
        /// <summary>A shaken drink stands in the tin, unmixed.</summary>
        FirstShake,
        /// <summary>A stirred drink stands in the open tin, unmixed, and the spoon is the bar's.</summary>
        FirstStir,
        /// <summary>A drink is in the glass and its guest asked for a garnish that goes IN it (ice, twist, olive, mint).</summary>
        FirstGarnish,
        /// <summary>...or for a RIM (salt, sugar), which is run round the glass rather than dropped.</summary>
        FirstRim,
    }

    public static class TourCues
    {
        private static readonly Dictionary<string, TourCue> ByName = new Dictionary<string, TourCue>
        {
            { "first_night", TourCue.FirstNight },
            { "first_door", TourCue.FirstDoor },
            { "first_shake", TourCue.FirstShake },
            { "first_stir", TourCue.FirstStir },
            { "first_garnish", TourCue.FirstGarnish },
            { "first_rim", TourCue.FirstRim },
        };

        public static TourCue? Parse(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return ByName.TryGetValue(name.Trim().ToLowerInvariant(), out var c) ? c : (TourCue?)null;
        }

        public static IEnumerable<string> Names => ByName.Keys;
    }

    /// <summary>
    /// The names a step may point at: the things on the screen she shows the player. Core never reads them - the screen
    /// resolves each to a place (TycoonHud.Tour) - but the vocabulary lives here, next to the waits, so the file's loader
    /// can refuse a name the screen would not find. Empty points at nothing.
    /// </summary>
    public static class TourPoints
    {
        public static readonly IReadOnlyList<string> Names = new[]
        {
            "clock", "till", "bill", "stars", "house", "stool", "patience", "card", "cellar", "bottle", "bench_bottle",
            "tin_gauge", "lid", "shaker", "serve_key", "counter_glass", "guest", "dirty_glass", "cloth", "book", "tap",
            "corner",
            // 2026-09-29: the licence's order row, its recipe card and pin, its age, its flag and its KICK key; the tin,
            // the spoon and the MIX column; the serve bench's glass; the sink and a mark on the wood; the garnish the
            // lesson is about; the bench's list of steps and its bin.
            "card_order", "recipe_card", "pin_key", "card_age", "card_flag", "card_kick", "tin", "spoon", "mix_column",
            "serve_glass", "sink", "mark", "garnish", "bench_steps", "bin_key",
        };

        public static bool Known(string point)
        {
            if (string.IsNullOrEmpty(point)) return true;
            foreach (var name in Names) if (name == point) return true;
            return false;
        }
    }

    /// <summary>One stop on the tour: what she says, what the screen points at while she says it, and what moves it
    /// on. <see cref="Point"/> is a name the screen resolves (the clock, the till, the stool, the cellar...); Core never
    /// reads it.</summary>
    public sealed class TourStep
    {
        public string Id { get; }
        public IReadOnlyList<string> Say { get; }
        public TourWait Wait { get; }
        public string Point { get; }

        /// <summary>Where a DRAG step's thing goes (the glass to the guest, the lid onto the tin): the screen shows a hand
        /// carrying it there, and lights both ends. Empty for everything else.</summary>
        public string To { get; }

        public TourStep(string id, IReadOnlyList<string> say, TourWait wait, string point, string to = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A tour step needs an id.", nameof(id));
            if (say == null || say.Count == 0) throw new ArgumentException($"tour step '{id}' has nothing to say", nameof(say));
            foreach (var line in say)
                if (string.IsNullOrWhiteSpace(line)) throw new ArgumentException($"tour step '{id}' has an empty line", nameof(say));
            Id = id;
            Say = new List<string>(say);
            Wait = wait;
            Point = point ?? "";
            To = to ?? "";
        }

        public override string ToString() => $"{Id} ({Wait})";
    }

    /// <summary>
    /// THE HOUSE TOUR (2026-09-28): the hostess's walk round the bar on its first night, in order. Data
    /// (<c>Resources/Data/tour.json</c>), the same every run. It needs a step that seats the guest before any step
    /// that works on the guest's drink, and it must end on a thing said - the tour hands the night over in words.
    /// </summary>
    public sealed class TourScript
    {
        public IReadOnlyList<TourStep> Steps { get; }

        /// <summary>The tour's name ("house", "door"...) - what the run remembers it has played.</summary>
        public string Id { get; }

        /// <summary>When it plays.</summary>
        public TourCue When { get; }

        /// <summary>The house tour: the one that seats a guest of its own and walks the whole first night.</summary>
        public bool IsHouse => When == TourCue.FirstNight;

        public TourScript(IReadOnlyList<TourStep> steps) : this("house", TourCue.FirstNight, steps) { }

        public TourScript(string id, TourCue when, IReadOnlyList<TourStep> steps)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A tour needs an id.", nameof(id));
            Id = id;
            When = when;
            if (steps == null || steps.Count == 0) throw new ArgumentException($"tour '{id}' has no steps.", nameof(steps));
            var ids = new HashSet<string>();
            bool seated = false;
            foreach (var step in steps)
            {
                if (step == null) throw new ArgumentException("The tour has an empty step.", nameof(steps));
                if (!ids.Add(step.Id)) throw new ArgumentException($"tour step '{step.Id}' is written twice", nameof(steps));
                if (step.Wait == TourWait.GuestReady)
                {
                    if (!IsHouse) throw new ArgumentException($"tour '{id}' seats a guest; only the house tour does.", nameof(steps));
                    if (seated) throw new ArgumentException("The tour seats its guest twice.", nameof(steps));
                    seated = true;
                }
                else if (IsHouse && !seated && step.Wait != TourWait.Heard && NeedsTheGuest(step.Wait))
                    throw new ArgumentException($"tour step '{step.Id}' works on the guest before one is seated", nameof(steps));
            }
            if (steps[steps.Count - 1].Wait != TourWait.Heard)
                throw new ArgumentException("The tour must end on a thing said.", nameof(steps));
            Steps = new List<TourStep>(steps);
        }

        private static bool NeedsTheGuest(TourWait w) =>
            w != TourWait.CellarOpen && w != TourWait.BookOpen && w != TourWait.BookShut;
    }

    /// <summary>
    /// Every tour the game has, the house tour and the first-use lessons (<c>Resources/Data/tour.json</c>). At most one
    /// per cue, and the step ids are unique across the whole book - they are the string table's keys.
    /// </summary>
    public sealed class TourBook
    {
        public IReadOnlyList<TourScript> Tours { get; }

        public TourBook(IReadOnlyList<TourScript> tours)
        {
            if (tours == null || tours.Count == 0) throw new ArgumentException("The tour book is empty.", nameof(tours));
            var cues = new HashSet<TourCue>();
            var ids = new HashSet<string>();
            var steps = new HashSet<string>();
            foreach (var t in tours)
            {
                if (t == null) throw new ArgumentException("The tour book has an empty tour.", nameof(tours));
                if (!ids.Add(t.Id)) throw new ArgumentException($"tour '{t.Id}' is written twice", nameof(tours));
                if (!cues.Add(t.When)) throw new ArgumentException($"two tours play on {t.When}", nameof(tours));
                foreach (var s in t.Steps)
                    if (!steps.Add(s.Id)) throw new ArgumentException($"tour step '{s.Id}' is written twice in the book", nameof(tours));
            }
            Tours = new List<TourScript>(tours);
        }

        /// <summary>The book of one tour (the tests' and a bench's).</summary>
        public TourBook(TourScript tour) : this(new[] { tour }) { }

        public TourScript For(TourCue cue)
        {
            foreach (var t in Tours) if (t.When == cue) return t;
            return null;
        }
    }
}
