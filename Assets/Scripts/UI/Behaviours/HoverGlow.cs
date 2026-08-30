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
    /// plate to warm: the plate is transparent, and lifting a drawing off the counter it is
    /// standing on is worse than leaving it alone.
    ///
    /// So the affordance is the prop's own light. The tap font already did this by hand
    /// (DiegeticStage.BuildTapDoor, 2026-08-15: "THE AFFORDANCE IS THE PROP, not the plate")
    /// and its number — 1.22 — is this component's default, so the room goes on speaking one
    /// language rather than two. What is new is that it now reaches SpriteRenderers as well as
    /// Graphics, which is what lets a world-space bottle in the cellar answer a UI hit plate
    /// hung over it, and that it eases rather than snaps.
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

        /// <summary>Optional: a transform that rises this far while hovered. Left at 0 for
        /// anything standing on a surface — a bottle that lifts off the shelf is a bug.</summary>
        public float Lift;

        /// <summary>What <see cref="Lift"/> moves; ignored when Lift is 0.</summary>
        public Transform Riser;

        private bool _over;
        private float _g;                 // 0 cold, 1 fully lit
        private Color[] _restGraphics, _restSprites;
        private Vector3 _home;
        private bool _held;               // rest colours are in hand

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
            if (Riser != null) _home = Riser.localPosition;
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
            if (Riser != null && Lift > 0f)
                Riser.localPosition = _home + new Vector3(0f, Lift * g, 0f);
        }

        private void LateUpdate()
        {
            float want = _over ? 1f : 0f;
            if (Mathf.Approximately(_g, want))
            {
                // Settled cold: hand the prop back to whatever else paints it, so a bottle
                // that is re-tinted by the room is not argued with every frame.
                if (_g <= 0f && _held) Restore();
                return;
            }
            _g = Mathf.MoveTowards(_g, want, Time.unscaledDeltaTime * Speed);
            Apply(_g * _g * (3f - 2f * _g));        // smoothstep
        }
    }
}
