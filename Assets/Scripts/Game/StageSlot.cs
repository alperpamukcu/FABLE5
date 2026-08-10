using System;

namespace LastCall.Game
{
    /// <summary>
    /// A named hook in the room where a bought fixture stands (2026-08-10).
    ///
    /// This lives in the GAME layer, not in Core, and that is the whole point: a slot is a
    /// POSITION IN A PICTURE, which is presentation and nothing else. Core knows a fixture
    /// names a slot and refuses two fixtures naming the same one; where that slot actually
    /// is on the wall is the stage's business, the way a recipe's GlassId is the rules'
    /// business and which glass sprite draws it is not.
    ///
    /// Coordinates are the room art's own pixels with a BOTTOM-LEFT origin — identical to
    /// stage units at the native 640x360 — and they mark the BOTTOM CENTRE of whatever
    /// stands there, because a thing stands on its feet.
    /// </summary>
    public sealed class StageSlot
    {
        public string Id { get; }
        public float X { get; }
        public float Y { get; }

        /// <summary>Draw over the counter rather than behind it: for anything standing ON
        /// the bar. The candle sorted behind the bar top and simply vanished until this
        /// existed (2026-08-10).</summary>
        public bool OnCounter { get; }

        public StageSlot(string id, float x, float y, bool onCounter)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Slot needs an id.", nameof(id));
            Id = id;
            X = x;
            Y = y;
            OnCounter = onCounter;
        }

        public override string ToString() => $"{Id} ({X}, {Y}){(OnCounter ? " on the counter" : "")}";
    }
}
