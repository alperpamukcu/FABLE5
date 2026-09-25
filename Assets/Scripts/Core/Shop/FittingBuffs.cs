using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>How a fitting buff's figure reads: a per cent of the thing it scales, points added
    /// to a chance or a satisfaction (a figure of 5 is +0.05), or a flag with no figure at all.</summary>
    public enum BuffUnit
    {
        Percent,
        Points,
        Flag,
    }

    /// <summary>
    /// ONE KIND OF FITTING BUFF (2026-09-23, the author: "Tüm upgrade'ler çeşitli bufflar vermeli
    /// ... kimi upgrade ürün fiyatını bufflar, konforu bufflar, bekleme süresini bufflar, tip'i
    /// bufflar, servisi bufflar ... Hangi geliştirme takılıysa o buff aktif olacak, konfor gibi
    /// değil").
    ///
    /// The kind is a RULE and lives here, the way a page's character lives in
    /// <see cref="DrinkTraits"/>: what the figure scales, which way is good, and how far one piece
    /// and the whole room may push it. WHICH piece carries it, and how much, is content — the
    /// <c>buff</c> / <c>buffPct</c> pair on a row of fixtures.json — and the loader refuses a row
    /// that names a kind nobody wrote here.
    ///
    /// One static instance per kind: compare by reference or by <see cref="Id"/>.
    /// </summary>
    public sealed class FittingBuff
    {
        /// <summary>The data key, the string table's tail (<c>book.trait.stat.&lt;Id&gt;</c>) and
        /// the icon's key — one word for all three, so a fitting and a page that move the same
        /// number print the same word under the same mark.</summary>
        public string Id { get; }

        /// <summary>The page characters' name for the same word (<see cref="DrinkTrait.StatKey"/>).</summary>
        public string StatKey => Id;

        /// <summary>Which of the menu's six levers it pulls: grouping, band order, icon fallback.</summary>
        public TraitChannel Channel { get; }

        /// <summary>+1 when up is good, −1 when down is good (lateness, the wash), 0 on a flag.
        /// A bought piece's figure must point this way, and the UI colours by goodness, never
        /// by the sign.</summary>
        public int Direction { get; }

        public BuffUnit Unit { get; }

        /// <summary>The most one fixtures.json row may carry, as |buffPct|. Zero for the tools,
        /// whose figure is never written in the data.</summary>
        public int MaxPerPiece { get; }

        /// <summary>The most the whole installed room may add up to, as |Σ|. The shipped full
        /// room lands exactly on it (a shipped-data test holds that), so the clamp is a guard
        /// for future content, not a rule anybody plays against today.</summary>
        public int HouseCap { get; }

        /// <summary>A tool's buff: its figure is READ OFF the tool — <see cref="FixtureDefinition.WorkSpeed"/>,
        /// <see cref="FixtureDefinition.WashSeconds"/>, <see cref="FixtureDefinition.DrainsFree"/> —
        /// and the data only names it, at 0, so no number is ever written twice.</summary>
        public bool IsTool { get; }

        /// <summary>
        /// Acts only while nobody else is waiting (CROWD and the house's SECOND ROUND). Measured
        /// 2026-09-23 on eighty seeded nights a cell: unconditional, both were pure profit while
        /// the door was the bar's limit and COST a competent hand up to a tenth of a star once the
        /// bartender was — more people sat, or stayed, than the bar could serve. A badge that
        /// quietly hurts its buyer breaks the badge, so the room waits for a free bartender.
        /// Pages keep their own unconditional rules.
        /// </summary>
        public bool WhileKeepingUp { get; }

        /// <summary>The kind's slot in <see cref="HouseBuffs"/>' arrays: its place in <see cref="FittingBuffs.All"/>.</summary>
        internal int Index { get; }

        internal FittingBuff(int index, string id, TraitChannel channel, int direction, BuffUnit unit,
            int maxPerPiece, int houseCap, bool isTool = false, bool whileKeepingUp = false)
        {
            Index = index;
            Id = id;
            Channel = channel;
            Direction = direction;
            Unit = unit;
            MaxPerPiece = maxPerPiece;
            HouseCap = houseCap;
            IsTool = isTool;
            WhileKeepingUp = whileKeepingUp;
        }

        public override string ToString() => Id;
    }

    /// <summary>
    /// One effect of one piece, as the market, the upgrade screen and the hover print it: the kind
    /// and its signed figure. A data kind's figure is the row's own; a tool's is derived from the
    /// tool against the one the slot opens with (<see cref="FittingBuffs.EffectsOf"/>).
    /// </summary>
    public readonly struct FittingEffect
    {
        public FittingBuff Kind { get; }

        /// <summary>Signed, in the kind's unit; 0 on a flag and on the house standard.</summary>
        public int Percent { get; }

        /// <summary>A flag prints alone, with no figure (FREE DRAIN).</summary>
        public bool IsFlag => Kind != null && Kind.Unit == BuffUnit.Flag;

        /// <summary>THE HOUSE STANDARD: a piece the room opens with, whose figure is the zero the
        /// ladder above it climbs from. Shown, so night one already says what each ladder buys.</summary>
        public bool IsBase => !IsFlag && Percent == 0;

        public FittingEffect(FittingBuff kind, int percent)
        {
            Kind = kind;
            Percent = percent;
        }

        public override string ToString() => Kind == null ? "none"
            : IsFlag ? Kind.Id : $"{Kind.Id} {(Percent >= 0 ? "+" : "")}{Percent}";
    }

    /// <summary>
    /// THE ROOM'S BUFFS, the one door to them (2026-09-23). Sixteen kinds: twelve the data names
    /// with a figure, and four the tools already carried as fields before any of this existed.
    ///
    /// Every hook is a SCALE on a constant that already stands at its call site, exactly the
    /// pattern <see cref="DrinkTraits"/> set: a room with no buffs computes bit for bit what it
    /// computed before this file (<see cref="HouseBuffs.None"/> is every identity), and no kind
    /// draws a random number.
    /// </summary>
    public static class FittingBuffs
    {
        // ── the twelve the data carries, in band order: Coin, Clock, Round, Room, Counter, Craft ──
        public static readonly FittingBuff Price =
            new FittingBuff(0, "price", TraitChannel.Coin, +1, BuffUnit.Percent, 12, 12);
        public static readonly FittingBuff Tip =
            new FittingBuff(1, "tip", TraitChannel.Coin, +1, BuffUnit.Percent, 15, 15);
        public static readonly FittingBuff Patience =
            new FittingBuff(2, "patience", TraitChannel.Clock, +1, BuffUnit.Percent, 12, 20);
        public static readonly FittingBuff Lateness =
            new FittingBuff(3, "lateness", TraitChannel.Clock, -1, BuffUnit.Percent, 20, 20);
        public static readonly FittingBuff LateTip =
            new FittingBuff(4, "late_tip", TraitChannel.Clock, +1, BuffUnit.Percent, 20, 20);
        public static readonly FittingBuff Refill =
            new FittingBuff(5, "refill", TraitChannel.Clock, +1, BuffUnit.Percent, 20, 20);
        public static readonly FittingBuff Round =
            new FittingBuff(6, "round", TraitChannel.Round, +1, BuffUnit.Points, 5, 10, whileKeepingUp: true);
        /// <summary>CROWD on the card: the door opens a little faster.</summary>
        public static readonly FittingBuff Arrivals =
            new FittingBuff(7, "arrivals", TraitChannel.Room, +1, BuffUnit.Percent, 10, 10, whileKeepingUp: true);
        public static readonly FittingBuff Service =
            new FittingBuff(8, "service", TraitChannel.Room, +1, BuffUnit.Points, 5, 5);
        public static readonly FittingBuff Comfort =
            new FittingBuff(9, "comfort", TraitChannel.Room, +1, BuffUnit.Percent, 10, 10);
        /// <summary>CLEAN-UP TIME on the card: how long a glass or a mark may stand before it costs.</summary>
        public static readonly FittingBuff Grace =
            new FittingBuff(10, "grace", TraitChannel.Counter, +1, BuffUnit.Percent, 60, 80);
        public static readonly FittingBuff Window =
            new FittingBuff(11, "window", TraitChannel.Craft, +1, BuffUnit.Percent, 40, 40);

        // ── the four the tools already carried: named in the data at 0, read off the tool ──
        public static readonly FittingBuff ShakeSpeed =
            new FittingBuff(12, "shake_speed", TraitChannel.Craft, +1, BuffUnit.Percent, 0, 0, isTool: true);
        public static readonly FittingBuff PourSpeed =
            new FittingBuff(13, "pour_speed", TraitChannel.Craft, +1, BuffUnit.Percent, 0, 0, isTool: true);
        public static readonly FittingBuff Wash =
            new FittingBuff(14, "wash", TraitChannel.Counter, -1, BuffUnit.Percent, 0, 0, isTool: true);
        public static readonly FittingBuff FreeDrain =
            new FittingBuff(15, "free_drain", TraitChannel.Counter, 0, BuffUnit.Flag, 0, 0, isTool: true);

        private static readonly FittingBuff[] Catalogue =
        {
            Price, Tip, Patience, Lateness, LateTip, Refill, Round, Arrivals, Service, Comfort, Grace, Window,
            ShakeSpeed, PourSpeed, Wash, FreeDrain,
        };

        private static readonly FittingBuff[] Data =
        {
            Price, Tip, Patience, Lateness, LateTip, Refill, Round, Arrivals, Service, Comfort, Grace, Window,
        };

        private static readonly Dictionary<string, FittingBuff> ById = BuildIndex();

        private static Dictionary<string, FittingBuff> BuildIndex()
        {
            var map = new Dictionary<string, FittingBuff>(Catalogue.Length, StringComparer.Ordinal);
            foreach (var kind in Catalogue) map[kind.Id] = kind;
            return map;
        }

        private static readonly FittingEffect[] NoEffects = Array.Empty<FittingEffect>();

        /// <summary>All sixteen, in BAND order: Coin, Clock, Round, Room, Counter, Craft, then the tools.</summary>
        public static IReadOnlyList<FittingBuff> All => Catalogue;

        /// <summary>The twelve the data names with a figure.</summary>
        public static IReadOnlyList<FittingBuff> DataKinds => Data;

        /// <summary>The kind with this id, or null. Null for a blank id as well.</summary>
        public static FittingBuff Find(string id) =>
            id != null && ById.TryGetValue(id.Trim(), out var kind) ? kind : null;

        /// <summary>Whether anybody has written this kind — any of the sixteen. The loader asks
        /// before it accepts a row, so a typo is a loud failure at parse and never a silent nothing.</summary>
        public static bool Known(string id) => Find(id) != null;

        /// <summary>
        /// A piece's effects in card order, lead first. EMPTY for a row that names no kind — the
        /// older inline test JSON does that, and so could a future piece the author has not
        /// decided on. A data kind is the row's own figure; a tool's is derived:
        /// <list type="bullet">
        /// <item>SHAKE / POUR SPEED: the piece's WorkSpeed against the slot's foot, as a per cent.</item>
        /// <item>WASH TIME: the piece's wash seconds against the foot's (0 seconds reads as the
        /// house's <see cref="Housekeeping.WashSeconds"/>).</item>
        /// <item>FREE DRAIN is appended to a drain that waives the write-off, as a flag.</item>
        /// </list>
        /// The FOOT is the slot's starts-in-the-room piece; without one, the slot's lowest rung;
        /// with no catalogue, the piece itself.
        /// </summary>
        public static IReadOnlyList<FittingEffect> EffectsOf(FixtureDefinition f,
            IReadOnlyList<FixtureDefinition> catalogue)
        {
            if (f == null || f.Buff == null) return NoEffects;
            var effects = new List<FittingEffect>(2);
            var kind = f.Buff;
            if (!kind.IsTool)
                effects.Add(new FittingEffect(kind, f.BuffPct));
            else if (ReferenceEquals(kind, ShakeSpeed) || ReferenceEquals(kind, PourSpeed))
            {
                var foot = FootOf(f, catalogue);
                effects.Add(new FittingEffect(kind, RoundAway((f.WorkSpeed / foot.WorkSpeed - 1.0) * 100.0)));
            }
            else if (ReferenceEquals(kind, Wash))
            {
                var foot = FootOf(f, catalogue);
                effects.Add(new FittingEffect(Wash, RoundAway((WashOf(f) / WashOf(foot) - 1.0) * 100.0)));
            }
            if (f.DrainsFree) effects.Add(new FittingEffect(FreeDrain, 0));
            return effects;
        }

        /// <summary>
        /// WHAT THE LOADER REFUSES ACROSS ROWS (2026-09-23), or null for a catalogue that is fine.
        /// A row's own figure is checked by <see cref="FixtureDefinition"/>'s constructor; these two
        /// need the whole slot:
        /// <list type="number">
        /// <item>ONE SLOT, ONE STAT. Every row of a slot that names a kind names the same one, so a
        /// ladder reads as a single number climbing and a swap between two owned rungs never trades
        /// one kind of help for another.</item>
        /// <item>EVERY RUNG CARRIES MORE. Up a ladder, a data kind's |figure| strictly rises — a rung
        /// that repeated the one below would sell the player nothing. The opening rung's 0 counts as
        /// the lowest.</item>
        /// </list>
        /// A row that names no kind is allowed here; the shipped-data test is what requires all of them.
        /// Pure, so a bench test can ask it without the loader.
        /// </summary>
        public static string CatalogueFault(IEnumerable<FixtureDefinition> catalogue)
        {
            if (catalogue == null) return null;
            var kindOf = new Dictionary<string, FittingBuff>(StringComparer.Ordinal);
            var rungsOf = new Dictionary<string, List<FixtureDefinition>>(StringComparer.Ordinal);
            var slots = new List<string>();
            foreach (var f in catalogue)
            {
                if (f == null || f.Buff == null) continue;
                if (kindOf.TryGetValue(f.Slot, out var held))
                {
                    if (!ReferenceEquals(held, f.Buff))
                        return $"Slot '{f.Slot}' carries two kinds of buff ('{held.Id}', '{f.Buff.Id}'): " +
                               "one slot, one stat.";
                }
                else
                {
                    kindOf[f.Slot] = f.Buff;
                    slots.Add(f.Slot);
                }
                if (f.Level > 0 && !f.Buff.IsTool)
                {
                    if (!rungsOf.TryGetValue(f.Slot, out var rungs)) rungsOf[f.Slot] = rungs = new List<FixtureDefinition>();
                    rungs.Add(f);
                }
            }
            foreach (var slot in slots)
            {
                if (!rungsOf.TryGetValue(slot, out var rungs) || rungs.Count < 2) continue;
                rungs.Sort((a, b) => a.Level.CompareTo(b.Level));
                for (int i = 1; i < rungs.Count; i++)
                {
                    if (Math.Abs(rungs[i].BuffPct) > Math.Abs(rungs[i - 1].BuffPct)) continue;
                    var figures = new List<string>(rungs.Count);
                    foreach (var r in rungs) figures.Add(r.BuffPct.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    return $"The '{kindOf[slot].Id}' figures up slot '{slot}' run {string.Join(", ", figures)}: " +
                           "every rung must carry more than the one below.";
                }
            }
            return null;
        }

        /// <summary>The piece a slot's tool figures are measured against: the one the room opens
        /// with, else the lowest rung, else the piece itself.</summary>
        private static FixtureDefinition FootOf(FixtureDefinition f, IReadOnlyList<FixtureDefinition> catalogue)
        {
            if (catalogue == null) return f;
            FixtureDefinition lowest = null;
            foreach (var p in catalogue)
            {
                if (p == null || p.Slot != f.Slot) continue;
                if (p.StartsInTheRoom) return p;
                if (lowest == null || p.Level < lowest.Level) lowest = p;
            }
            return lowest ?? f;
        }

        private static double WashOf(FixtureDefinition p) =>
            p.WashSeconds > 0 ? p.WashSeconds : Housekeeping.WashSeconds;

        private static int RoundAway(double x) => (int)Math.Round(x, MidpointRounding.AwayFromZero);
    }
}
