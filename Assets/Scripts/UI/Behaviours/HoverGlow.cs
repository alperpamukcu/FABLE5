using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// A THING IN THE ROOM ANSWERS THE POINTER BY LIGHTING UP (2026-08-25, the author:
    /// "etkileşime girilebilir her buton veya nesne mouse ile üstüne gelince hafif
    /// parlamalı").
    ///
    /// <see cref="PressSink"/> is the answer for a KEY: it lifts, blooms and warms, because a
    /// key is an object with a face and a throw. It is the wrong answer for a prop. Half the
    /// clickable things in this game are not keys at all — a bottle standing in the cellar, a
    /// stool with somebody on it, a bowl of nuts, the till, the beer font — and each of those
    /// is a DRAWING lying under an invisible hit plate. There is nothing there to lift and no
    /// plate to warm: the plate is transparent.
    ///
    /// So the affordance is the prop's own light. The tap font already did this by hand
    /// (DiegeticStage.BuildTapDoor, 2026-08-15: "THE AFFORDANCE IS THE PROP, not the plate")
    /// and its number — 1.22 — is this component's default, so the room goes on speaking one
    /// language rather than two. What is new is that it now reaches SpriteRenderers as well as
    /// Graphics, which is what lets a world-space bottle in the cellar answer a UI hit plate
    /// hung over it, and that it eases rather than snaps.
    ///
    /// THE ANSWER GREW ON 2026-09-06 (the author: "seçilebilir bir nesnenin üstüne mouse
    /// geldiyse, o nesne yükselir biraz boyutu büyük ve arkasından ışık çıkar aynı zamanda
    /// çok hafif sağa ve sola doğru hareket eder"). Four things at once, and all of them
    /// small: the prop RISES, GROWS a little, a soft light comes up BEHIND it, and it SWAYS
    /// side to side. The earlier note under <see cref="Rise"/> said a bottle that lifts off
    /// the shelf is a bug; the author has asked for the lift, so the number is the thing that
    /// keeps it honest — a few units, not a hop.
    ///
    /// HOW IT COMPOSES WITH WHATEVER ELSE MOVES THE PROP. Most of these props are placed
    /// every frame by the room (the rail re-slots its dishes, the book and the coaster ride
    /// the counter, the cellar animates its drawer). So the offset is not remembered as a
    /// home position — it is UNDONE and RE-APPLIED each frame, in LateUpdate, on top of
    /// wherever the owner just put the prop. Nothing here fights an owner, and nothing here
    /// leaves a prop displaced when it stops.
    ///
    /// THE REST COLOUR IS CAPTURED ON ENTER, never at construction — the same rule
    /// <see cref="HoverWarm"/> obeys and for the same reason. These props are lit: the room's
    /// 2D lights and the day's own dimming write their tint, so a colour remembered at build
    /// time would cool a bottle back to the brightness it had at eight in the evening. Exit
    /// and disable both put back exactly what was captured, so nothing this does can outlive
    /// the pointer leaving.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HoverGlow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>Canvas graphics that light up. Usually the prop's own Image.</summary>
        public Graphic[] Graphics;

        /// <summary>World sprites that light up — a prop that lives in the room rather than
        /// on the canvas, reached through the hit plate hung over it.</summary>
        public SpriteRenderer[] Sprites;

        /// <summary>How much brighter, as a multiplier on whatever the prop is wearing. The
        /// tap font's own number: enough to read as "you are pointing at this" on a lit
        /// counter, small enough that it never reads as a state change.</summary>
        public float Gain = 1.22f;

        /// <summary>Approach rate. Fast enough to feel like an answer, slow enough that
        /// running the pointer along a shelf of bottles does not strobe.</summary>
        public float Speed = 14f;

        /// <summary>How far the prop rises while hovered, in ITS OWN units — HUD units for a
        /// canvas prop, stage units for a world one, which is why the two get different
        /// numbers at the call sites. 0 leaves it standing still.</summary>
        public float Rise;

        /// <summary>Deprecated spelling of <see cref="Rise"/>, kept because the tap font and
        /// the cellar were built with it.</summary>
        public float Lift { get => Rise; set => Rise = value; }

        /// <summary>What rises, grows and sways. Defaults to this transform on the first
        /// frame — a prop is normally its own drawing — and is set explicitly where the
        /// drawing is a child of the hit plate.</summary>
        public Transform Riser;

        /// <summary>How much bigger while hovered. 1 leaves the size alone.</summary>
        public float Grow = 1.06f;

        /// <summary>How far it drifts side to side, in its own units, and how fast. Very
        /// slight on purpose: this is a breath, not a wobble.</summary>
        public float Sway = 1.5f;
        public float SwaySpeed = 2.2f;

        /// <summary>The soft light behind the prop, as a share of the prop's own size. 0
        /// draws none — for a prop that already sits in front of something bright.</summary>
        public float Halo = 1.7f;

        /// <summary>The colour of that light. Warm by default: it is a bar.</summary>
        public Color HaloTint = new Color(1f, 0.86f, 0.62f, 0.55f);

        private bool _over;
        private float _g;                 // 0 cold, 1 fully lit
        private Color[] _restGraphics, _restSprites;
        private bool _held;               // rest colours are in hand

        // What this component added to the prop last frame, so it can be taken off again
        // before the next one is worked out. Position in the riser's own local space.
        private Vector3 _addedOffset;
        private float _addedScale = 1f;
        private float _phase;

        private Graphic _halo;            // the canvas light, made on the first hover
        private SpriteRenderer _haloSprite;   // ...or the world one

        /// <summary>When the room last answered the cursor. Static on purpose: the brake
        /// belongs to the ROOM, not to each prop — one cooldown per object would let a
        /// sweep across twelve bottles fire twelve times, which is the rattle this
        /// exists to prevent.</summary>
        private static float _lastHoverSound;
        private const float HoverGap = 0.09f;

        public void OnPointerEnter(PointerEventData _)
        {
            Capture();
            if (Time.unscaledTime - _lastHoverSound >= HoverGap)
            {
                _lastHoverSound = Time.unscaledTime;
                Sfx.Play("hover", 0.18f);
            }
            // The sway starts from rest whichever way the pointer arrived, so a prop never
            // jumps to the middle of a swing.
            _phase = 0f;
            _over = true;
        }

        public void OnPointerExit(PointerEventData _)
        {
            _over = false;
            // The glow eases out from wherever it is; Restore() is for the cases where there
            // will be no more frames to ease in (disable, teardown).
        }

        private void OnDisable()
        {
            _over = false;
            _g = 0f;
            Restore();
        }

        private Transform Body => Riser != null ? Riser : transform;

        private void Capture()
        {
            if (_held) return;
            if (Graphics != null)
            {
                _restGraphics = new Color[Graphics.Length];
                for (int i = 0; i < Graphics.Length; i++)
                    if (Graphics[i] != null) _restGraphics[i] = Graphics[i].color;
            }
            if (Sprites != null)
            {
                _restSprites = new Color[Sprites.Length];
                for (int i = 0; i < Sprites.Length; i++)
                    if (Sprites[i] != null) _restSprites[i] = Sprites[i].color;
            }
            _held = true;
        }

        /// <summary>
        /// Push a new REST colour in from outside, without the glow and the light fighting
        /// over the same field.
        ///
        /// The props in this room are lit, and until now that only ever came from Unity —
        /// a SpriteRenderer tinted by a Light2D, which no script writes, so nothing could
        /// clash. A prop on a CANVAS has no light on it; the only way to put the evening on
        /// it is for something to write Image.color every frame. That something and this
        /// component would then take turns clobbering each other: the tint would erase the
        /// glow, and the rest colour captured on enter would be a frozen snapshot of the
        /// light at the moment the pointer arrived.
        ///
        /// So the light does not write the colour any more, it writes the REST colour. When
        /// the pointer is away that is the same thing. When it is not, the glow keeps
        /// driving the graphic and simply glows off a rest colour that is still moving with
        /// the room — which is exactly what a lit object under a pointer should do.
        /// </summary>
        public void Retint(Color c)
        {
            if (Graphics == null) return;
            for (int i = 0; i < Graphics.Length; i++)
            {
                if (Graphics[i] == null) continue;
                if (_held && _restGraphics != null && i < _restGraphics.Length)
                    _restGraphics[i] = new Color(c.r, c.g, c.b, _restGraphics[i].a);
                else
                    Graphics[i].color = new Color(c.r, c.g, c.b, Graphics[i].color.a);
            }
            // ALPHA IS LEFT ALONE here for the same reason Apply() leaves it alone.
            if (_held) Apply(_g);
        }

        private void Restore()
        {
            if (!_held) return;
            Apply(0f);
            _held = false;
        }

        /// <summary>Puts the lit colour on at <paramref name="g"/>, 0 rest to 1 full.</summary>
        private void Apply(float g)
        {
            float k = Mathf.Lerp(1f, Gain, g);
            if (Graphics != null && _restGraphics != null)
                for (int i = 0; i < Graphics.Length && i < _restGraphics.Length; i++)
                {
                    if (Graphics[i] == null) continue;
                    var c = _restGraphics[i];
                    // ALPHA IS NEVER TOUCHED, and it is read LIVE rather than remembered.
                    // A drinker's body carries the seat's fade in its alpha and something
                    // else writes it every frame (SyncPatronBody); an alpha captured on
                    // enter would be put back here a frame later, so hovering somebody
                    // walking in would freeze them half-faded for as long as the pointer
                    // stayed on them.
                    Graphics[i].color = new Color(c.r * k, c.g * k, c.b * k, Graphics[i].color.a);
                }
            if (Sprites != null && _restSprites != null)
                for (int i = 0; i < Sprites.Length && i < _restSprites.Length; i++)
                {
                    if (Sprites[i] == null) continue;
                    var c = _restSprites[i];
                    Sprites[i].color = new Color(c.r * k, c.g * k, c.b * k, Sprites[i].color.a);
                }
            Move(g);
            Shine(g);
        }

        /// <summary>
        /// The rise, the growth and the sway — taken off and put back on every frame, so the
        /// owner of this prop's position (the rail, the counter, the drawer) stays the owner.
        /// </summary>
        private void Move(float g)
        {
            var body = Body;
            if (body == null) return;
            bool moves = Rise > 0f || Sway > 0f;
            if (moves)
            {
                var basePos = body.localPosition - _addedOffset;
                if (g > 0f)
                {
                    _phase += Time.unscaledDeltaTime * SwaySpeed;
                    _addedOffset = new Vector3(Mathf.Sin(_phase * Mathf.PI * 2f) * Sway * g,
                                               Rise * g, 0f);
                }
                else _addedOffset = Vector3.zero;
                body.localPosition = basePos + _addedOffset;
            }
            if (Grow > 1f)
            {
                float want = Mathf.Lerp(1f, Grow, g);
                var s = body.localScale;
                float undo = _addedScale > 0.0001f ? 1f / _addedScale : 1f;
                body.localScale = new Vector3(s.x * undo * want, s.y * undo * want, s.z);
                _addedScale = want;
            }
        }

        /// <summary>
        /// The light behind it. Made on the first hover and then simply faded — a prop that
        /// is never pointed at never pays for one. On a canvas it is a SIBLING drawn just
        /// before the prop (a child would be in front of it); in the world it is a child
        /// renderer one sorting order behind.
        /// </summary>
        private void Shine(float g)
        {
            if (Halo <= 0f) return;
            if (_halo == null && _haloSprite == null) MakeHalo();
            if (_halo != null)
            {
                var c = HaloTint;
                _halo.color = new Color(c.r, c.g, c.b, c.a * g);
                var rt = (RectTransform)_halo.transform;
                var body = Body as RectTransform;
                if (body != null)
                {
                    rt.position = body.position;
                    rt.sizeDelta = body.rect.size * Halo;
                    rt.localScale = body.localScale;
                }
            }
            if (_haloSprite != null)
            {
                var c = HaloTint;
                _haloSprite.color = new Color(c.r, c.g, c.b, c.a * g);
            }
        }

        private void MakeHalo()
        {
            var body = Body;
            if (body == null) return;
            // A PROP THAT LIVES IN THE ROOM GETS A LIGHT IN THE ROOM. Its hit plate is on the
            // canvas, which draws over everything in the world — a bloom hung there would sit
            // in FRONT of the bottle it is meant to be behind. So a prop with world sprites
            // gets a world halo, three sorting orders under its own drawing, which puts it
            // behind the whole v4 sandwich rather than between its layers.
            var lit = Sprites != null && Sprites.Length > 0 ? Sprites[0] : null;
            if (lit != null)
            {
                var wgo0 = new GameObject("Halo");
                wgo0.transform.SetParent(lit.transform, false);
                var wsr0 = wgo0.AddComponent<SpriteRenderer>();
                wsr0.sprite = ChromeArt.Halo();
                wsr0.sortingLayerID = lit.sortingLayerID;
                wsr0.sortingOrder = lit.sortingOrder - 3;
                wsr0.color = new Color(HaloTint.r, HaloTint.g, HaloTint.b, 0f);
                var size0 = lit.sprite != null ? lit.sprite.bounds.size : Vector3.one;
                var halo0 = wsr0.sprite != null ? wsr0.sprite.bounds.size : Vector3.one;
                if (halo0.x > 0.0001f && halo0.y > 0.0001f)
                    wgo0.transform.localScale = new Vector3(size0.x * Halo / halo0.x,
                                                            size0.y * Halo / halo0.y, 1f);
                _haloSprite = wsr0;
                return;
            }
            var asRect = body as RectTransform;
            if (asRect != null)
            {
                var go = new GameObject("Halo", typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(asRect.parent, false);
                rt.SetSiblingIndex(asRect.GetSiblingIndex());   // just BEHIND the prop
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                var img = go.AddComponent<Image>();
                img.sprite = ChromeArt.Halo();
                img.raycastTarget = false;
                img.color = new Color(HaloTint.r, HaloTint.g, HaloTint.b, 0f);
                _halo = img;
                return;
            }
            var sr = body.GetComponent<SpriteRenderer>();
            if (sr == null && Sprites != null && Sprites.Length > 0) sr = Sprites[0];
            if (sr == null) return;
            var wgo = new GameObject("Halo");
            wgo.transform.SetParent(sr.transform, false);
            var wsr = wgo.AddComponent<SpriteRenderer>();
            wsr.sprite = ChromeArt.Halo();
            wsr.sortingLayerID = sr.sortingLayerID;
            wsr.sortingOrder = sr.sortingOrder - 1;
            wsr.color = new Color(HaloTint.r, HaloTint.g, HaloTint.b, 0f);
            // Sized off the prop's own drawing, in the world's own units.
            var size = sr.sprite != null ? sr.sprite.bounds.size : Vector3.one;
            var haloSize = wsr.sprite != null ? wsr.sprite.bounds.size : Vector3.one;
            if (haloSize.x > 0.0001f && haloSize.y > 0.0001f)
                wgo.transform.localScale = new Vector3(size.x * Halo / haloSize.x,
                                                       size.y * Halo / haloSize.y, 1f);
            _haloSprite = wsr;
        }

        private void LateUpdate()
        {
            float want = _over ? 1f : 0f;
            if (Mathf.Approximately(_g, want) && want <= 0f)
            {
                // Settled cold: hand the prop back to whatever else paints it, so a bottle
                // that is re-tinted by the room is not argued with every frame.
                if (_held) Restore();
                return;
            }
            _g = Mathf.MoveTowards(_g, want, Time.unscaledDeltaTime * Speed);
            // Held at full: the sway is still moving, so this keeps applying rather than
            // settling — that is the difference between a breath and a nudge.
            Apply(_g * _g * (3f - 2f * _g));        // smoothstep
        }
    }
}
