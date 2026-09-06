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

        /// <summary>How far it ROCKS side to side, in degrees, and how fast (2026-09-06,
        /// the author: "sağ sola hareket etmesinden kastım sağ sola sallanması"). A drift
        /// along x reads as sliding; a bar prop under a hand tips. Very slight either way.</summary>
        public float Sway = 2.2f;
        public float SwaySpeed = 1.8f;

        /// <summary>
        /// How far the light reaches past the prop, as a multiple of the automatic reach
        /// (`ChromeArt.GlowSpread`, about a sixth of the drawing's short side). 0 draws none.
        ///
        /// The light itself is the PROP'S OWN SHAPE, swollen and soft, not a stock ellipse
        /// (2026-09-06, the author: "parlama alanı nesnenin şekline göre gerçek nesnenin
        /// şeklinden daha büyük olmalı ... şu an standart bir elips ve bu her nesneye
        /// uymuyor"). A dish glows like a dish and a spoon like a spoon.
        /// </summary>
        public float Halo = 1f;

        /// <summary>The colour of that light. Warm by default: it is a bar.</summary>
        public Color HaloTint = new Color(1f, 0.86f, 0.62f, 0.7f);

        /// <summary>Everything that moves with the prop when it is more than one object —
        /// the cellar's bottle is a front plate, a back plate, a drink and a mask standing in
        /// one place. Left null, the prop moves alone.</summary>
        public Transform[] Movers;

        /// <summary>Sorting orders to lift while hovered, so a world prop comes to the front
        /// of the room the way a canvas prop comes to the front of its parent.</summary>
        public int OrderLift = 20;

        /// <summary>
        /// Lit without a pointer (2026-09-06, the author: "bardak tutulduğunda lavaboya
        /// oyuncuyu yönlendirmeli ... bardak sürüklenirken lavabo ön plana çıkmalı"). The
        /// room calls a prop forward while the hand is carrying something that belongs to
        /// it; everything else — the rise, the light, the coming to the front — is what
        /// the pointer would have got, so the two never disagree about what "lit" means.
        /// </summary>
        public void Beckon(bool on) => _beckoned = on;

        private bool _over, _beckoned;
        private float _g;                 // 0 cold, 1 fully lit
        private Color[] _restGraphics, _restSprites;
        private bool _held;               // rest colours are in hand

        // What this component added to the prop last frame, so it can be taken off again
        // before the next one is worked out. Position in the riser's own local space.
        private Vector3[] _added;         // what this glow has added to each mover, per mover
        private Quaternion[] _addedRot;   // ...and the turn, so an owner's own tilt survives
        private float _addedScale = 1f;
        private float _phase;

        private Graphic _halo;            // the canvas light, made on the first hover
        private Sprite _haloFor;          // the drawing it was cut from
        private bool _fronted;            // brought to the front by the pointer
        private bool _stowing;            // being disabled: leave the draw order alone
        private int _restIndex = -1;      // where it stood before that
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
            _stowing = true;
            Restore();
            _stowing = false;
        }

        /// <summary>Puts back the draw order a hover borrowed, once the object is properly
        /// alive again — see the note in <see cref="Front"/>.</summary>
        private void OnEnable()
        {
            if (_restIndex < 0) return;
            var body = Body as RectTransform;
            if (body != null && body.parent != null)
                body.SetSiblingIndex(Mathf.Min(_restIndex, body.parent.childCount - 1));
            _restIndex = -1;
            _fronted = false;
        }

        private Transform Body => Riser != null ? Riser : transform;

        /// <summary>Everything this glow moves: the body, and the other objects the same
        /// prop is drawn out of.</summary>
        private void ForEachMover(System.Action<Transform> act)
        {
            var body = Body;
            if (body != null) act(body);
            if (Movers == null) return;
            foreach (var t in Movers)
                if (t != null && t != body) act(t);
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
            Front(false);
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
            float rise = Rise * g;
            float angle = 0f;
            if (Sway > 0f && g > 0f)
            {
                _phase += Time.unscaledDeltaTime * SwaySpeed;
                angle = Mathf.Sin(_phase * Mathf.PI * 2f) * Sway * g;
            }
            float want = Grow > 1f ? Mathf.Lerp(1f, Grow, g) : 1f;
            float undo = _addedScale > 0.0001f ? 1f / _addedScale : 1f;
            _addedScale = want;

            // ONE PROP, HOWEVER MANY OBJECTS IT IS DRAWN OUT OF. A bottle in the cellar is a
            // front plate, a back plate, a column of drink and the mask that cuts it —
            // four transforms standing in one place with four different pivots. So each is
            // turned and grown ABOUT THE BODY'S centre rather than about its own, which is
            // the difference between a bottle that rocks and a bottle whose drink swings
            // out through its glass. What was added is remembered PER MOVER and taken off
            // again next frame, so whoever owns these positions stays their owner.
            var body = Body;
            int n = 0;
            ForEachMover(t => n++);
            if (_added == null || _added.Length != n) _added = new Vector3[n];
            if (_addedRot == null || _addedRot.Length != n)
            {
                _addedRot = new Quaternion[n];
                for (int q = 0; q < n; q++) _addedRot[q] = Quaternion.identity;
            }
            var rot = Quaternion.Euler(0f, 0f, angle);
            Vector3 pivot = body != null ? body.localPosition - _added[0] : Vector3.zero;
            int k = 0;
            ForEachMover(t =>
            {
                var rest = t.localPosition - _added[k];
                var moved = pivot + rot * ((rest - pivot) * want) + new Vector3(0f, rise, 0f);
                t.localPosition = moved;
                _added[k] = moved - rest;
                // ROCKED, not slid (2026-09-06, the author: "sag sola hareket etmesinden
                // kastim sag sola sallanmasi"). A drift along x reads as sliding; a thing
                // picked up off a bar tips.
                //
                // ADDED to whatever else is turning it, not written over the top — the tin
                // being tipped over a glass IS rotated by somebody else, and an absolute
                // angle here held it upright through the whole pour (the author: "shakerdan
                // bardağa koyma sahnesinde shaker devrilmiyor koyarken"). Same law as the
                // rise: take off what this glow added last frame, then add this frame's.
                if (Sway > 0f)
                {
                    var owner = t.localRotation * Quaternion.Inverse(_addedRot[k]);
                    t.localRotation = owner * rot;
                    _addedRot[k] = rot;
                }
                if (Grow > 1f)
                {
                    var s = t.localScale;
                    t.localScale = new Vector3(s.x * undo * want, s.y * undo * want, s.z);
                }
                k++;
            });
            Front(g > 0.001f);
        }

        /// <summary>
        /// THE THING UNDER THE POINTER COMES TO THE FRONT (2026-09-06, the author: "mouse
        /// önüne gelen hiyerarşide en üste çıkmalı onun bir altında ışıklandırma olmalı").
        /// On a canvas that is the sibling order; in the room it is the sorting order. Both
        /// are put back exactly as they were when the pointer leaves — a prop that stayed on
        /// top would quietly re-stack the whole bar.
        /// </summary>
        private void Front(bool on)
        {
            if (on == _fronted) return;
            _fronted = on;
            var body = Body as RectTransform;
            if (body != null)
            {
                // NOT WHILE THE PANEL IS BEING PUT AWAY. Unity refuses a sibling move made
                // during a parent's activation, and a bench panel closing disables a whole
                // tree of these at once — which is exactly when the pointer's prop is being
                // let go of (2026-09-06: "Cannot change the sibling position of GameObject
                // 'ShakerCap' while activating or deactivating the parent"). The order is
                // put back on the way in instead; nothing looks at it while it is hidden.
                if (_stowing) return;
                if (on)
                {
                    _restIndex = body.GetSiblingIndex();
                    body.SetAsLastSibling();
                    // THE LIGHT STAYS DOWN THERE (2026-09-06, the author: "parlama efekti
                    // ana sahnede garnishlerin önünde kalıyor"). The prop comes to the
                    // front; its light does not follow it up, or a hovered dish throws its
                    // rim over the two dishes either side of it. Light comes from behind a
                    // thing, and things in front of it are allowed to stand in the way.
                }
                else
                {
                    if (_restIndex >= 0) body.SetSiblingIndex(Mathf.Min(_restIndex, body.parent.childCount - 1));
                    _restIndex = -1;
                }
                return;
            }
            if (Sprites == null) return;
            for (int i = 0; i < Sprites.Length; i++)
            {
                if (Sprites[i] == null) continue;
                Sprites[i].sortingOrder += on ? OrderLift : -OrderLift;
            }
            // ...and the world light stays where it is, for the reason above.
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
            // NO DRAWING, NO LIGHT (2026-09-06). The glow is cut FROM the prop's own art, so a
            // prop that has none has nothing to light: the ellipse fallback sized itself to the
            // hit RECT and a 180x384 plate became a pair of beams across the bench, which is
            // what the author photographed when a juice carton found no bottle sprite.
            if (LitSprite() == null) { HideHalo(); return; }
            // A light nobody has asked for is never built — and never built ON THE WAY OUT
            // either, which is what a fading-to-nothing prop and a closing panel both are.
            if (_halo == null && _haloSprite == null)
            {
                if (g <= 0.001f || _stowing) return;
                MakeHalo();
            }
            // The drawing can change under a lit prop — the tin's tier, a bottle's plates —
            // and the light is cut from that drawing, so it is re-cut when it does.
            var now = LitSprite();
            if (now != _haloFor && !_stowing) DressHalo(now);
            if (_halo != null)
            {
                var c = HaloTint;
                _halo.color = new Color(c.r, c.g, c.b, c.a * g);
                var rt = (RectTransform)_halo.transform;
                var body = Body as RectTransform;
                if (body != null)
                {
                    // THE GLOW IS THE DRAWING'S OWN SHAPE, and it is built on a canvas that
                    // is the drawing's canvas plus the reach on every side — so it lines up
                    // by construction. All this has to do is draw that canvas at the scale
                    // the prop draws its own, about the same centre.
                    // THE RECT'S CENTRE, NOT ITS PIVOT (2026-09-06, the author: "pour
                    // sahnelerinde parlamalar aşağı doğru kaymış"). The bench's props are
                    // hung by their feet — the bottle by 0.22, the spoon by its grip — and
                    // a light centred on the pivot of a 384-tall bottle sits a hundred
                    // units below the bottle.
                    rt.position = body.TransformPoint(body.rect.center);
                    rt.sizeDelta = HaloBox(body);
                    rt.localScale = body.localScale;
                    rt.localRotation = body.localRotation;
                    // ...and it keeps station DIRECTLY UNDER the prop while the pointer is
                    // on it. Ordering it once inside Front() is not enough: the light is
                    // made on the first hover, which is after Front() has already run.
                    if (_fronted && !_stowing) body.SetAsLastSibling();
                }
            }
            if (_haloSprite != null)
            {
                var c = HaloTint;
                _haloSprite.color = new Color(c.r, c.g, c.b, c.a * g);
            }
        }

        /// <summary>Takes the light off the screen without destroying it: a prop can be given
        /// a drawing later (the bench dresses its bottle every pick).</summary>
        private void HideHalo()
        {
            if (_halo != null) _halo.color = new Color(1f, 1f, 1f, 0f);
            if (_haloSprite != null) _haloSprite.color = new Color(1f, 1f, 1f, 0f);
        }

        /// <summary>The sprite whose shape the light is cut from: the prop's own drawing,
        /// canvas or world.</summary>
        private Sprite LitSprite()
        {
            var lit = Sprites != null && Sprites.Length > 0 ? Sprites[0] : null;
            if (lit != null && lit.sprite != null) return lit.sprite;
            var img = Graphics != null && Graphics.Length > 0 ? Graphics[0] as Image : null;
            if (img != null && img.sprite != null) return img.sprite;
            var sr = Body != null ? Body.GetComponent<SpriteRenderer>() : null;
            return sr != null ? sr.sprite : null;
        }

        /// <summary>
        /// The light behind it. Made on the first hover and then simply faded — a prop that
        /// is never pointed at never pays for one. On a canvas it is a SIBLING drawn just
        /// before the prop (a child would be in front of it); in the world it is a child
        /// renderer three sorting orders behind, because its hit plate is on the canvas and
        /// a bloom hung there would sit in FRONT of the bottle it is meant to be behind.
        /// </summary>
        private void MakeHalo()
        {
            var body = Body;
            if (body == null) return;
            var lit = Sprites != null && Sprites.Length > 0 ? Sprites[0] : null;
            if (lit == null && !(body is RectTransform)) lit = body.GetComponent<SpriteRenderer>();
            if (lit != null)
            {
                var wgo = new GameObject("Halo");
                wgo.transform.SetParent(lit.transform, false);
                var wsr = wgo.AddComponent<SpriteRenderer>();
                wsr.sortingLayerID = lit.sortingLayerID;
                wsr.sortingOrder = lit.sortingOrder - 3;
                wsr.color = new Color(HaloTint.r, HaloTint.g, HaloTint.b, 0f);
                _haloSprite = wsr;
                DressHalo(LitSprite());
                return;
            }
            var asRect = body as RectTransform;
            if (asRect == null) return;
            var go = new GameObject("Halo", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(asRect.parent, false);
            rt.SetSiblingIndex(asRect.GetSiblingIndex());   // just BEHIND the prop
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = new Color(HaloTint.r, HaloTint.g, HaloTint.b, 0f);
            _halo = img;
            DressHalo(LitSprite());
        }

        /// <summary>Cuts the light to this drawing. The world glow needs nothing else: it is
        /// built at the source's own pixels-per-unit and pivot, so a child at scale one sits
        /// exactly where the drawing is, however the stage has scaled it.</summary>
        private void DressHalo(Sprite src)
        {
            _haloFor = src;
            var art = src != null ? ChromeArt.Glow(src, ChromeArt.GlowSpread(src, Halo)) : ChromeArt.Halo();
            if (_halo is Image im) im.sprite = art;
            if (_haloSprite != null)
            {
                _haloSprite.sprite = art;
                _haloSprite.transform.localScale = Vector3.one;
                _haloSprite.transform.localPosition = Vector3.zero;
            }
        }

        /// <summary>The plate the canvas glow is drawn on: the drawing's canvas plus the
        /// reach on every side, at whatever scale the prop draws its own canvas.</summary>
        private Vector2 HaloBox(RectTransform rect)
        {
            var img = Graphics != null && Graphics.Length > 0 ? Graphics[0] as Image : null;
            var sp = _haloFor;
            var box = rect.rect.size;
            if (sp == null || sp.rect.width < 1f || sp.rect.height < 1f) return box;
            float grown = 2f * ChromeArt.GlowSpread(sp, Halo);
            if (img != null && !img.preserveAspect)
                return new Vector2(box.x * (1f + grown / sp.rect.width),
                                   box.y * (1f + grown / sp.rect.height));
            float k = Mathf.Min(box.x / sp.rect.width, box.y / sp.rect.height);
            return new Vector2((sp.rect.width + grown) * k, (sp.rect.height + grown) * k);
        }

        /// <summary>
        /// Has something above this prop stopped taking the pointer? A CanvasGroup that drops
        /// `blocksRaycasts` — the flow's own root while a stage slides, the counter's prop
        /// doors while the cellar is open — sends NO exit event, so a prop the pointer was on
        /// when the shutter came down stays lit for as long as the room is open (2026-09-06,
        /// the author: "musluk seçiliymiş gibi takılı kalabiliyor sürekli ön planda kalıp
        /// sanki mouse üstünde kalmış gibi duruyor"). Asked only while lit, and only up the
        /// parents this prop actually has.
        /// </summary>
        private bool Shuttered()
        {
            var t = transform;
            while (t != null)
            {
                var group = t.GetComponent<CanvasGroup>();
                if (group != null && (!group.blocksRaycasts || group.alpha <= 0.01f)) return true;
                t = t.parent;
            }
            return false;
        }

        private void LateUpdate()
        {
            if (_over && Shuttered()) _over = false;
            float want = _over || _beckoned ? 1f : 0f;
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
