using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    // TycoonHud, part LitProps: the things standing on the bar are IN the room (2026-09-22).
    //
    // The author: "Garnishler ve menü ve bez stage world'ün içinde olmadığından sahnede kullandığımız
    // ışıklandırmalardan etkilenmiyor. Neden orada konumlandırdın, orada konumlanması bizim işimize mi yarıyor?
    // Eğer hayırsa değiştir."
    //
    // WHY THEY WERE ON THE CANVAS. Every one of them is a VERB before it is a picture: a dish is picked up and
    // carried to a glass, the book opens the menu, the cloth is taken off its rail and wiped along the counter.
    // uGUI is what the pointer work is written in - hit plates, alpha hit tests, hover cards, drag - and the
    // quickest way to give a prop a verb was to draw it where the verbs already live.
    //
    // WHAT IT COST. A canvas on an overlay is outside every Light2D in the room, so the pendants, the window and
    // the house lamps stopped at the counter's edge: a lit slab with unlit crockery standing on it. It was papered
    // over by multiplying the counter's light into the images by hand, which is a guess that cannot know where a
    // lamp is - and the author saw it.
    //
    // WHAT IT IS NOW. The rect stays exactly where it was - the hit target, the hover's lift, the drag's origin,
    // untouched - and is simply not drawn; the PICTURE is a world sprite on the stage that follows it frame for
    // frame and takes the room's light like the counter under it. It is the same trick the drinkers have used
    // since 2026-08-10 (DiegeticStage.NewStageSprite), applied to the six dishes, the menu and the cloth.
    public sealed partial class TycoonHud
    {
        /// <summary>A prop whose rect lives on the HUD and whose picture lives in the room.</summary>
        private sealed class LitProp
        {
            public RectTransform Rt;      // where it stands, and what the pointer hits
            public Image Img;             // the picture it used to be drawn as: its sprite and its tint
            public SpriteRenderer Sr;     // what the room lights
            public CanvasGroup Group;     // holds the canvas copy invisible while keeping its raycasts
            public bool OnCanvas;         // drawn on the HUD again for as long as its card stands over it
            public SpriteRenderer Own;    // the unlit share of the picture, over Sr (null for most props)
            public float OwnShare;        // its alpha: how much of the prop is its own colour
        }

        // A PROP KEEPS A SHARE OF ITS OWN COLOUR (2026-09-23, the author: "Garnishler hem menü görsellerinde hem de
        // ana sahnede çok karanlık kalıyorlar"). Lit, a dish is the drawing times the room, and the counter's light is
        // low and amber: the blue goes first, so the ice and the salt went tan and the mint went olive-drab. A second,
        // UNLIT copy of the picture drawn over the lit one at alpha OwnShare gives, per pixel,
        //     share * drawing + (1 - share) * room(drawing)
        // so the room still makes most of it — warm under the pendants, pink at the neon, dimmer at the last call — and
        // the prop can never fall below its share of itself. The room's light is clamped at 1 (the renderer's HDR
        // emulation scale is 1), so the lit copy is never brighter than the drawing, and neither is the mix: it cannot
        // glow. Clear pixels are clear in both copies, so the counter never shows through a dish ("katı olması
        // gerekiyor", 2026-09-21, still holds). The book and the cloth take no share; only the dishes ask for one.
        private static Material s_ownLight;

        /// <summary>The one unlit material every prop's own share draws with, made once. The same two shaders
        /// DiegeticStage already finds for the view through the window, so both are in the build.</summary>
        private static Material OwnLightMaterial
        {
            get
            {
                if (s_ownLight == null)
                {
                    var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                             ?? Shader.Find("Sprites/Default");
                    if (sh != null) s_ownLight = new Material(sh) { name = "PropOwnLight" };
                }
                return s_ownLight;
            }
        }

        private readonly List<LitProp> _litProps = new List<LitProp>();
        private readonly Dictionary<RectTransform, LitProp> _litByRect = new Dictionary<RectTransform, LitProp>();
        private readonly Vector3[] _litCorners = new Vector3[4];

        /// <summary>
        /// A PROP COMES BACK TO THE CANVAS WHILE ITS CARD IS UP (2026-09-22). The garnish card is a HUD plate with a
        /// SLOT cut for the dish it is about, and the dish used to rise through that slot on a canvas of its own
        /// (the author, 2026-09-08: "üstüne gelinen asset hiyerarşide en üste, bilgi kartının üstüne çıksın,
        /// odağa"). A world sprite cannot rise over an overlay canvas at all, so for exactly as long as the card
        /// stands, the dish is drawn where it used to be - over the card, in focus - and the room's copy stands
        /// down. Every other frame it is in the room.
        /// </summary>
        private void PropOnCanvas(RectTransform rt, bool onCanvas)
        {
            if (rt == null || !_litByRect.TryGetValue(rt, out var p) || p.Group == null) return;
            p.Group.alpha = onCanvas ? 1f : 0f;
            p.OnCanvas = onCanvas;
            if (p.Sr != null && p.Sr.gameObject.activeSelf == onCanvas) p.Sr.gameObject.SetActive(!onCanvas);
        }

        /// <summary>
        /// Moves a counter prop's PICTURE into the room. The rect keeps everything it had; the image is held at
        /// nothing and mirrored by a stage sprite at <paramref name="order"/> (the stage's ledger: the bar is 30,
        /// what stands on it 35, the cloth in front of it 36). <paramref name="ownLight"/> above 0 lays that share of
        /// the prop's own, unlit colour over the lit picture (see OwnLightMaterial); 0 leaves it wholly the room's.
        /// </summary>
        private void IntoTheRoom(string name, RectTransform rt, Image img, int order = 35, float ownLight = 0f)
        {
            if (rt == null || img == null) return;
            var theStage = stage != null ? stage : FindFirstObjectByType<DiegeticStage>();
            if (theStage == null) return;                      // a bench scene with no room: it stays on the canvas
            var group = rt.GetComponent<CanvasGroup>();
            if (group == null) group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = true;                       // invisible, and still the thing the pointer is on
            group.interactable = true;
            var sr = theStage.NewStageSprite(name, order);
            SpriteRenderer own = null;
            if (ownLight > 0f && OwnLightMaterial != null)
            {
                // A CHILD of the lit sprite, so its position, scale, turn and on/off (PropOnCanvas, StepLitProps) all
                // follow for nothing. Same layer, same order, a hair nearer the camera: the 2D renderer sorts a tie by
                // z (Renderer2D: transparency sort Default, orthographic), so it draws after the lit copy, over it.
                var go = new GameObject(name + "_Own");
                go.transform.SetParent(sr.transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, -0.01f);
                own = go.AddComponent<SpriteRenderer>();
                own.sharedMaterial = OwnLightMaterial;
                own.sortingLayerID = sr.sortingLayerID;
                own.sortingOrder = sr.sortingOrder;
            }
            var prop = new LitProp { Rt = rt, Img = img, Sr = sr, Group = group, Own = own, OwnShare = ownLight };
            _litProps.Add(prop);
            _litByRect[rt] = prop;
        }

        /// <summary>
        /// One frame of the room's copy: where the rect is, at the size the rect draws it, in the tint the HUD
        /// asked for. Costs one rect read a prop — the same price the drinkers' bodies pay.
        /// </summary>
        private void StepLitProps()
        {
            if (_litProps.Count == 0 || _hudRoot == null) return;
            for (int i = 0; i < _litProps.Count; i++)
            {
                var p = _litProps[i];
                if (p.Sr == null || p.Rt == null || p.Img == null) continue;
                var sprite = p.Img.sprite;
                bool on = !p.OnCanvas && sprite != null && p.Img.enabled && p.Rt.gameObject.activeInHierarchy;
                if (p.Sr.gameObject.activeSelf != on) p.Sr.gameObject.SetActive(on);
                if (!on) continue;

                p.Sr.sprite = sprite;
                // The HUD's own tint carries what the rail is SAYING — a dish that cannot be used is darkened —
                // so it is kept; its alpha is not, because a prop in a room is either there or it is not.
                var c = p.Img.color;
                p.Sr.color = new Color(c.r, c.g, c.b, 1f);
                if (p.Own != null)
                {
                    // the prop's own share: the same picture, in the same tint (so a spent dish is spent in both)
                    p.Own.sprite = sprite;
                    p.Own.color = new Color(c.r, c.g, c.b, p.OwnShare);
                }

                // the rect, in the HUD's own units (through the root, so a prop on its own canvas measures the same)
                p.Rt.GetWorldCorners(_litCorners);
                var lo = _hudRoot.InverseTransformPoint(_litCorners[0]);
                var hi = _hudRoot.InverseTransformPoint(_litCorners[2]);
                float wide = Mathf.Abs(hi.x - lo.x), tall = Mathf.Abs(hi.y - lo.y);
                var mid = (lo + hi) * 0.5f;

                // what the Image actually draws inside that rect: preserveAspect letterboxes it
                float sw = sprite.rect.width, sh = sprite.rect.height;
                float fit = p.Img.preserveAspect ? Mathf.Min(wide / sw, tall / sh) : 1f;
                float drawnW = p.Img.preserveAspect ? sw * fit : wide;
                float drawnH = p.Img.preserveAspect ? sh * fit : tall;

                float unit = Mathf.Max(0.0001f, sprite.bounds.size.x);
                float k = (drawnW / StageToHud) / unit;
                float ky = (drawnH / StageToHud) / Mathf.Max(0.0001f, sprite.bounds.size.y);
                p.Sr.transform.localScale = new Vector3(k, ky, 1f);
                p.Sr.transform.localRotation = Quaternion.Euler(0f, 0f, p.Rt.eulerAngles.z);
                // THE MIDDLE IS THE MIDDLE. The drinkers' bodies convert from a seat rect anchored at the HUD's
                // bottom-left, so they subtract half the stage to get there; this reads the rect THROUGH the HUD
                // root, whose pivot is already the middle of the screen, and the stage's origin is that same
                // middle - so one halving is the whole conversion. Subtracting again put every prop a screen and
                // a half to the left (measured, r182: the ice bowl at world x -475 of a room that ends at -320).
                p.Sr.transform.position = new Vector3(mid.x / StageToHud, mid.y / StageToHud, 0f);
            }
        }
    }
}
