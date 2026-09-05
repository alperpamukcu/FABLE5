using System;
using System.Collections.Generic;

namespace LastCall.Core
{
    /// <summary>
    /// What one served customer left on the counter when they got up (GDD 27 §4.1): their
    /// EMPTY GLASS, which takes the stool until it is collected, and a SMUDGE — a ring, a wet
    /// patch — which stays until it is wiped. This is the 2026-08-11 <c>DirtyGlass</c> grown a
    /// second half: the glass used to clear itself after seven seconds, which was a stand-in
    /// for a verb that did not exist. Nothing clears itself now.
    ///
    /// The HUD holds one of these per stool it drew the mess on, exactly as it held the
    /// glass; Core does not know which stool is which (the floor counts seats, the room
    /// places them), so a mess is an object, not a seat index.
    /// </summary>
    public sealed class CounterMess
    {
        /// <summary>Which glass line the drink was served in, so the empty on the counter is
        /// drawn as THE glass (the author, 2026-08-02). Null when the line is not known.</summary>
        public string GlasswareId { get; }

        /// <summary>The empty glass is still standing there. While it is, the stool is taken.</summary>
        public bool HasGlass { get; private set; }

        /// <summary>The counter under it still wants the cloth.</summary>
        public bool Smudged { get; private set; }

        /// <summary>Nothing left to do here; the floor drops it from the list.</summary>
        public bool IsClean => !HasGlass && !Smudged;

        /// <summary>Seconds since it was left. Dirt inside the grace drains nothing.</summary>
        public double Age { get; private set; }

        /// <summary>Whether this mess is costing the room right now: still dirty, and old
        /// enough that the bar has had its moment to reach for the cloth.</summary>
        public bool IsCounting => !IsClean && Age >= Housekeeping.DirtGrace;

        internal CounterMess(string glasswareId, bool glass, bool smudge)
        {
            GlasswareId = glasswareId;
            HasGlass = glass;
            Smudged = smudge;
        }

        internal void Tick(double seconds)
        {
            if (seconds > 0) Age += seconds;
        }

        internal void TakeGlass()
        {
            if (!HasGlass) throw new InvalidOperationException("There is no glass here to collect.");
            HasGlass = false;
        }

        internal void WipeAway()
        {
            if (HasGlass)
                throw new InvalidOperationException("Collect the glass first — you cannot wipe under it.");
            if (!Smudged) throw new InvalidOperationException("The counter is clean here.");
            Smudged = false;
        }
    }

    /// <summary>
    /// THE COUNTER'S OWN NIGHT (GDD 27 §4, 2026-09-04, the author: "her müşteriden sonra
    /// tezgahtaki bez ile tezgahı silmen gerekecek. Tezgahta müşterilerin bıraktığı bardakları
    /// toplaman gerekecek. Toplanılan bardakları lavaboya götürmelisin … bardaklar
    /// toplanmadıysa, tezgah silinmediyse bu konfor puanını düşürecek").
    ///
    /// One per <see cref="BarDay"/>. It keeps the messes the leavers left, the glasses in the
    /// bartender's hand, the sink's wash timer, and the one number the night's comfort is
    /// read from: how many SPOT-SECONDS the counter stood dirty past its grace. The four
    /// verbs — collect, wipe, wash, and the tick — are the whole mechanic; the presentation
    /// layer draws them and the sim bot calls them, and neither decides anything.
    /// </summary>
    public sealed class Housekeeping
    {
        /// <summary>Seconds a fresh mess may stand before it starts costing the room: time
        /// to notice, and to reach for the cloth.</summary>
        public const double DirtGrace = 6.0;

        /// <summary>A wash takes this long, plus a little per glass in it.</summary>
        public const double WashBaseSeconds = 1.5, WashPerGlassSeconds = 0.5;

        private readonly List<CounterMess> _messes = new List<CounterMess>();

        /// <summary>Everything still on the counter that wants a hand — a glass, a smudge, or both.</summary>
        public IReadOnlyList<CounterMess> Messes => _messes;

        /// <summary>Empty glasses collected and not yet washed.</summary>
        public int GlassesInHand { get; private set; }

        /// <summary>Glasses in the sink right now (zero unless <see cref="SinkBusy"/>).</summary>
        public int GlassesWashing { get; private set; }

        /// <summary>Seconds the sink still has to run. Zero when it is idle.</summary>
        public double WashLeft { get; private set; }

        /// <summary>The tap is running: a second wash waits for this one.</summary>
        public bool SinkBusy => WashLeft > 0;

        /// <summary>Seat-seconds the counter stood dirty past the grace, summed over the night.
        /// Divided by seats × night seconds, it is what the room lost (<see cref="Cleanliness"/>).</summary>
        public double DirtSpotSeconds { get; private set; }

        // The night's tallies, for the slip and the sim.
        public int MessesLeft { get; private set; }
        public int GlassesCollected { get; private set; }
        public int Wipes { get; private set; }
        public int GlassesWashed { get; private set; }

        /// <summary>Glasses standing on the counter — each one holds a stool.</summary>
        public int GlassesOnCounter
        {
            get
            {
                int n = 0;
                foreach (var m in _messes) if (m.HasGlass) n++;
                return n;
            }
        }

        /// <summary>Messes past their grace: what the live comfort reading counts.</summary>
        public int DirtySpots
        {
            get
            {
                int n = 0;
                foreach (var m in _messes) if (m.IsCounting) n++;
                return n;
            }
        }

        /// <summary>
        /// The share of the night the counter was clean, time-weighted per seat. Measured
        /// against the night the floor ACTUALLY ran (<paramref name="elapsedSeconds"/>, which
        /// runs past closing until the last stool empties), never the config's number, and
        /// clamped to [0, 1] so the most the mess can cost is <see cref="VenueComfort.DirtPenalty"/>.
        /// One spot dirty past its grace for the whole of a four-stool night reads 0.75; ten
        /// customers' marks each left twenty seconds past the grace read roughly 0.47. Read
        /// once at close by <see cref="VenueComfort.Tonight"/>.
        /// </summary>
        public double Cleanliness(int seats, double elapsedSeconds)
        {
            if (seats <= 0 || elapsedSeconds <= 0) return 1.0;
            double share = DirtSpotSeconds / (seats * elapsedSeconds);
            return Math.Max(0.0, Math.Min(1.0, 1.0 - share));
        }

        /// <summary>
        /// A served customer got up. Their glass and their mark land on the counter.
        /// Storm-offs, declined orders, the kicked and the guest of the house call nothing —
        /// nothing was poured, or nothing counts (GDD 27 §4.1).
        /// </summary>
        public CounterMess LeaveMess(string glasswareId, bool smudge = true)
        {
            var mess = new CounterMess(glasswareId, glass: true, smudge: smudge);
            _messes.Add(mess);
            MessesLeft++;
            return mess;
        }

        /// <summary>The glass leaves the counter for the hand; the stool is free this instant.</summary>
        public void CollectGlass(CounterMess mess)
        {
            Own(mess).TakeGlass();
            GlassesInHand++;
            GlassesCollected++;
            Drop(mess);
        }

        /// <summary>The cloth. Refuses under a glass — collect first, then wipe.</summary>
        public void Wipe(CounterMess mess)
        {
            Own(mess).WipeAway();
            Wipes++;
            Drop(mess);
        }

        /// <summary>
        /// Carry what is in the hand to the sink and run the tap. Returns how long the water
        /// runs; the hand is empty from this moment and the glasses are clean when it stops.
        /// </summary>
        public double WashGlasses()
        {
            if (GlassesInHand <= 0) throw new InvalidOperationException("There is nothing in your hands to wash.");
            if (SinkBusy) throw new InvalidOperationException("The sink is running — wait for it.");
            GlassesWashing = GlassesInHand;
            GlassesInHand = 0;
            WashLeft = WashSecondsFor(GlassesWashing);
            return WashLeft;
        }

        /// <summary>How long the tap runs for a stack of this size.</summary>
        public static double WashSecondsFor(int glasses) =>
            WashBaseSeconds + WashPerGlassSeconds * Math.Max(0, glasses);

        /// <summary>
        /// The floor's clock: every mess ages, every mess past its grace costs a seat-second
        /// per second, and the sink counts down. Called by <see cref="BarDay.Tick"/>.
        /// </summary>
        public void Tick(double seconds)
        {
            if (seconds <= 0) return;
            foreach (var mess in _messes)
            {
                // The grace is spent BEFORE the exposure starts, exactly: a mess that crosses
                // the line inside this tick pays only for the part past it.
                double before = mess.Age;
                mess.Tick(seconds);
                if (mess.IsClean) continue;
                double counted = Math.Max(0.0, mess.Age - Math.Max(before, DirtGrace));
                DirtSpotSeconds += counted;
            }
            if (WashLeft > 0)
            {
                WashLeft -= seconds;
                if (WashLeft <= 0)
                {
                    WashLeft = 0;
                    GlassesWashed += GlassesWashing;
                    GlassesWashing = 0;
                }
            }
        }

        /// <summary>
        /// Closing: whatever is still in the hand goes through the sink for free and whatever
        /// is still on the counter has already been paid for in exposure. The night files what
        /// it recorded; nothing carries over to tomorrow's counter.
        /// </summary>
        public void CloseNight()
        {
            if (SinkBusy)
            {
                GlassesWashed += GlassesWashing;
                GlassesWashing = 0;
                WashLeft = 0;
            }
            GlassesWashed += GlassesInHand;
            GlassesInHand = 0;
        }

        private CounterMess Own(CounterMess mess)
        {
            if (mess == null) throw new ArgumentNullException(nameof(mess));
            if (!_messes.Contains(mess))
                throw new InvalidOperationException("That is not on this counter.");
            return mess;
        }

        private void Drop(CounterMess mess)
        {
            if (mess.IsClean) _messes.Remove(mess);
        }
    }
}
