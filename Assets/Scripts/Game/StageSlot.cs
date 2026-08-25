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

        /// <summary>A PAIR of mounting points, this many art px apart and symmetric about
        /// the slot (2026-08-24, the author: "simetrik bir şekilde 2 adet duvar lambaları").
        /// One fixture, two mountings: whatever stands here is drawn twice, spread this far,
        /// so a matched pair is one purchase and one catalogue entry. 0 = a single hook.</summary>
        public float PairSpreadPx { get; }

        /// <summary>The room's HOUSE LIGHTS hang here: whatever shines in this slot is run
        /// on the evening's clock — dim while the window owns the room, up as the sky dies,
        /// and taken down by the closing beat. A lantern bought as dressing keeps its own
        /// steady glow; the house lights belong to the hour.</summary>
        public bool HouseLight { get; }

        /// <summary>Whatever stands here HANGS — it touches no floor and no counter, so it
        /// casts no contact shadow and draws behind the floor dressing rather than among it
        /// (2026-08-24, the flamingo triptych: a picture on the wall is behind the table in
        /// front of the wall, and a foot-blob under a frame reads as a stain).</summary>
        public bool Hangs { get; }

        /// <summary>Whatever stands here LIES FLAT on its surface, and everything else on
        /// that surface stands on IT (2026-08-25, the rug and the drip mat). A mat is not a
        /// prop: it draws under the dressing that shares its surface rather than among it,
        /// where two pieces on one sorting order leave which one wins to chance — and it
        /// casts no contact shadow, because a blob under something already lying on the
        /// floor reads as a stain. Independent of <see cref="OnCounter"/>: the rug is flat
        /// on the boards, the drip mat is flat on the bar.</summary>
        public bool Flat { get; }

        public StageSlot(string id, float x, float y, bool onCounter,
                         float pairSpreadPx = 0f, bool houseLight = false, bool hangs = false,
                         bool flat = false)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Slot needs an id.", nameof(id));
            if (pairSpreadPx < 0) throw new ArgumentException($"Slot '{id}' has a negative pair spread.");
            Id = id;
            X = x;
            Y = y;
            OnCounter = onCounter;
            PairSpreadPx = pairSpreadPx;
            HouseLight = houseLight;
            Hangs = hangs;
            Flat = flat;
        }

        public override string ToString() => $"{Id} ({X}, {Y}){(OnCounter ? " on the counter" : "")}";
    }
}
