using System;
using System.Collections.Generic;
using System.Text;

namespace LastCall.Core
{
    /// <summary>
    /// What a bar has to have DONE before a thing is for sale (GDD 26 §12.2 step 4,
    /// 2026-08-14, the author: "bazı alkoller bazı yükseltmeler sadece mağza yıldızını
    /// arttırarak değil aynı zamanda müşteriye doğru siparişi vererek kilit açılacak ...
    /// başarımlar kilit görselinin altında belirtilecek").
    ///
    /// This exists because the game was about to have FOUR kinds of lock in four places. It
    /// already had three: a recipe's star gate (a rank table inside TycoonRun), the bought-
    /// recipe set, and the stock quarantine that a bought recipe releases. Adding "you must
    /// have served this guest what they asked for" as a fourth private rule would have made
    /// the shop unable to answer one question — *why can I not buy this?* — in one voice.
    ///
    /// So a lock is one object that can do two things: say whether it is OPEN, and say what
    /// it is WAITING FOR, in a sentence a shop can print under it. The second is not a
    /// courtesy. A lock the player cannot read is indistinguishable from a thing the game
    /// forgot to give them, which is the same failure the withheld story night was designed
    /// around (§12.1).
    /// </summary>
    public abstract class UnlockCondition
    {
        /// <summary>Is it open, for this bar, right now?</summary>
        public abstract bool MetBy(IUnlockState state);

        /// <summary>What the shop prints under the lock. UPPERCASE, no full stop — it is a
        /// label on a shelf, not prose.</summary>
        public abstract string Sentence { get; }

        /// <summary>
        /// THE RUNG THIS WAITS ON, or NaN for a lock that is not about the standing at all
        /// (2026-08-14). The shop's aisle crate counts what is held back and promises the
        /// NEAREST star — it can only do that if a lock will say its number, and a lock that
        /// answers "no number" is honest rather than silent: a bottle earned from a person
        /// must not drag an aisle's hint down to a rung that opens nothing.
        /// </summary>
        public virtual double StarsWanted => double.NaN;

        /// <summary>Nothing to earn. The default for everything that is simply for sale.</summary>
        public static readonly UnlockCondition Open = new AlwaysOpen();

        public static UnlockCondition Stars(double stars) => stars <= 0 ? Open : new StarGate(stars);

        /// <summary>Served the way they asked, on the night they asked (GDD 26 §4). The
        /// author's third kind: a bottle you earn from a person, not from a number.</summary>
        public static UnlockCondition Kept(string beatId, string who = null) =>
            new BeatKept(beatId, who);

        /// <summary>Every one of them, and the sentence names every one — a lock that admits
        /// to half of what it wants is worse than a lock that says nothing.</summary>
        public static UnlockCondition All(params UnlockCondition[] parts)
        {
            var real = new List<UnlockCondition>();
            foreach (var p in parts)
                if (p != null && !(p is AlwaysOpen)) real.Add(p);
            return real.Count == 0 ? Open : real.Count == 1 ? real[0] : new Every(real);
        }

        private sealed class AlwaysOpen : UnlockCondition
        {
            public override bool MetBy(IUnlockState state) => true;
            public override string Sentence => "";
        }

        private sealed class StarGate : UnlockCondition
        {
            private readonly double _stars;
            public StarGate(double stars) { _stars = stars; }
            // The same epsilon the story's gate uses: a bar sitting exactly on the rung has
            // reached it, and floating point must not be the thing that says otherwise.
            public override bool MetBy(IUnlockState state) => state.Stars + 1e-9 >= _stars;
            public override string Sentence => $"NEEDS {_stars:0.0} STARS";
            public override double StarsWanted => _stars;
        }

        private sealed class BeatKept : UnlockCondition
        {
            private readonly string _beatId, _who;
            public BeatKept(string beatId, string who)
            {
                if (string.IsNullOrWhiteSpace(beatId))
                    throw new ArgumentException("A kept-beat lock needs a beat.", nameof(beatId));
                _beatId = beatId; _who = who;
            }
            public override bool MetBy(IUnlockState state) => state.BeatWasKept(_beatId);
            // Named after the PERSON when the caller knows one, because "SERVE ECE HER
            // MANHATTAN" is a thing a player can go and do and "REQUIRES BEAT ECE_2" is not.
            public override string Sentence => string.IsNullOrEmpty(_who)
                ? "EARNED AT THE LAST CALL"
                : $"SERVE {_who.ToUpperInvariant()} WHAT THEY ASK FOR";
        }

        private sealed class Every : UnlockCondition
        {
            private readonly List<UnlockCondition> _parts;
            public Every(List<UnlockCondition> parts) { _parts = parts; }
            public override bool MetBy(IUnlockState state)
            {
                foreach (var p in _parts) if (!p.MetBy(state)) return false;
                return true;
            }

            /// <summary>The FURTHEST rung any part names — an "everything" lock is not open
            /// until its slowest half is, and promising the nearer one would be a lie the
            /// shop tells with a straight face.</summary>
            public override double StarsWanted
            {
                get
                {
                    double most = double.NaN;
                    foreach (var p in _parts)
                    {
                        double s = p.StarsWanted;
                        if (double.IsNaN(s)) continue;
                        if (double.IsNaN(most) || s > most) most = s;
                    }
                    return most;
                }
            }
            public override string Sentence
            {
                get
                {
                    var sb = new StringBuilder();
                    foreach (var p in _parts)
                    {
                        if (sb.Length > 0) sb.Append("  ·  ");
                        sb.Append(p.Sentence);
                    }
                    return sb.ToString();
                }
            }
        }
    }

    /// <summary>
    /// The only things a lock is allowed to ask about a bar. Deliberately tiny: a condition
    /// that could reach into the whole run would grow into a second rules engine, and the
    /// point of this type is that every lock in the game is answerable from two facts.
    /// </summary>
    public interface IUnlockState
    {
        /// <summary>The bar's standing.</summary>
        double Stars { get; }

        /// <summary>Was this written night served the way it was asked for?</summary>
        bool BeatWasKept(string beatId);
    }
}
