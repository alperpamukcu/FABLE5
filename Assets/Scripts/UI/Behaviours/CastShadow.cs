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
            transform.localPosition = t.localPosition + new Vector3(Offset.x, Offset.y, ZNudge);
            _sr.color = new Color(0f, 0f, 0f, Alpha * Strength * Source.color.a);
        }
    }
}
