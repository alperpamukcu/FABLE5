using System;
using System.Collections.Generic;
using System.Linq;

namespace LastCall.Core
{
    /// <summary>
    /// WHAT THE NIGHT IS GOING TO ASK FOR (2026-09-23, the author's economy brief).
    ///
    /// > *"her gün ekonomiye göre nasıl siparişler gelecek oyuncu hangi gün ne kadar kazanabilecek
    /// > bunların hesaplamalarını yap, tamamen rastgele değil bir düzen içerisinde rastgele olmalı,
    /// > her şeyi rastgele yapıp oyuncuya kötü şans sonucu kötü bir oynanış sunmak istemiyorum."*
    ///
    /// The old roll was one line: take the lowest-ranked <c>3 + day</c> pages the bar can pour and
    /// pick one uniformly. Two things came out of that, and both are the reason this class exists.
    ///
    /// The pool was ordered by RANK and capped by the DAY, so a bar that climbed to three stars on
    /// day four owned twenty-one pages and was asked for seven of them — the ones it had opened
    /// with. Everything the player bought to get there was unorderable for a fortnight. And the pick
    /// was uniform over that pool, so a night's takings were a coin-flip: the same bar, the same
    /// work, could bank $40 or $95 depending on how the dice fell. That is exactly the *"kötü şans
    /// sonucu kötü bir oynanış"* the brief rules out.
    ///
    /// A plan is the answer. The night's COMPOSITION is decided up front — how many covers, how they
    /// split across the rungs the bar has opened, and how many of them are easy, ordinary or hard —
    /// and only the ORDER is shuffled. So two nights at the same standing earn within a few dollars
    /// of each other and still play differently, which is what "random inside a pattern" means.
    ///
    /// Nothing here reads a stool or a clock: it is a list of pages, handed out one at a time as the
    /// door opens. <see cref="TycoonRun"/> builds one per night on the "plan" stream.
    /// </summary>
    public sealed class DayPlan
    {
        /// <summary>
        /// WHICH RUNG A COVER COMES FROM, counted DOWN from the bar's own. A three-star bar spends
        /// nearly half its night on its three-star pages, a third on its two-star ones, and the tail
        /// keeps the opening menu alive — a Vodka &amp; Soda is still a drink somebody orders.
        ///
        /// This is the shape that pays for climbing. The pages of the rung you just reached are the
        /// dearest ones you own AND the ones most of the room asks for, so buying up moves the whole
        /// night's takings rather than adding one expensive drink to a night of cheap ones. The tail
        /// is what stops it being a cliff: a bar that has just climbed still pours what it knows.
        /// </summary>
        public static readonly int[] RungWeights = { 45, 30, 15, 7, 2, 1 };

        /// <summary>
        /// HOW HARD THE NIGHT IS, by the bar's rung. A one-star room is a room of highballs; by five
        /// stars three covers in ten are the hard pages. The brief asks for exactly this — *"oyuncuya
        /// yüksek yıldızlı zor kokteyl yapmanın ödülü de olmalı"* — and the reward is in two places
        /// at once: the hard band pays more (<see cref="DrinkPricing"/>) and the crowd asks for it
        /// more often, so the skill the player built has somewhere to go.
        ///
        /// It also protects the opening night, which is the fragile one. At zero stars nothing hard
        /// is ever asked for: the first shift is eighty-five per cent two-pour drinks, and the player
        /// learns the bench before the book asks anything of them.
        /// </summary>
        private static readonly int[,] WorkMix =
        {
            //  easy  medium  hard
            {   85,    15,     0 },   // 0★
            {   70,    25,     5 },   // 1★
            {   55,    33,    12 },   // 2★
            {   45,    38,    17 },   // 3★
            {   33,    42,    25 },   // 4★
            {   25,    45,    30 },   // 5★
        };

        /// <summary>
        /// The most of one night any single page may be. Without it a rung with one page in it eats
        /// that rung's whole share — a bar that has just opened its first three-star recipe would be
        /// asked for it five times in nine, which reads as the game nagging rather than as a crowd.
        /// What does not fit spills to the rung below, which is where a real room's orders go.
        /// </summary>
        public const double PageCap = 0.18;

        /// <summary>The fewest covers a plan is ever cut for. Below this the largest-remainder split
        /// stops being able to express the mix at all — two covers cannot be 45/33/12.</summary>
        public const int MinCovers = 6;

        private readonly RecipeDefinition[] _queue;
        private int _next;

        private DayPlan(int day, double stars, RecipeDefinition[] queue)
        {
            Day = day;
            Stars = stars;
            _queue = queue;
        }

        /// <summary>The night this plan was cut for.</summary>
        public int Day { get; }

        /// <summary>The standing it was cut against.</summary>
        public double Stars { get; }

        /// <summary>How many drinks the night was planned to sell.</summary>
        public int Covers => _queue.Length;

        /// <summary>The night's list, in the order it will be asked for. The tests read it; the run
        /// takes from it one cover at a time.</summary>
        public IReadOnlyList<RecipeDefinition> Queue => _queue;

        /// <summary>
        /// The next page the door is going to ask for. A night that outlasts its plan — the player
        /// is fast, the stools keep emptying — starts the same list again rather than falling back
        /// to a dice roll: the composition is what was planned, and serving MORE of it is the reward
        /// for working quickly, not a different night.
        /// </summary>
        public RecipeDefinition Take()
        {
            var pick = _queue[_next % _queue.Length];
            _next++;
            return pick;
        }

        /// <summary>
        /// HOW MANY DRINKS A NIGHT SELLS, asked of the same numbers the floor uses, so the plan is
        /// cut for the night that is actually going to happen. The door admits one drinker every
        /// <see cref="TycoonConfig.ArrivalGap"/> seconds and the shift runs
        /// <see cref="TycoonConfig.NightSeconds"/>; a busy bar tops up by re-reading its plan.
        /// <paramref name="gapScale"/> is the installed room's CROWD (2026-09-23), the floor's own
        /// scale: the plan is cut for the upper bound, since the floor applies it only while the
        /// bar keeps up and the plan wraps whatever happens. 1 is the door as it was.
        /// </summary>
        public static int CoversFor(int day, double stars, TycoonConfig config, double gapScale = 1.0)
        {
            double gap = Math.Max(0.5, config.ArrivalGap(day, stars) * gapScale);
            return Math.Max(MinCovers, (int)Math.Round(config.NightSeconds / gap,
                MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// Cuts the night. <paramref name="menu"/> is what the bar can actually pour tonight —
        /// unlocked, ratio-graded, and with the extras it owns — and the plan never invents a page
        /// that is not on it.
        /// </summary>
        public static DayPlan Roll(IReadOnlyList<RecipeDefinition> menu, int day, double stars,
            TycoonConfig config, SeededRng rng, double gapScale = 1.0)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            if (config == null) throw new ArgumentNullException(nameof(config));
            var pool = menu.Where(r => r != null && r.RatioRequirements.Count > 0).ToList();
            if (pool.Count == 0)
                throw Said.With(new InvalidOperationException("No drinks you can pour yet."),
                    Line.Of("rule.no_pourable_drinks"));

            // THE FIRST WEEK IS WRITTEN, NOT ROLLED. It is the only part of the game where the
            // arithmetic is wrong on purpose: a new bar owns six pages, and a plan cut from six
            // pages is six pages in a bag whatever the weights say. The week teaches instead.
            var written = FirstWeek.For(day, stars, pool, config);
            if (written != null) return Shuffled(day, stars, written, rng);

            int covers = CoversFor(day, stars, config, gapScale);
            int barRung =Math.Max(0, Math.Min(DrinkPricing.Rungs - 1, (int)Math.Floor(stars)));

            // THE BOOK, SORTED THE WAY THE PLAN READS IT. A page above the bar's rung can only be
            // here if something unlocked it early (the story's own pages do); it counts as the
            // bar's own rung rather than being dropped, because a page on the menu is a page the
            // room can ask for.
            var byRung = new List<RecipeDefinition>[DrinkPricing.Rungs];
            for (int i = 0; i < byRung.Length; i++) byRung[i] = new List<RecipeDefinition>();
            foreach (var r in pool.OrderBy(r => r.Rank).ThenBy(r => r.Id, StringComparer.Ordinal))
                byRung[Math.Min(barRung, DrinkPricing.RungOf(r))].Add(r);

            // AND TURNED BY THE DAY. Everything below this line is arithmetic, which is the point —
            // but arithmetic on a fixed list gives every night the same twelve pages, and a menu of
            // forty would show the player about a quarter of itself for ever. The rung's list is
            // rotated a place a night, so which pages take its share moves through the whole rung
            // over a week without a single die being thrown. A bar with a big book sees its book.
            foreach (var rung in byRung)
                if (rung.Count > 1)
                {
                    int turn = ((day % rung.Count) + rung.Count) % rung.Count;
                    var turned = rung.Skip(turn).Concat(rung.Take(turn)).ToList();
                    rung.Clear();
                    rung.AddRange(turned);
                }

            int[] rungCount = Split(covers, RungShares(byRung, barRung));
            int[] workCount = Split(covers, new[]
            {
                WorkMix[barRung, 0], WorkMix[barRung, 1], WorkMix[barRung, 2],
            });

            // The cap is a ceiling on ONE page, so a two-page menu cannot be held to it — the floor
            // of covers/pages is what "as spread as this book allows" means.
            int cap = Math.Max((int)Math.Ceiling(covers / (double)pool.Count),
                (int)Math.Round(covers * PageCap, MidpointRounding.AwayFromZero));

            var used = new Dictionary<string, int>(StringComparer.Ordinal);
            var queue = new RecipeDefinition[covers];
            for (int slot = 0; slot < covers; slot++)
            {
                int rung = PickRung(rungCount, byRung, used, cap, barRung);
                var page = PickPage(byRung[rung], workCount, used, cap)
                           ?? PickPage(byRung[rung], workCount, used, int.MaxValue);
                if (page == null)          // that rung is empty after all — take anything on the menu
                {
                    page = PickPage(pool, workCount, used, int.MaxValue) ?? pool[0];
                    rung = Math.Min(barRung, DrinkPricing.RungOf(page));
                }
                rungCount[rung] = Math.Max(0, rungCount[rung] - 1);
                int work = Math.Max(0, Math.Min(2, (int)RecipeDifficulty.Of(page) - 1));
                workCount[work] = Math.Max(0, workCount[work] - 1);
                used[page.Id] = used.TryGetValue(page.Id, out int n) ? n + 1 : 1;
                queue[slot] = page;
            }

            return Shuffled(day, stars, queue, rng);
        }

        /// <summary>
        /// AND ONLY NOW THE DICE. Everything that built the list was arithmetic — the same menu on
        /// the same night always plans the same covers — and the one random thing about a night is
        /// the order they walk in. That is the whole of *"bir düzen içerisinde rastgele"*.
        /// </summary>
        private static DayPlan Shuffled(int day, double stars, RecipeDefinition[] queue, SeededRng rng)
        {
            for (int i = queue.Length - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                (queue[i], queue[j]) = (queue[j], queue[i]);
            }
            return new DayPlan(day, stars, queue);
        }

        /// <summary>The rung weights, renormalised over the rungs that actually have pages on
        /// them — a bar with nothing on its second rung gives that share to its first.</summary>
        private static int[] RungShares(List<RecipeDefinition>[] byRung, int barRung)
        {
            var shares = new int[byRung.Length];
            for (int rung = 0; rung <= barRung; rung++)
            {
                if (byRung[rung].Count == 0) continue;
                int delta = barRung - rung;
                shares[rung] = delta < RungWeights.Length ? RungWeights[delta] : 1;
            }
            if (shares.Sum() == 0)
                for (int rung = 0; rung < byRung.Length; rung++)
                    if (byRung[rung].Count > 0) shares[rung] = 1;
            return shares;
        }

        /// <summary>Largest remainder: <paramref name="total"/> split across the shares so the parts
        /// sum to exactly the total and the same shares always split the same way.</summary>
        public static int[] Split(int total, IReadOnlyList<int> shares)
        {
            var counts = new int[shares.Count];
            int sum = shares.Sum();
            if (sum <= 0 || total <= 0) return counts;

            var remainder = new double[shares.Count];
            int handed = 0;
            for (int i = 0; i < shares.Count; i++)
            {
                double exact = total * shares[i] / (double)sum;
                counts[i] = (int)Math.Floor(exact);
                remainder[i] = exact - counts[i];
                handed += counts[i];
            }
            // Ties go to the earlier share, which for the rungs is the one nearest the ground: a
            // spare cover belongs to the drink more of the room can be bothered with.
            var order = Enumerable.Range(0, shares.Count)
                .OrderByDescending(i => remainder[i]).ThenBy(i => i).ToList();
            for (int k = 0; handed < total; k++, handed++) counts[order[k % order.Count]]++;
            return counts;
        }

        /// <summary>The rung with the most of its share still owing, skipping any that is empty or
        /// whose every page has already been asked for as often as the cap allows.</summary>
        private static int PickRung(int[] rungCount, List<RecipeDefinition>[] byRung,
            Dictionary<string, int> used, int cap, int barRung)
        {
            int best = -1;
            for (int rung = 0; rung < byRung.Length; rung++)
            {
                if (byRung[rung].Count == 0) continue;
                if (rungCount[rung] <= 0) continue;
                if (byRung[rung].All(p => used.TryGetValue(p.Id, out int n) && n >= cap)) continue;
                if (best < 0 || rungCount[rung] > rungCount[best]) best = rung;
            }
            if (best >= 0) return best;
            // Every rung's share is spent (or capped out) and there are covers left: spill DOWN,
            // toward the pages the whole room drinks, and only then up.
            for (int rung = barRung; rung >= 0; rung--)
                if (byRung[rung].Count > 0
                    && byRung[rung].Any(p => !used.TryGetValue(p.Id, out int n) || n < cap))
                    return rung;
            for (int rung = 0; rung < byRung.Length; rung++)
                if (byRung[rung].Count > 0) return rung;
            return 0;
        }

        /// <summary>Within a rung: the page whose KIND of work the night still owes most of, then
        /// the one asked for least so far, then the book's own order. No dice — the spread inside a
        /// rung is a fact about the menu, not a roll.</summary>
        private static RecipeDefinition PickPage(IReadOnlyList<RecipeDefinition> pages,
            int[] workCount, Dictionary<string, int> used, int cap)
        {
            RecipeDefinition best = null;
            int bestWork = -1, bestUse = int.MaxValue;
            foreach (var page in pages)
            {
                int seen = used.TryGetValue(page.Id, out int n) ? n : 0;
                if (seen >= cap) continue;
                int work = Math.Max(0, Math.Min(2, (int)RecipeDifficulty.Of(page) - 1));
                int owing = workCount[work];
                if (best == null || owing > bestWork || (owing == bestWork && seen < bestUse))
                {
                    best = page;
                    bestWork = owing;
                    bestUse = seen;
                }
            }
            return best;
        }
    }
}
