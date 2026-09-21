using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// What a rung of the ladder opens (PLAN_rank_ladder §1.2, 2026-09-21). A feature is a VERB the bar did not
    /// have — the rules layer refuses it below its rung, the room only hides it. The market's own gates (olives and
    /// mint behind 2.0 in base_bar.json) are data, not a feature: <see cref="Jars"/> exists so the ceremony can
    /// name them and a test can hold the data to the rung.
    /// </summary>
    public enum Feature
    {
        /// <summary>The ice bucket and the lemon on the counter; orders may ask for them.</summary>
        IceAndLemon,
        /// <summary>The papers behind the card, forgeries, the KICK key, the fine and the thanks (GDD 28).</summary>
        Door,
        /// <summary>The salt and the sugar on the counter; orders may ask for a rim.</summary>
        Rims,
        /// <summary>The bar spoon: stirred drinks (GDD 21 §14).</summary>
        Spoon,
        /// <summary>Olives and mint on the market's board.</summary>
        Jars,
        /// <summary>A second draught line — the second keg in the market (GDD 21 §10; 2026-09-21, the author:
        /// "Seviye atlatmak bira slotunu arttıracak"). The tower on the counter is the same drawing; the count grows.</summary>
        SecondLine,
        /// <summary>A third draught line — the third keg.</summary>
        ThirdLine,
    }

    /// <summary>One rung: where it stands on the star scale, what it is called, what it opens.</summary>
    public sealed class Rung
    {
        public int Index { get; }
        public double Stars { get; }
        /// <summary>The rung's name as a string-table line ("rank.title.r3").</summary>
        public Line Title { get; }
        public IReadOnlyList<Feature> Opens { get; }

        internal Rung(int index, double stars, params Feature[] opens)
        {
            Index = index;
            Stars = stars;
            Title = Line.Of("rank.title.r" + index);
            Opens = opens;
        }

        public bool Grants(Feature feature) => Array.IndexOf((Feature[])Opens, feature) >= 0;

        public override string ToString() => $"rung {Index} ({Stars:0.0}★)";
    }

    /// <summary>
    /// THE LADDER (2026-09-21, the author: "Her yıldız seviyesi geçildiğinde ... barın popülerliği değişecek ...
    /// ve her popülerlikte yeni oynanış özellikleri açılmalı"). Six rungs on the standing's own scale, at the
    /// author's thresholds — a half star, then one, two, three, four, five — each with a title the top bar wears
    /// and a short list of what it opened.
    ///
    /// The rank is read off the standing's HIGH-WATER MARK (<see cref="BarRating.BestStanding"/>), not the
    /// standing itself: a bad week drags the standing down, and a bar that was once the talk of the town does not
    /// hand its bar spoon back. The unlocks are therefore monotonic, which is what lets every gate in the run be a
    /// plain question — <see cref="Has"/> — asked at the moment the verb is attempted, with no bookkeeping of what
    /// was once open.
    ///
    /// Pure and static: the table is the rule. Anything that wants to show the ladder (the ceremony, the star row's
    /// key, the market's "NEW") reads this and nothing else, so no screen can carry a second copy of a threshold.
    /// </summary>
    public static class BarRank
    {
        /// <summary>The epsilon every star gate in the game uses: a bar sitting exactly on a rung has reached it.</summary>
        public const double Epsilon = 1e-9;

        public static readonly IReadOnlyList<Rung> Rungs = new[]
        {
            new Rung(0, 0.0),
            new Rung(1, 0.5, Feature.IceAndLemon),
            new Rung(2, 1.0, Feature.Door, Feature.Rims),
            new Rung(3, 2.0, Feature.Spoon, Feature.Jars, Feature.SecondLine),
            new Rung(4, 3.0, Feature.ThirdLine),
            new Rung(5, 4.0),
            new Rung(6, 5.0),
        };

        /// <summary>The foot of the ladder: a bar nobody has heard of.</summary>
        public static Rung Bottom => Rungs[0];

        /// <summary>The highest rung <paramref name="stars"/> has reached.</summary>
        public static Rung Of(double stars)
        {
            var at = Rungs[0];
            foreach (var r in Rungs)
                if (stars + Epsilon >= r.Stars) at = r;
            return at;
        }

        /// <summary>The rung above <paramref name="rung"/>, or null at the top.</summary>
        public static Rung Above(Rung rung) =>
            rung != null && rung.Index + 1 < Rungs.Count ? Rungs[rung.Index + 1] : null;

        /// <summary>The rung that opens <paramref name="feature"/>. Every feature is on exactly one rung.</summary>
        public static Rung Granting(Feature feature)
        {
            foreach (var r in Rungs)
                if (r.Grants(feature)) return r;
            throw new ArgumentOutOfRangeException(nameof(feature), $"{feature} is on no rung of the ladder.");
        }

        /// <summary>Has a standing of <paramref name="stars"/> opened <paramref name="feature"/>?</summary>
        public static bool Has(double stars, Feature feature) => Of(stars).Index >= Granting(feature).Index;

        /// <summary>
        /// The preparations a bar at <paramref name="stars"/> can put on a glass — the six the counter rail
        /// carries, in the order it carries them, minus the ones the ladder still holds. What orders may ask for
        /// (ServingSpec.Roll) and what <c>AddPreparationAtGlass</c> accepts are both this list, so a customer can
        /// never ask for a twist the bar cannot give.
        /// </summary>
        public static IReadOnlyList<PreparationDefinition> PreparationsOpen(double stars)
        {
            var open = new List<PreparationDefinition>(4);
            if (Has(stars, Feature.IceAndLemon)) { open.Add(Preparations.Ice); open.Add(Preparations.LemonTwist); }
            if (Has(stars, Feature.Rims)) { open.Add(Preparations.SaltRim); open.Add(Preparations.SugarRim); }
            // The jars (2026-09-21): olives and mint are extras on the rail from the third rung — and only when
            // the market's jar is on the shelf, which is the run's question (TycoonRun.PreparationsOpen), not the table's.
            if (Has(stars, Feature.Jars)) { open.Add(Preparations.Olive); open.Add(Preparations.Mint); }
            return open;
        }

        /// <summary>
        /// How many draught lines the ladder has opened at <paramref name="stars"/>: one at the foot, a second at
        /// two stars, a third at three (2026-09-21, the author: "ilk başta 1 bira alınırken ilerleyen yıldızlarda
        /// 2 ve 3. bira alınabilecek"). The kegs' locks (UnlockCondition.Tap) read the run's TapLevel, which is
        /// this whenever a tower stands on the counter at all.
        /// </summary>
        public static int DraughtLines(double stars) =>
            1 + (Has(stars, Feature.SecondLine) ? 1 : 0) + (Has(stars, Feature.ThirdLine) ? 1 : 0);

        /// <summary>Which feature, if any, holds <paramref name="preparation"/> back; null for a preparation
        /// the ladder never gated (a shake, a stir — those have their own laws).</summary>
        public static Feature? Gating(PreparationDefinition preparation)
        {
            if (preparation == null) return null;
            switch (preparation.Id)
            {
                case "ice":
                case "lemon_twist": return Feature.IceAndLemon;
                case "salt_rim":
                case "sugar_rim": return Feature.Rims;
                case "olive":
                case "mint": return Feature.Jars;
                default: return null;
            }
        }
    }
}
