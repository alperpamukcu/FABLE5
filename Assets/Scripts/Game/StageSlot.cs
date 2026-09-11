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

        /// <summary>Whatever stands here IS THE ROOM (2026-09-06, the author's wall ladder:
        /// "tüm arkaplanı değiştirerek oyunda duvar geliştirmesi olarak sunulacak"). The
        /// piece is not placed at the hook; its sprite REPLACES the back wall's plate, so a
        /// rung of this ladder is a whole 640x360 picture of the room. X and Y are moot.</summary>
        public bool Backdrop { get; }

        /// <summary>Whatever stands here is CARRIED (2026-09-06, the shaker): the room does
        /// not stand it at a hook — it is a tool, and the HUD draws it where the work is,
        /// only while there is work in it. The slot still says WHERE for anything that wants
        /// to know, and the market still sells the ladder; the stage simply skips it.</summary>
        public bool Carried { get; }

        /// <summary>A backdrop LAID OVER the room's plate rather than replacing it (2026-09-12,
        /// the author's right wall ladder: "4 tier sağ duvar getirdim"). The piece is a whole
        /// 640x360 layer, mostly transparent, drawn on the plate's own transform just above it —
        /// under the window's glass and everything that hangs or stands — so one surface of the
        /// room climbs its own ladder while the back wall climbs another.</summary>
        public bool Overlay { get; }

        /// <summary>Where a rung of this slot goes, in the market's words ("The right wall");
        /// null lets the market say what it always has (the counter, or the back wall).</summary>
        public string Place { get; }

        public StageSlot(string id, float x, float y, bool onCounter,
                         float pairSpreadPx = 0f, bool houseLight = false, bool hangs = false,
                         bool flat = false, bool backdrop = false, bool carried = false,
                         bool overlay = false, string place = null)
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
            Backdrop = backdrop;
            Carried = carried;
            if (overlay && !backdrop)
                throw new ArgumentException($"Slot '{id}' is an overlay but not a backdrop — an overlay is a layer OF the room's picture.");
            Overlay = overlay;
            Place = string.IsNullOrWhiteSpace(place) ? null : place.Trim();
        }

        public override string ToString() => $"{Id} ({X}, {Y}){(OnCounter ? " on the counter" : "")}";
    }
}
