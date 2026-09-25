using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// WHAT THE INSTALLED ROOM DOES FOR THE BAR (2026-09-23, the author: "kimi upgrade ürün
    /// fiyatını bufflar, konforu bufflar, bekleme süresini bufflar, tip'i bufflar, servisi bufflar
    /// ... Hangi geliştirme takılıysa o buff aktif olacak, konfor gibi değil").
    ///
    /// The sum, per kind, of the figures the installed pieces carry — the rung each ladder WEARS
    /// and every owned single piece (<see cref="TycoonRun.IsActive"/>) — clamped to the kind's
    /// <see cref="FittingBuff.HouseCap"/>, and read at each hook as a SCALE on a constant that
    /// already stands there. <see cref="None"/> is every identity: <c>1.0 + 0/100.0 == 1.0</c>,
    /// <c>1.0/1.0 == 1.0</c>, <c>x * 1.0 == x</c> and <c>x + 0.0 == x</c> all hold in IEEE-754,
    /// so a bare room computes today's doubles bit for bit. That is the page characters' safety
    /// argument (<see cref="DrinkTraits"/>), made again.
    ///
    /// Immutable: the run builds a fresh one when the room changes, and the room cannot change
    /// while the doors are open (<see cref="TycoonRun.WearFixture"/> refuses outside the day's end),
    /// so everything a night reads is frozen for the night by construction.
    /// </summary>
    public sealed class HouseBuffs
    {
        /// <summary>
        /// The 200-run sim's A/B gate, and nobody else's. False: <see cref="From"/> returns
        /// <see cref="None"/>, so the same seeds can be played with and without the room's buffs and
        /// the difference is the buffs and nothing else. It gates the twelve DATA kinds only — the
        /// tools and the sink's fix are not behind it, because they were rules before this existed.
        /// No rule reads it.
        /// </summary>
        public static bool Enabled = true;

        /// <summary>Nothing installed that helps: every percent 0, every scale exactly 1.0.</summary>
        public static readonly HouseBuffs None = new HouseBuffs(null);

        private static readonly FixtureDefinition[] NoSources = Array.Empty<FixtureDefinition>();

        private readonly int[] _raw = new int[FittingBuffs.All.Count];
        private readonly List<FixtureDefinition>[] _sources = new List<FixtureDefinition>[FittingBuffs.All.Count];

        private HouseBuffs(IEnumerable<FixtureDefinition> installed)
        {
            if (installed == null) return;
            foreach (var f in installed)
            {
                if (f == null || f.Buff == null || f.Buff.IsTool || f.BuffPct == 0) continue;
                int i = f.Buff.Index;
                _raw[i] += f.BuffPct;
                (_sources[i] ?? (_sources[i] = new List<FixtureDefinition>())).Add(f);
            }
        }

        /// <summary>The room these pieces make: Σ of their data kinds' figures. <see cref="None"/>
        /// when the A/B gate is off or nothing carries a figure.</summary>
        public static HouseBuffs From(IEnumerable<FixtureDefinition> installed)
        {
            if (!Enabled || installed == null) return None;
            var house = new HouseBuffs(installed);
            return house.IsNone ? None : house;
        }

        /// <summary>
        /// The room a bar WOULD wear having bought every piece its standing opens: per slot the
        /// highest open rung, every open single, and every piece the room opens with. What the
        /// projection's fitted column reads, and what the shipped-data test holds against the caps.
        /// </summary>
        public static HouseBuffs FullRoomAt(IReadOnlyList<FixtureDefinition> catalogue, double stars)
        {
            if (catalogue == null) return None;
            var top = new Dictionary<string, FixtureDefinition>(StringComparer.Ordinal);
            var room = new List<FixtureDefinition>();
            foreach (var f in catalogue)
            {
                if (f == null) continue;
                bool open = f.StartsInTheRoom || f.Stars <= stars + 1e-9;
                if (!open) continue;
                if (f.Level <= 0) { room.Add(f); continue; }
                if (!top.TryGetValue(f.Slot, out var held) || f.Level > held.Level) top[f.Slot] = f;
            }
            room.AddRange(top.Values);
            return From(room);
        }

        /// <summary>The sum over the installed room, unclamped — so a screen can say "capped".
        /// Always 0 for a tool, whose figure is the tool's own.</summary>
        public int RawPercent(FittingBuff kind) => kind == null ? 0 : _raw[kind.Index];

        /// <summary>The sum, clamped to ±<see cref="FittingBuff.HouseCap"/>: what the hooks read.</summary>
        public int Percent(FittingBuff kind)
        {
            if (kind == null) return 0;
            int raw = _raw[kind.Index];
            return Math.Max(-kind.HouseCap, Math.Min(kind.HouseCap, raw));
        }

        /// <summary>The installed pieces that make this kind's figure, in the room's order.</summary>
        public IReadOnlyList<FixtureDefinition> Sources(FittingBuff kind) =>
            kind == null ? NoSources : (IReadOnlyList<FixtureDefinition>)_sources[kind.Index] ?? NoSources;

        /// <summary>Every kind reads 0: the room as it opens.</summary>
        public bool IsNone
        {
            get
            {
                foreach (var kind in FittingBuffs.DataKinds)
                    if (Percent(kind) != 0) return false;
                return true;
            }
        }

        // ── the scales, one per hook. Identity is exactly 1.0, or exactly 0.0 for an add. ──

        /// <summary>× the till's price of every drink (TycoonRun.PriceOf, and the page's PagePrice).</summary>
        public double PriceScale => 1.0 + Percent(FittingBuffs.Price) / 100.0;

        /// <summary>× <see cref="ServiceJudge.TipCeiling"/>, beside a page's own scale.</summary>
        public double TipScale => 1.0 + Percent(FittingBuffs.Tip) / 100.0;

        /// <summary>× the one "patience" roll an arrival takes, before the card is read — the same
        /// for every order, so it names nobody's drink.</summary>
        public double PatienceScale => 1.0 + Percent(FittingBuffs.Patience) / 100.0;

        /// <summary>× the lateness penalty on satisfaction. At most 1: the kind points down.</summary>
        public double WaitPenaltyScale => 1.0 + Percent(FittingBuffs.Lateness) / 100.0;

        /// <summary>× <see cref="ServiceJudge.ClockFloor"/>: pays only a serve that is already late.</summary>
        public double ClockFloorScale => 1.0 + Percent(FittingBuffs.LateTip) / 100.0;

        /// <summary>× the clock a second round starts on.</summary>
        public double RefillScale => 1.0 + Percent(FittingBuffs.Refill) / 100.0;

        /// <summary>Chance, in points, added to a page's own grant of a second round on the SAME
        /// "round" roll — and only while nobody else is waiting (<see cref="BarDay.KeepingUp"/>).</summary>
        public double RoundChance => Percent(FittingBuffs.Round) / 100.0;

        /// <summary>Satisfaction added at the serve's call site, beside the glassware's ambience.</summary>
        public double ServiceBonus => Percent(FittingBuffs.Service) / 100.0;

        /// <summary>× the gap between arrivals: a +10% crowd is a gap of 1/1.10. Read only while the
        /// bar is keeping up (<see cref="BarDay.KeepingUp"/>).</summary>
        public double ArrivalGapScale => 1.0 / (1.0 + Percent(FittingBuffs.Arrivals) / 100.0);

        /// <summary>× the night's filed comfort, AFTER the mess — a cushion, never a base
        /// multiplier, which would go dead the moment a room passed the ceiling.</summary>
        public double ComfortScale => 1.0 + Percent(FittingBuffs.Comfort) / 100.0;

        /// <summary>× <see cref="Housekeeping.DirtGrace"/>: how long a glass or a mark may stand.</summary>
        public double GraceScale => 1.0 + Percent(FittingBuffs.Grace) / 100.0;

        /// <summary>× <see cref="ServiceJudge.PerfectWindow"/>, beside a page's own scale.</summary>
        public double PerfectWindowScale => 1.0 + Percent(FittingBuffs.Window) / 100.0;

        public override string ToString()
        {
            var parts = new List<string>();
            foreach (var kind in FittingBuffs.DataKinds)
            {
                int p = Percent(kind);
                if (p != 0) parts.Add($"{kind.Id} {(p > 0 ? "+" : "")}{p}");
            }
            return parts.Count == 0 ? "the house standard" : string.Join(", ", parts);
        }
    }
}
