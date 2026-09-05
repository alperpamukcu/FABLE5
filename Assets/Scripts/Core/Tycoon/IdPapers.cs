using System;

namespace LastCall.Core
{
    /// <summary>How a card can lie (GDD 28 §2.1).</summary>
    public enum Forgery
    {
        /// <summary>The card is theirs and says what it says.</summary>
        None,
        /// <summary>Somebody else's licence: another face's photo, name, age and country.
        /// The tell is the photo — the person on the stool is not the person on the card.</summary>
        Borrowed,
        /// <summary>Their own card with the age bumped; the print gives it away (the flag
        /// does not match the country). Specified now, built second.</summary>
        Altered,
    }

    /// <summary>
    /// THE TRUTH BEHIND THE CARD (GDD 28, 2026-09-04, the author: "20 yaş altı kişiler alkol
    /// alamayacak … sahte kimlik de işin içerisine eklenecek … doğru şekilde kovması ise gün
    /// sonunda küçük bonus paralar verecek").
    ///
    /// Every person the bar meets carries one of these from their first arrival
    /// (<see cref="RegularState"/>, Core-only) — an honest adult's says exactly what the
    /// registry rolled and nothing else. It is HIDDEN INFORMATION exactly like the order: the
    /// visit refuses to show it until the card has been read, and the fine and the bonus are
    /// decided on the truth in here, never on what the screen printed.
    ///
    /// ONE FACT IS NOT HIDDEN: <see cref="LooksYoung"/>. You can see the person. Every minor
    /// looks young, and so does a share of honest adults, so a young face is a reason to read
    /// the card carefully and never the verdict — the trap CLAUDE.md names (a tell outside the
    /// card makes the card decorative) is closed by giving adults the same face.
    ///
    /// Rolled on its own named stream (<c>"papers"</c>) so no existing seed moves.
    /// </summary>
    public sealed class IdPapers
    {
        /// <summary>Nineteen is a minor; twenty is served (the author's number, not the
        /// registry's 21 — both stay legal).</summary>
        public const int DrinkingAge = 20;

        /// <summary>Of the minors who come in, how many carry somebody else's card.</summary>
        public const double ForgedShare = 0.5;

        /// <summary>Of the honest adults, how many could pass for nineteen on the stool. Not a
        /// balance number so much as the rule that keeps the face from being the tell.</summary>
        public const double YoungAdultShare = 0.25;

        /// <summary>The fine for a served minor: this, plus this again per whole star of the
        /// standing — twenty dollars on a no-name bar, a hundred at four stars. The standing
        /// is the game's own measure of how far the bar has come ("gelişmişlik"), so the
        /// first week's mistakes are cheap and a made bar's are not.</summary>
        public const int FineBase = 20, FinePerStar = 20;

        /// <summary>The state's thanks for a right kick: a well drink's price on the slip
        /// (the author: "bir içki ücreti, örneğin 10 dolar" — the starter menu prices at
        /// $4–5, and the 200-run floor bot nets about $8 a served customer, so ten was more
        /// than a drink; five is one). A person shown the door does not come back, so it is
        /// paid once per face.</summary>
        public const int KickBonus = 5;

        /// <summary>How old they actually are.</summary>
        public int TrueAge { get; }

        /// <summary>What the card says. An honest card says the truth; a borrowed card says
        /// the lender's age, which is always of age.</summary>
        public int PrintedAge { get; }

        public Forgery Forgery { get; }

        /// <summary>Whether they could pass for nineteen — the one fact about the papers that
        /// the room may read before the card is, because it is written on the face.</summary>
        public bool LooksYoung { get; }

        public bool IsMinor => TrueAge < DrinkingAge;
        public bool IsForged => Forgery != Forgery.None;

        /// <summary>The crowd as it always was: of age, on their own card.</summary>
        public bool IsHonestAdult => !IsMinor && !IsForged;

        /// <summary>The door's whole question: should this person be shown it?</summary>
        public bool ShouldBeKicked => IsMinor || IsForged;

        public IdPapers(int trueAge, int printedAge, Forgery forgery, bool looksYoung = false)
        {
            if (trueAge <= 0) throw new ArgumentOutOfRangeException(nameof(trueAge));
            if (printedAge <= 0) throw new ArgumentOutOfRangeException(nameof(printedAge));
            if (forgery == Forgery.None && printedAge != trueAge)
                throw new ArgumentException("An honest card prints the true age.", nameof(printedAge));
            if (forgery != Forgery.None && printedAge < DrinkingAge)
                throw new ArgumentException("Nobody forges a card that still says they are under age.", nameof(printedAge));
            if (trueAge < DrinkingAge && !looksYoung)
                throw new ArgumentException("A minor looks young; that is the one thing the room can see.", nameof(looksYoung));
            TrueAge = trueAge;
            PrintedAge = printedAge;
            Forgery = forgery;
            LooksYoung = looksYoung;
        }

        /// <summary>
        /// Who is under twenty among the NEW people who walk in, by day: nobody on opening
        /// night, five in a hundred on the second, twelve in a hundred from the ninth on.
        /// Returns run at 55% from the second night and a bounced minor never returns, so
        /// the share of SEATS that are minors is roughly half of this. A starting stake for
        /// the sim.
        /// </summary>
        public static double MinorChance(int day) =>
            day < 2 ? 0.0 : Math.Min(0.12, 0.03 + 0.01 * day);

        /// <summary>
        /// Rolls one new person's papers on the <c>"papers"</c> stream. Never null: an honest
        /// adult carries the age the registry gave them (<paramref name="registryAge"/>) and
        /// the answer to "could they pass for nineteen". The draws are deterministic per seed
        /// and touch no other stream.
        /// </summary>
        public static IdPapers Roll(SeededRng rng, int day, int registryAge)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (registryAge < DrinkingAge) throw new ArgumentOutOfRangeException(nameof(registryAge),
                "The registry rolls adults; minors are rolled here.");
            if (rng.NextDouble() >= MinorChance(day))
            {
                bool young = rng.NextDouble() < YoungAdultShare;
                return new IdPapers(registryAge, registryAge, Forgery.None, young);
            }
            int trueAge = rng.NextInt(DrinkingAge - 2, DrinkingAge);      // 18 or 19
            bool forged = rng.NextDouble() < ForgedShare;
            if (!forged) return new IdPapers(trueAge, trueAge, Forgery.None, looksYoung: true);
            int printed = rng.NextInt(DrinkingAge + 1, DrinkingAge + 8);   // 21..27, the lender
            return new IdPapers(trueAge, printed, Forgery.Borrowed, looksYoung: true);
        }

        /// <summary>The fine for serving a minor at this standing.</summary>
        public static int FineFor(double standing)
        {
            double clamped = Math.Max(0.0, Math.Min(BarRating.MaxStars, standing));
            return FineBase + FinePerStar * (int)Math.Floor(clamped);
        }

        public override string ToString() =>
            Forgery == Forgery.None
                ? (IsMinor ? $"under age ({TrueAge})" : $"of age ({TrueAge})")
                : $"{Forgery.ToString().ToLowerInvariant()} card (prints {PrintedAge}, is {TrueAge})";
    }
}
