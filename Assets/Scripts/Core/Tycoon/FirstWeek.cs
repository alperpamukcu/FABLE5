using System;
using System.Collections.Generic;
using System.Linq;

namespace LastCall.Core
{
    /// <summary>
    /// THE SEVEN NIGHTS THAT ARE WRITTEN (2026-09-23, the author's economy brief: *"Her yıldız
    /// seviyesinde günler ve ilk 1 hafta için kesinlikle özel sipariş listesi olmalı"*).
    ///
    /// <see cref="DayPlan"/> cuts every other night from weights, and it is right to: a menu of
    /// forty pages has a shape worth computing. The opening week has no such shape. A new bar owns
    /// SIX pages — a pint, a neat pour and four two-part builds — so a plan cut from weights is
    /// six drinks in a bag whatever the arithmetic says, and the first thing the player meets is
    /// noise. Which is exactly the night that decides whether there is a second one.
    ///
    /// So the week is authored, and it is authored as a LESSON rather than as a balance curve.
    /// Each night adds one thing and nothing else changes:
    ///
    /// <list type="table">
    /// <item><term>1</term><description>the builds only — pour, serve, take the money</description></item>
    /// <item><term>2</term><description>and the keg: a pint is poured by the angle of the glass</description></item>
    /// <item><term>3</term><description>and the neat pour: one bottle, no mixer, nowhere to hide</description></item>
    /// <item><term>4</term><description>and the shaker: the first page with a method on it</description></item>
    /// <item><term>5</term><description>the whole opening menu, still mostly builds</description></item>
    /// <item><term>6</term><description>busier, and the shaken page comes round twice</description></item>
    /// <item><term>7</term><description>the week's own shift — the first night that looks like the game</description></item>
    /// </list>
    ///
    /// The counts are the other half. A computed opening night is seven covers of a three-dollar
    /// drink, which is twenty-seven dollars against a fourteen-dollar rent — a week of nothing
    /// happening. The written week runs busier than the door's own arithmetic would, so the first
    /// seven nights pay for the room AND the first few fittings, which is what *"oyuncu oyundan
    /// bezdirilmesin"* costs in money.
    ///
    /// It stands down the moment it stops being true: a bar that has climbed off the ground floor
    /// inside its first week (it happens — the standing can move a sixth of a star a night) is cut
    /// by the weights like any other, because the lesson is over.
    /// </summary>
    public static class FirstWeek
    {
        /// <summary>How many nights are written. Day 8 is the first the weights cut.</summary>
        public const int Nights = 7;

        /// <summary>
        /// What each night asks for, as a list of page ids with one entry per COVER. Read down a
        /// column and you can see the lesson: night one is four ids, night seven is the book.
        ///
        /// A page the bar does not own tonight is dropped and its cover given to the night's first
        /// page that IS owned, so the week survives a player who sold something, a bench fixture
        /// with three recipes, and the market's own gates.
        /// </summary>
        private static readonly string[][] Nightly =
        {
            // 1 — the builds. Three pages, nine covers: enough of each to stop guessing.   $36
            new[]
            {
                "vodka_soda", "gin_tonic", "whiskey_cola",
                "vodka_soda", "gin_tonic", "whiskey_cola",
                "vodka_soda", "gin_tonic", "whiskey_cola",
            },
            // 2 — and the keg: a pint is poured by the angle of the glass.                 $39
            new[]
            {
                "gin_tonic", "draught", "whiskey_cola", "vodka_soda", "gin_tonic",
                "draught", "whiskey_cola", "gin_tonic", "whiskey_cola", "vodka_soda",
            },
            // 3 — and the neat pour: one bottle, no mixer, nowhere to hide.                $40
            new[]
            {
                "whiskey_cola", "neat_pour", "gin_tonic", "draught", "whiskey_cola",
                "neat_pour", "gin_tonic", "draught", "whiskey_cola", "vodka_soda",
            },
            // 4 — and the shaker: the first page with a method on it.                      $51
            new[]
            {
                "gin_tonic", "gin_sour", "whiskey_cola", "draught", "neat_pour",
                "gin_sour", "gin_tonic", "whiskey_cola", "draught", "neat_pour",
                "vodka_soda",
            },
            // 5 — the whole opening menu, still mostly builds.                             $52
            new[]
            {
                "whiskey_cola", "gin_tonic", "gin_sour", "draught", "whiskey_cola",
                "neat_pour", "gin_sour", "gin_tonic", "draught", "whiskey_cola",
                "vodka_soda",
            },
            // 6 — busier, and the shaken page comes round a third time.                    $61
            new[]
            {
                "gin_sour", "whiskey_cola", "gin_tonic", "draught", "neat_pour",
                "gin_sour", "whiskey_cola", "gin_tonic", "draught", "neat_pour",
                "gin_sour", "whiskey_cola",
            },
            // 7 — the first night that looks like the game.                                $64
            new[]
            {
                "gin_sour", "gin_tonic", "draught", "gin_sour", "whiskey_cola",
                "neat_pour", "gin_sour", "gin_tonic", "draught", "neat_pour",
                "gin_sour", "whiskey_cola",
            },
        };

        /// <summary>The ids the week names, for the test that holds every one of them to a page in
        /// the book that the bar opens its doors owning.</summary>
        public static IEnumerable<string> PagesNamed =>
            Nightly.SelectMany(n => n).Distinct(StringComparer.Ordinal);

        /// <summary>How many covers night <paramref name="day"/> was written for.</summary>
        public static int CoversOn(int day) =>
            day >= 1 && day <= Nights ? Nightly[day - 1].Length : 0;

        /// <summary>
        /// The night's covers, or <c>null</c> when this night is not one of the written ones —
        /// which is every night after the seventh, and any night the bar has already climbed off
        /// the ground floor, because the lesson is about a bar that owns six pages.
        /// </summary>
        public static RecipeDefinition[] For(int day, double stars,
            IReadOnlyList<RecipeDefinition> pool, TycoonConfig config)
        {
            if (day < 1 || day > Nights) return null;
            if (stars >= 1.0) return null;
            if (pool == null || pool.Count == 0) return null;

            var owned = new Dictionary<string, RecipeDefinition>(StringComparer.Ordinal);
            foreach (var r in pool) owned[r.Id] = r;

            var written = Nightly[day - 1];
            var fallback = written.Select(id => owned.TryGetValue(id, out var r) ? r : null)
                .FirstOrDefault(r => r != null) ?? pool[0];

            var covers = new RecipeDefinition[written.Length];
            for (int i = 0; i < written.Length; i++)
                covers[i] = owned.TryGetValue(written[i], out var page) ? page : fallback;
            return covers;
        }
    }
}
