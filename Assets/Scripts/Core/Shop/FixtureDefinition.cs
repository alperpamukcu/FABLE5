using System;

namespace LastCall.Core
{
    /// <summary>
    /// One piece of buyable bar dressing (2026-08-10, the author: modular background
    /// objects — plants, lamps, wall pieces — sold as bar upgrades). Dressing is
    /// COSMETIC: it changes what the room looks like, never what the bar can do, so it
    /// is exempt from the one-fitting-a-night cap the way stock and recipes are.
    ///
    /// **THE TAPS ARE THE EXCEPTION** (2026-08-15, the author: "bira koyma sahnesi için
    /// oyuna yeni eklediğimiz bira musluğuna tıklanması gereksin"). A beer font is a
    /// piece of the room AND the only door onto the draught station — the kegs left the
    /// back-bar wall the day the fonts arrived. That makes one fixture load-bearing, so
    /// it is also the one the bar opens already owning: <see cref="StartsInTheRoom"/>
    /// stands the first font on the counter on night one, and the market shows it as
    /// OURS rather than selling a bar the ability to pour what it already sells.
    ///
    /// AND IT IS NOT COSMETIC ANY MORE (2026-08-19, the author: "3 seviye musluk olacak,
    /// marketten musluğu geliştirmeden bir üst seviye fıçı bira alınmamalı"). The three
    /// towers are one LADDER standing in one slot — <see cref="TapLevel"/> orders it —
    /// and every keg past the first is locked behind a line to pour it out of. So this
    /// one kind of dressing does change what the bar can do, which is why the ladder's
    /// rules live in Core with the rest of them and not in the shop's UI.
    ///
    /// Core carries the whole definition — including the sprite name and the light
    /// numbers — as plain data it never interprets, the way RecipeDefinition carries its
    /// GlassId: the rules only care about Id, Price and Stars; where the thing stands and
    /// what it looks like are the presentation layer's to read.
    /// </summary>
    public sealed class FixtureDefinition
    {
        public string Id { get; }
        public string Name { get; }

        /// <summary>The stage slot this piece stands in. One catalogue entry per slot —
        /// two fixtures fighting over one hook is a content bug, and the parser refuses it.</summary>
        public string Slot { get; }

        public int Price { get; }

        /// <summary>The bar standing required before the market will sell it (0 = always).</summary>
        public double Stars { get; }

        /// <summary>The market card's one line about it.</summary>
        public string Flavor { get; }

        /// <summary>The room opens with this piece already standing in it — never bought,
        /// and so never refundable, since a refund only walks back tonight's purchases.</summary>
        public bool StartsInTheRoom { get; }

        /// <summary>
        /// How many lines this draught tower runs, or 0 for a piece that is not one
        /// (2026-08-19, the author: "3 seviye musluk olacak"). It replaced a plain bool,
        /// and the level is doing three jobs the bool could not: it orders the ladder, so
        /// the market will not sell the triple to a bar that never bought the twin; it is
        /// what a keg's lock is measured against; and it is how the room picks which of
        /// several owned towers to actually stand on the counter.
        ///
        /// Data rather than a hardcoded id, so a fourth level needs no code.
        /// </summary>
        public int TapLevel { get; }

        /// <summary>
        /// This piece's rung on its slot's ladder, or 0 for a piece that is not on one.
        /// THE LADDER STOPPED BEING ABOUT BEER on 2026-08-24 (the author: wall lamps with
        /// "başlangıç, lvl1, lvl2"): several pieces may stand in one slot when every one
        /// of them carries a rung, the room stands only the tallest owned, the market sells
        /// one rung at a time, and a rung cannot be refunded from under the one above it.
        /// A tower's rung is its TapLevel — the tap ladder was the first of these and its
        /// field keeps its name because a draught line count is also what kegs are locked
        /// against; a piece that is not a tower carries its rung here instead.
        /// </summary>
        public int Level { get; }

        /// <summary>This piece is a BEER FONT: clicking it in the room opens the draught
        /// station. There is exactly one station a prop is a door to, and inventing a
        /// second would be inventing a feature.</summary>
        public bool IsTap => TapLevel > 0;

        /// <summary>
        /// This piece is the bar's DRAIN: a drink you decide against goes down it
        /// (2026-08-26, the author: "çöp kutusunu da kaldır, çöp kutusu yerine lavabo
        /// kullanılacak"). The pedal bin standing at the right-hand end of the counter was
        /// a second, invented object doing a job the room already had a fixture for — and
        /// one the market was already selling two marks of.
        ///
        /// It is the second prop that is a door, and it follows the font's rule exactly:
        /// the flag is DATA, the room hangs a hit plate on whatever carries it, and the
        /// bar opens owning one because a bar with nowhere to pour a mistake is a bar that
        /// cannot make one.
        /// </summary>
        public bool IsDrain { get; }

        /// <summary>
        /// Nothing is written off down this drain (the author, same round: "üst seviye
        /// lavabo alındığında dökülen içkilerden zarar elde edilmeyecek, başlangıç
        /// lavabosunda içkiyi çöpe attığında para yiyeceksin"). THIS IS WHAT THE UPGRADE
        /// BUYS — the brass basin is the first piece of dressing that changes what the bar
        /// can afford to do, so the waiver rides the fixture rather than a rung number in
        /// Core: a third basin, or a different slot entirely, needs data and no code.
        /// </summary>
        public bool DrainsFree { get; }

        /// <summary>
        /// This piece is a SCREEN: its sprite is a sheet of frames rather than one
        /// picture, and the room plays them (2026-09-04, the author: a television on the
        /// wall running adverts, each one ending with the set switching itself off and
        /// back on again).
        ///
        /// Carried, not read — exactly like <see cref="Sprite"/> and the light numbers.
        /// The rules do not care that a fixture animates: nothing about a night changes
        /// because a picture moved, so the whole of it is the presentation layer's, and
        /// Core's only job is to hand the flag across without interpreting it.
        /// </summary>
        public bool IsScreen { get; }

        /// <summary>Resources/Fixtures sprite name. Presentation data, carried not read.</summary>
        public string Sprite { get; }

        /// <summary>A lamp is a fixture whose light intensity is above zero. Colour is
        /// linear 0..1 per channel; radius is in stage units.</summary>
        public bool HasLight => LightIntensity > 0f;
        public float LightR { get; }
        public float LightG { get; }
        public float LightB { get; }
        public float LightIntensity { get; }
        public float LightRadius { get; }

        public FixtureDefinition(string id, string name, string slot, int price,
            double stars, string flavor, string sprite,
            float lightR = 0f, float lightG = 0f, float lightB = 0f,
            float lightIntensity = 0f, float lightRadius = 0f,
            bool startsInTheRoom = false, int tapLevel = 0, int level = 0,
            bool isDrain = false, bool drainsFree = false, bool isScreen = false)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Fixture needs an id.", nameof(id));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException($"Fixture '{id}' needs a name.", nameof(name));
            if (string.IsNullOrWhiteSpace(slot)) throw new ArgumentException($"Fixture '{id}' needs a slot.", nameof(slot));
            if (string.IsNullOrWhiteSpace(sprite)) throw new ArgumentException($"Fixture '{id}' needs a sprite.", nameof(sprite));
            if (price <= 0) throw new ArgumentOutOfRangeException(nameof(price), $"Fixture '{id}' must cost something.");
            if (stars < 0 || stars > 5) throw new ArgumentOutOfRangeException(nameof(stars), $"Fixture '{id}' gate must be 0..5 stars.");
            if (lightIntensity < 0) throw new ArgumentOutOfRangeException(nameof(lightIntensity), $"Fixture '{id}' has negative light.");
            if (lightIntensity > 0 && lightRadius <= 0)
                throw new ArgumentOutOfRangeException(nameof(lightRadius), $"Fixture '{id}' shines but has no radius.");
            if (tapLevel < 0)
                throw new ArgumentOutOfRangeException(nameof(tapLevel), $"Fixture '{id}' has {tapLevel} draught lines.");
            if (level < 0)
                throw new ArgumentOutOfRangeException(nameof(level), $"Fixture '{id}' stands on rung {level}.");
            if (level > 0 && tapLevel > 0)
                throw new ArgumentException($"Fixture '{id}' cannot climb two ladders at once — " +
                                            "a tower's rung IS its tap level.", nameof(level));
            // A waiver on a piece that is not a drain would be a rule nobody can reach:
            // the fee is charged at the drain, so only a drain can excuse it.
            if (drainsFree && !isDrain)
                throw new ArgumentException($"Fixture '{id}' drains free but is not a drain.",
                                            nameof(drainsFree));
            if (isDrain && tapLevel > 0)
                throw new ArgumentException($"Fixture '{id}' cannot be a font and a drain.",
                                            nameof(isDrain));
            Id = id;
            Name = name;
            Slot = slot;
            Price = price;
            Stars = stars;
            Flavor = flavor ?? string.Empty;
            Sprite = sprite;
            LightR = lightR; LightG = lightG; LightB = lightB;
            LightIntensity = lightIntensity;
            LightRadius = lightRadius;
            StartsInTheRoom = startsInTheRoom;
            TapLevel = tapLevel;
            Level = tapLevel > 0 ? tapLevel : level;
            IsDrain = isDrain;
            DrainsFree = drainsFree;
            IsScreen = isScreen;
        }

        public override string ToString() => $"{Name} ({Id}, ${Price}, slot {Slot})";
    }
}
