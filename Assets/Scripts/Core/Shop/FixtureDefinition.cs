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

        /// <summary>This piece is a BEER FONT: clicking it in the room opens the draught
        /// station. Data rather than a hardcoded id, so a fourth font needs no code — and a
        /// bool rather than a general "opens" string, because there is exactly one station a
        /// prop is a door to, and inventing a second would be inventing a feature.</summary>
        public bool IsTap { get; }

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
            bool startsInTheRoom = false, bool isTap = false)
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
            IsTap = isTap;
        }

        public override string ToString() => $"{Name} ({Id}, ${Price}, slot {Slot})";
    }
}
