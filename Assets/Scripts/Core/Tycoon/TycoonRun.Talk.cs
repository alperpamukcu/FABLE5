namespace LastCall.Core
{
    // TycoonRun, part Talk: while the hostess is talking, the night stands still.
    //
    // The author, 2026-09-27: "Konuşmalar yaşanırken zaman ilerlememeli, yeni insanlar gelmemeli." It was the
    // HUD's job until now - the book and the menus scale the clock it hands to Tick - and a conversation had no
    // such hold at all: the night ran on under her lines, drinkers walked in, and the ones waiting lost patience
    // while the player read. The rules layer never trusts the UI, so the hold is a VERB here and Tick refuses to
    // move while it is on: no clock, no arrivals, no patience, no mess, no trial. The screen asks for it when a
    // conversation opens and lets go when the last line is heard; the sim bot never asks, so its nights are the
    // same nights they always were.
    public sealed partial class TycoonRun
    {
        /// <summary>Whether a conversation is holding the night. Nothing on the floor moves while it is.</summary>
        public bool Talking { get; private set; }

        /// <summary>A conversation has opened. Only an OPEN night can be held - a closed one has no clock to stop,
        /// and a hold asked for over the books would outlive the night it was asked on.</summary>
        public void BeginTalk()
        {
            if (Phase == TycoonPhase.DayOpen) Talking = true;
        }

        /// <summary>The last line was heard: the night picks up exactly where it stopped. Harmless when nothing is
        /// held, so a screen can call it on any way out of a conversation without checking first.</summary>
        public void EndTalk() => Talking = false;
    }
}
