using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// A CAST SHADOW ON THE WALL BEHIND A THING (2026-09-17, the author: "nesnelerin
    /// gölgeleri olmalı ... müşterilerin arkaplandan ayrılması, sıyrılması").
    ///
    /// A second renderer wearing the same drawing as its source, black, part transparent,
    /// stood a few units off it in the direction the room's light is coming from and one
    /// sorting step behind it — the trick the cellar's bottles already sell with
    /// (DiegeticStage.PlaceCellarShadow), applied to whatever stands or sits in the room.
    /// It is what lifts a figure off a wall of the same brightness: the dark edge on one
    /// side says there is air between them.
    ///
    /// The DIRECTION is not this component's to choose. The stage sets <see cref="Offset"/>
    /// and <see cref="Alpha"/> once a frame from the hour — off the sun while the sun is in
    /// the window (long, to the right, lower as it sets), off the lamps once the house has
    /// switched itself on (short, close under) — so every shadow in the room swings on the
    /// same light and none of them is a painted blob that ignores the evening.
    ///
    /// It follows in LateUpdate so it sees the frame the source's own driver just wrote:
    /// the drinkers change sprite every few frames and the fixtures are re-hung on a resize.
    /// </summary>
    public sealed class CastShadow : MonoBehaviour
    {
        /// <summary>Where every shadow falls, in the stage's own units (art px at 640×360).</summary>
        public static Vector2 Offset = new Vector2(4f, -3f);
        /// <summary>How dark, 0..1, before each caster's own <see cref="Strength"/>.</summary>
        public static float Alpha = 0.26f;
        /// <summary>How much of the room's shadow the HOUSE LAMPS own right now, 0..1 (the rest is the sun and the
        /// sky). The stage writes it from the hour (2026-09-22, the author: "ışıkların gölgesi güneş varken güneşe
        /// göre, lambalar açıldığında lambalara göre olmalı, hepsinde"): at this share every caster swings its
        /// shadow off ITS OWN lamp instead of the window, which is what a room full of pendants actually does.</summary>
        public static float LampShare;
        /// <summary>How far the lamp-lit shadow is thrown, in the stage's own units.</summary>
        public static float LampReach = 5f;
        /// <summary>WHERE THE HOUSE'S LAMPS HANG, in world units - the stage writes the list when it dresses the
        /// room. Every caster takes the nearest of them and throws away from it, so the pendants over the counter
        /// push the shadows of what stands under them outwards and the sconces push theirs along the wall.</summary>
        public static Vector3[] Lamps = System.Array.Empty<Vector3>();
        /// <summary>Which way THIS caster's shadow falls while the lamps own the room: away from the nearest lamp,
        /// found each frame off <see cref="Lamps"/>. Zero - no lamp in the room - falls back to the window's.</summary>
        private Vector2 LampAway()
        {
            var lamps = Lamps;
            if (lamps == null || lamps.Length == 0) return Vector2.zero;
            var me = Source.transform.position;
            int best = 0; float bestD = float.MaxValue;
            for (int i = 0; i < lamps.Length; i++)
            {
                float d = (lamps[i] - me).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            var away = (Vector2)(me - lamps[best]);
            // Straight under a lamp there is no direction to fall in, and a shadow that flips as the caster crosses
            // the axis reads as a glitch: under it the throw goes DOWN, which is what a lamp overhead makes.
            if (away.sqrMagnitude < 4f) return new Vector2(away.x * 0.5f, -1f);
            return away;
        }
        /// <summary>This caster's share of the room's throw: 1 for a figure on the floor, less for a thing SCREWED
        /// TO THE WALL (2026-09-22, the author: "duvara sabit eşyaların gölgeleri kendilerine daha yakın olmalı,
        /// tablo televizyon ışık vs") - a picture hangs a finger off the plaster and its shadow says so.</summary>
        public float Reach = 1f;

        public SpriteRenderer Source;
        /// <summary>This caster's share of the room's shadow: 1 for a figure or a piece on
        /// the floor, less for something already half in its own light.</summary>
        public float Strength = 1f;
        /// <summary>How many sorting steps behind the source it draws.</summary>
        public int OrderDrop = 1;
        /// <summary>A hair toward the camera: two sprites on one order are sorted by depth,
        /// and a shadow that landed on a mat's own order has to win that tie to be seen.</summary>
        public float ZNudge = -0.0001f;

        private SpriteRenderer _sr;

        /// <summary>Hangs a shadow off <paramref name="source"/>, beside it under the same parent.</summary>
        public static CastShadow Attach(SpriteRenderer source, float strength = 1f, int orderDrop = 1)
        {
            var go = new GameObject(source.name + "_Cast");
            go.transform.SetParent(source.transform.parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = source.sharedMaterial;
            sr.enabled = false;
            var cast = go.AddComponent<CastShadow>();
            cast.Source = source;
            cast.Strength = strength;
            cast.OrderDrop = orderDrop;
            cast._sr = sr;
            return cast;
        }

        private void Awake()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            if (Source == null) { Destroy(gameObject); return; }
            bool on = Source.enabled && Source.gameObject.activeInHierarchy
                      && Source.sprite != null && Source.color.a > 0.01f && Alpha * Strength > 0.005f;
            if (_sr.enabled != on) _sr.enabled = on;
            if (!on) return;

            if (_sr.sprite != Source.sprite) _sr.sprite = Source.sprite;
            _sr.flipX = Source.flipX;
            _sr.flipY = Source.flipY;
            if (_sr.drawMode != Source.drawMode) _sr.drawMode = Source.drawMode;
            if (Source.drawMode != SpriteDrawMode.Simple) _sr.size = Source.size;
            if (_sr.sortingLayerID != Source.sortingLayerID) _sr.sortingLayerID = Source.sortingLayerID;
            int order = Source.sortingOrder - OrderDrop;
            if (_sr.sortingOrder != order) _sr.sortingOrder = order;

            var t = Source.transform;
            if (transform.parent != t.parent) transform.SetParent(t.parent, false);
            transform.localScale = t.localScale;
            transform.localRotation = t.localRotation;
            var throwBy = Offset;
            if (LampShare > 0.001f)
            {
                var away = LampAway();
                if (away.sqrMagnitude > 0.0001f)
                    throwBy = Vector2.Lerp(Offset, away.normalized * LampReach, LampShare);
            }
            throwBy *= Reach;
            transform.localPosition = t.localPosition + new Vector3(throwBy.x, throwBy.y, ZNudge);
            _sr.color = new Color(0f, 0f, 0f, Alpha * Strength * Source.color.a);
        }
    }
}
