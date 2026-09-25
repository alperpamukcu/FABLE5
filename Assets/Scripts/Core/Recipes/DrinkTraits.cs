using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>Whether a page's character helps the house, costs it, or does both.</summary>
    public enum TraitSign
    {
        None,
        Buff,
        Nerf,
    }

    /// <summary>Which lever a character pulls. The menu groups by this, and the balance pass
    /// reads it to check that no one lever carries the whole book.</summary>
    public enum TraitChannel
    {
        None,
        /// <summary>The clock over the head: how long they will sit for it.</summary>
        Clock,
        /// <summary>Whether they ask for another.</summary>
        Round,
        /// <summary>What the rest of the room feels when it goes past.</summary>
        Room,
        /// <summary>What it leaves on the counter.</summary>
        Counter,
        /// <summary>How forgiving the pour is.</summary>
        Craft,
        /// <summary>What lands in the till.</summary>
        Coin,
    }

    /// <summary>
    /// A PAGE'S CHARACTER (2026-09-22, the author: "Tariflere/kokteyllere buff/nerf özellikler
    /// eklenmeli ve bu dengeli olmalı, çeşitlilik çok ve etkisi az ile orta seviye arasında
    /// olmalı ... o kokteyller hiçbir özelliği olmayanlardan bir tık daha uygun fiyatlı olabilir").
    ///
    /// Every field here is a SCALE or a COUNT on a constant that already exists at the call site,
    /// and <see cref="DrinkTraits.None"/> is every identity — so a page with no character computes
    /// bit-for-bit the doubles it computed before this file existed. That is the whole safety
    /// argument: one file holds every figure, so re-tuning a house constant re-tunes the
    /// characters that scale it, and nothing can drift.
    ///
    /// The ASSIGNMENT — which page carries which id — is content and lives in
    /// <c>recipes.json</c> ↔ <see cref="RecipeCatalog"/> under the parity test. The CATALOGUE —
    /// what an id is worth — is a rule and lives here, the way <see cref="Preparations"/> and the
    /// <see cref="BarRank"/> rungs do. <see cref="RecipeDefinition.Trait"/> carries only the
    /// string; the loader is what refuses a name nobody wrote, loudly, on first parse.
    /// </summary>
    public sealed class DrinkTrait
    {
        /// <summary>No character: every identity. A page without a trait, a pour that matched
        /// nothing, and a null recipe all read as this.</summary>
        public static readonly DrinkTrait None = new DrinkTrait("", TraitSign.None, TraitChannel.None);

        public string Id { get; }
        public TraitSign Sign { get; }
        public TraitChannel Channel { get; }

        /// <summary>
        /// WHAT IT SAYS ON THE PAGE, AS A NUMBER (2026-09-23, the author: "bufflar statlarıyla
        /// beraber açıklanmalı. Örneğin (Sabır +%5) gibi").
        ///
        /// The first cut of this system printed no figures at all, on the theory that a bar's
        /// menu is not a stat sheet — the author's ruling reverses that, and is right to: a buff
        /// nobody can read is a buff nobody can plan around, and this whole set exists so that
        /// buying a page is a choice. <see cref="StatKey"/> names the thing that moves (a short
        /// word the string table owns) and <see cref="StatPercent"/> is how far, signed.
        ///
        /// The number is the SCALE's own, on the thing the label names — never a re-derived
        /// "felt" figure, which would be a second set of numbers to keep in step with these.
        /// </summary>
        public string StatKey { get; private set; } = "";

        /// <summary>Signed, in whole per cent of whatever <see cref="StatKey"/> names.</summary>
        public int StatPercent { get; private set; }

        /// <summary>−1 for a buff (a notch cheaper), +1 for a nerf (a notch dearer), 0 otherwise.
        /// A SIGN, never a dollar and never a share: <see cref="DrinkOrder.MenuPrice"/> owns the
        /// arithmetic, so there is exactly one place in the game where a character touches money.</summary>
        public int PriceSign => Sign == TraitSign.Buff ? -1 : Sign == TraitSign.Nerf ? 1 : 0;

        // ── the scales. Identity is 1.0, or the house's own figure. ──────────────────────────

        /// <summary>× <see cref="CustomerVisit.OrderTakenPatienceBonus"/> — the box a customer
        /// gets back for being ASKED. Post-card by construction, so the gauge over an unread head
        /// drains at the house rate for everyone and the character cannot be read off the clock.</summary>
        public double AskBoxScale { get; private set; } = 1.0;

        /// <summary>× <see cref="CustomerVisit.ExtraOrderPatienceRefill"/> — the clock a second
        /// round starts on.</summary>
        public double RefillScale { get; private set; } = 1.0;

        /// <summary>× the lateness penalty on satisfaction (ServiceJudge's WaitPenalty).</summary>
        public double WaitPenaltyScale { get; private set; } = 1.0;

        /// <summary>× <see cref="ServiceJudge.ClockFloor"/> — the share of the tip that is not on
        /// the clock at all. Pays only a serve that is already late, and nothing to one that is not.</summary>
        public double ClockFloorScale { get; private set; } = 1.0;

        /// <summary>× <see cref="ServiceJudge.TipCeiling"/>. Lands outside the weighted craft sum,
        /// never as a re-weighting of it.</summary>
        public double TipCeilingScale { get; private set; } = 1.0;

        /// <summary>× <see cref="ServiceJudge.AccuracyPayFloor"/> — the worst a correct drink can
        /// pay. A floor under a shaky hand; exactly nothing at a perfect pour.</summary>
        public double PayFloorScale { get; private set; } = 1.0;

        /// <summary>× <see cref="ServiceJudge.PerfectWindow"/> — how close the pour must be to
        /// reveal the page's exact numbers.</summary>
        public double PerfectWindowScale { get; private set; } = 1.0;

        /// <summary>× <see cref="ServiceJudge.RefusalFill"/> — the line under which they refuse to
        /// pay for it at all. Only ever scaled DOWN: the line is a step and not a curve, so a page
        /// that raised it could turn a good night into a hole.</summary>
        public double RefusalFillScale { get; private set; } = 1.0;

        /// <summary>Whole 4.4-second sip cycles on the stool. Three is the house's 13.2; a savour
        /// that is not a whole number of cycles cuts the last sip off mid-gesture (TycoonConfig).</summary>
        public int SavorCycles { get; private set; } = 3;

        /// <summary>Chance of opening a second round the deterministic judge withheld, on the
        /// "round" stream. Zero for every page but one character.</summary>
        public double GrantRound { get; private set; }

        /// <summary>Chance of taking back a second round the judge granted, on the same stream.</summary>
        public double TakeRound { get; private set; }

        /// <summary>Satisfaction this page lends every OTHER serve for <see cref="AuraSeconds"/>.
        /// Fired at the SERVE and not at the order: the order lives behind the ID card, and a room
        /// that brightened the moment somebody sat down would name their drink.</summary>
        public double RoomAura { get; private set; }

        /// <summary>The counter's three mark thresholds (BarDay's "mess" roll). The house's own
        /// triple is the identity; the SAME single draw is taken whatever the page is.</summary>
        public double Mess0 { get; private set; } = 0.25;
        public double Mess1 { get; private set; } = 0.65;
        public double Mess2 { get; private set; } = 0.90;

        /// <summary>How long a showy drink holds the room. ONE number for the whole game, not one
        /// per character: the aura is a room effect and the room has one clock.</summary>
        public const double AuraSeconds = 20.0;

        private DrinkTrait(string id, TraitSign sign, TraitChannel channel,
            string statKey = "", int statPercent = 0)
        {
            Id = id;
            Sign = sign;
            Channel = channel;
            StatKey = statKey;
            StatPercent = statPercent;
        }

        /// <summary>Whether this one has a figure to print. Only <see cref="None"/> has not.</summary>
        public bool HasStat => StatKey.Length != 0 && StatPercent != 0;

        private static DrinkTrait Buff(string id, TraitChannel channel) =>
            new DrinkTrait(id, TraitSign.Buff, channel);

        private static DrinkTrait Nerf(string id, TraitChannel channel) =>
            new DrinkTrait(id, TraitSign.Nerf, channel);

        public override string ToString() => Id.Length == 0 ? "no character" : Id;

        // ── the nineteen, each one a single field off its identity ───────────────────────────
        //
        // The rule the set is built on: ONE field per character. A page that moved two numbers
        // would be two characters wearing one name, and the balance pass could not tell which
        // half did the work.

        internal static DrinkTrait MakeNursed() =>
            new DrinkTrait("nursed", TraitSign.Buff, TraitChannel.Clock, "patience", 40) { AskBoxScale = 1.40 };

        internal static DrinkTrait MakeSameAgain() =>
            new DrinkTrait("same_again", TraitSign.Buff, TraitChannel.Clock, "refill", 19) { RefillScale = 1.1875 };

        internal static DrinkTrait MakeKeepsWell() =>
            new DrinkTrait("keeps_well", TraitSign.Buff, TraitChannel.Clock, "lateness", -20) { WaitPenaltyScale = 0.80 };

        internal static DrinkTrait MakeDrinkItHot() =>
            new DrinkTrait("drink_it_hot", TraitSign.Nerf, TraitChannel.Clock, "lateness", 20) { WaitPenaltyScale = 1.20 };

        internal static DrinkTrait MakeStillGoodLate() =>
            new DrinkTrait("still_good_late", TraitSign.Buff, TraitChannel.Clock, "late_tip", 20) { ClockFloorScale = 1.20 };

        internal static DrinkTrait MakeDiesWarm() =>
            new DrinkTrait("dies_warm", TraitSign.Nerf, TraitChannel.Clock, "late_tip", -20) { ClockFloorScale = 0.80 };

        internal static DrinkTrait MakeKnockedBack() =>
            new DrinkTrait("knocked_back", TraitSign.Buff, TraitChannel.Round, "stool", -33) { SavorCycles = 2 };

        internal static DrinkTrait MakeHoldsTheStool() =>
            new DrinkTrait("holds_the_stool", TraitSign.Nerf, TraitChannel.Round, "stool", 33) { SavorCycles = 4 };

        internal static DrinkTrait MakeNeverJustOne() =>
            new DrinkTrait("never_just_one", TraitSign.Buff, TraitChannel.Round, "round", 22) { GrantRound = 0.22 };

        internal static DrinkTrait MakeOneIsPlenty() =>
            new DrinkTrait("one_is_plenty", TraitSign.Nerf, TraitChannel.Round, "round", -20) { TakeRound = 0.20 };

        internal static DrinkTrait MakeMadeByFeel() =>
            new DrinkTrait("made_by_feel", TraitSign.Buff, TraitChannel.Craft, "pay_floor", 120) { PayFloorScale = 2.2 };

        internal static DrinkTrait MakeNoPlaceToHide() =>
            new DrinkTrait("no_place_to_hide", TraitSign.Nerf, TraitChannel.Craft, "pay_floor", -60) { PayFloorScale = 0.40 };

        internal static DrinkTrait MakeEasyToLearn() =>
            new DrinkTrait("easy_to_learn", TraitSign.Buff, TraitChannel.Craft, "window", 20) { PerfectWindowScale = 1.20 };

        internal static DrinkTrait MakeReadsFull() =>
            new DrinkTrait("reads_full", TraitSign.Buff, TraitChannel.Craft, "refusal", -20) { RefusalFillScale = 0.80 };

        internal static DrinkTrait MakeLeavesNoRing() =>
            new DrinkTrait("leaves_no_ring", TraitSign.Buff, TraitChannel.Counter, "mess", -19)
            { Mess0 = 0.38, Mess1 = 0.72, Mess2 = 0.93 };

        internal static DrinkTrait MakeWrecksTheBar() =>
            new DrinkTrait("wrecks_the_bar", TraitSign.Nerf, TraitChannel.Counter, "mess", 21)
            { Mess0 = 0.15, Mess1 = 0.55, Mess2 = 0.85 };

        internal static DrinkTrait MakeTheRoomLooksUp() =>
            new DrinkTrait("the_room_looks_up", TraitSign.Buff, TraitChannel.Room, "room", 5) { RoomAura = 0.04 };

        internal static DrinkTrait MakeTheyTipForThis() =>
            new DrinkTrait("they_tip_for_this", TraitSign.Buff, TraitChannel.Coin, "tip", 20) { TipCeilingScale = 1.20 };

        internal static DrinkTrait MakePaysTheBill() =>
            new DrinkTrait("pays_the_bill", TraitSign.Nerf, TraitChannel.Coin, "tip", -13) { TipCeilingScale = 0.87 };
    }

    /// <summary>
    /// THE BOOK'S CHARACTERS, and the one door to them. Nineteen of them, each moving exactly one
    /// number by between a twentieth and a quarter — the author's "etkisi az ile orta seviye
    /// arasında" — so the variety is in WHICH lever, never in how hard it is pulled.
    ///
    /// Thirteen pages carry none at all, on purpose: a book where every page has a character has
    /// no characters, only noise.
    /// </summary>
    public static class DrinkTraits
    {
        /// <summary>
        /// The A/B gate, and it belongs to the 200-run sim and to nobody else. With it false every
        /// page reads as <see cref="DrinkTrait.None"/>, so the same 200 seeds can be played twice
        /// and the difference is the characters and nothing else. It is never read by a rule.
        /// </summary>
        public static bool Enabled = true;

        private static readonly DrinkTrait[] Catalogue =
        {
            DrinkTrait.MakeNursed(),
            DrinkTrait.MakeSameAgain(),
            DrinkTrait.MakeKeepsWell(),
            DrinkTrait.MakeDrinkItHot(),
            DrinkTrait.MakeStillGoodLate(),
            DrinkTrait.MakeDiesWarm(),
            DrinkTrait.MakeKnockedBack(),
            DrinkTrait.MakeHoldsTheStool(),
            DrinkTrait.MakeNeverJustOne(),
            DrinkTrait.MakeOneIsPlenty(),
            DrinkTrait.MakeMadeByFeel(),
            DrinkTrait.MakeNoPlaceToHide(),
            DrinkTrait.MakeEasyToLearn(),
            DrinkTrait.MakeReadsFull(),
            DrinkTrait.MakeLeavesNoRing(),
            DrinkTrait.MakeWrecksTheBar(),
            DrinkTrait.MakeTheRoomLooksUp(),
            DrinkTrait.MakeTheyTipForThis(),
            DrinkTrait.MakePaysTheBill(),
        };

        private static readonly Dictionary<string, DrinkTrait> ById = BuildIndex();

        private static Dictionary<string, DrinkTrait> BuildIndex()
        {
            var map = new Dictionary<string, DrinkTrait>(Catalogue.Length);
            foreach (var trait in Catalogue) map[trait.Id] = trait;
            return map;
        }

        /// <summary>Every character in the book, in the order they were written.</summary>
        public static IReadOnlyList<DrinkTrait> All => Catalogue;

        /// <summary>Whether anybody has written this character. The loader asks before it accepts
        /// a page, so a typo in the data is a loud failure at parse and never a silent nothing.</summary>
        public static bool Known(string id) => !string.IsNullOrWhiteSpace(id) && ById.ContainsKey(id);

        /// <summary>The character with this id, or <see cref="DrinkTrait.None"/>.</summary>
        public static DrinkTrait Find(string id) =>
            id != null && ById.TryGetValue(id, out var trait) ? trait : DrinkTrait.None;

        /// <summary>
        /// The character of a page. <see cref="DrinkTrait.None"/> for a null recipe, a page with no
        /// character, and — this is the one that matters — a pour that matched nothing, since the
        /// counter's mess and the savour both reach here off the DELIVERED drink, which is null
        /// when the glass was not any recipe at all.
        /// </summary>
        public static DrinkTrait Of(RecipeDefinition recipe) =>
            Enabled && recipe != null ? Find(recipe.Trait) : DrinkTrait.None;
    }
}
