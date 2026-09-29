using System;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// THE MENU KIT (2026-09-29, the author: "Ayarlar menüsünü/esc/dil vs. arkaplanıyla her şeyiyle tekrardan diğer
    /// sahneleri ürettiğine uygun bir tasarımda sıfırdan tekrar oluştur"). The builders every menu screen is made of,
    /// in the language the week's other screens were made in - the CLOSED sign, the cellar, the curtain's night sign:
    ///
    ///   CABINET  a Night[2] plate on a Graphite rim, crowned on stepped shoulders (MenuArt.Cabinet), its title a
    ///            NeonWord in the crown and a magenta tube round its body broken under the crown; both strike on when
    ///            the screen opens (NeonStrike)
    ///   RECESS   a panel let into the plate, where the keys stand (MenuArt.Recess); a WELL is a Night[0] one
    ///   SIGN KEY a key that is a small sign (SignKey): plate, terrazzo, neon mark, word dead centre, a ClubBlue tube
    ///            under the pointer; the one amber enamel key is the thing to press
    ///
    /// ONE MEANING PER COLOUR (BUILD_SPEC §1): MAGENTA is the house speaking (titles, the cabinet's tube, the verbs'
    /// marks); CYAN the second line and whatever is currently set; AMBER the one thing to press; CLUBBLUE the pointer;
    /// Graphite and Night the architecture - dark glass means unavailable.
    ///
    /// Everything is placed in FIELD units by its top-left corner (1280x720, y down, the way the mocks and the spec
    /// measure it), and every rect is even, so a 2x drawing's texels land on the house's grid. The shared builders
    /// (PackWordKey, PackIconKey, SurfaceKey, MenuPack, NightTitle, SunsetRules, NeonEdge, BluePlate) are untouched:
    /// the bill, the ladder, the game over, the ID pin and the bench still draw with them, and their baselines hold.
    /// </summary>
    public sealed partial class TycoonHud
    {
        /// <summary>The crown's minimum width, and the air it keeps round its title's light on either side.</summary>
        private const float CrownMinW = 240f, CrownAir = 24f;

        /// <summary>A sign key's heights: the door's column and the pause's verbs; the small row and the feet.</summary>
        private const float SignKeyH = 50f, SignKeySmallH = 46f;

        /// <summary>What a cabinet built: its plate, where its body starts (field y), its title and its tube.</summary>
        private sealed class MenuCabinetParts
        {
            public RectTransform Plate;
            public float Top, BodyTop, CrownW;
            public Text TitleText;
            public NeonWord Title;
            public NeonTube Tube;
            public NeonStrike Strike;
        }

        /// <summary>Places <paramref name="rt"/> by its top-left corner at field (<paramref name="x"/>,
        /// <paramref name="y"/>) of a parent that stands over the field (or any parent, by its own top-left).</summary>
        private static void FieldRect(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        /// <summary>A flat token rect at field coordinates (a ledge's row, a vignette's band).</summary>
        private static Image FieldFill(RectTransform parent, string name, float x, float y, float w, float h, Color colour)
        {
            var rt = NewRect(name, parent);
            FieldRect(rt, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = colour;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Stands a sign key centred on field x <paramref name="cx"/> with its top edge at field y
        /// <paramref name="top"/> (its pivot stays its centre, where a press lands).</summary>
        private static void PlaceSignKey(SignKey key, float cx, float top)
        {
            var rt = (RectTransform)key.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(cx, -(top + rt.sizeDelta.y * 0.5f));
        }

        /// <summary>Up to the next multiple of <paramref name="step"/>.</summary>
        private static float SnapUp(float v, float step) => Mathf.Ceil(v / step - 0.001f) * step;

        /// <summary>
        /// THE CABINET at field x <paramref name="x"/>, its crown's top at <paramref name="top"/>: <paramref name="w"/>
        /// wide, a crown <paramref name="crownH"/> tall over a body <paramref name="bodyH"/> tall. The crown is as wide as
        /// its title's LIGHT plus air - the title at display 24 (the neon's reach, 6 of the face's pixels, either side and
        /// 24 units of plate beyond it), snapped to 8, never under 240; a title that would not fit the body less 32 steps to
        /// display 16 (documented, never an overflow). THE LIGHT IS CLIPPED TO THE CROWN'S FACE (the critic, 2026-09-29:
        /// baked opaque and unclipped, the bands erased the crown's top rim and started on odd rows over the top bar): a
        /// RectMask2D from under the crown's rim and lit row, between its side rims, down to <paramref name="lightStopsAt"/>
        /// (the recess's top) - and the word hangs so its light's top row is the mask's top row, so nothing is cut.
        /// The magenta tube runs twelve units in from the body's edge, broken at the top under the crown.
        /// </summary>
        private MenuCabinetParts BuildMenuCabinet(RectTransform field, string name, float x, float top, float w,
            float crownH, float bodyH, string title, float lightStopsAt)
        {
            var parts = new MenuCabinetParts { Top = top, BodyTop = top + crownH };
            var plate = NewRect(name, field);
            FieldRect(plate, x, top, w, crownH + bodyH);
            var plateImg = plate.gameObject.AddComponent<Image>();
            plateImg.raycastTarget = true;                 // the room under it is on hold: a click on the plate stops here
            parts.Plate = plate;

            // the title, measured first: the crown is cut to it
            var clip = NewRect("TitleLight", plate);
            clip.gameObject.AddComponent<RectMask2D>();
            var text = NewText("Title", clip, _display, 24, TextAnchor.UpperCenter, UITheme.Cream[4]);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = title;
            int reach = NeonWord.ReachUnits(text);
            float crownW = Mathf.Max(CrownMinW, SnapUp(text.preferredWidth + 2f * (reach + CrownAir), 8f));
            float room = Mathf.Floor((w - 32f) / 8f) * 8f;
            if (crownW > room)
            {
                text.fontSize = LanguageFonts.Size(text.font, 16);
                reach = NeonWord.ReachUnits(text);
                crownW = Mathf.Min(room, Mathf.Max(CrownMinW, SnapUp(text.preferredWidth + 2f * (reach + CrownAir), 8f)));
            }
            parts.CrownW = crownW;

            plateImg.sprite = MenuArt.Cabinet(Mathf.RoundToInt(w / 2f), Mathf.RoundToInt(bodyH / 2f),
                Mathf.RoundToInt(crownW / 2f), Mathf.RoundToInt(crownH / 2f));
            plateImg.type = Image.Type.Simple;
            plateImg.color = Color.white;

            float crownX = (w - crownW) * 0.5f;
            FieldRect(clip, crownX + 2f, 4f, crownW - 4f, Mathf.Max(8f, lightStopsAt - top - 4f));
            FieldRect(text.rectTransform, -2f, reach, crownW, text.fontSize + 8f);   // caps' top one reach under the mask's top
            var word = text.gameObject.AddComponent<NeonWord>();
            word.Ramp = UITheme.Magenta;
            word.Ground = UITheme.Night[2];
            parts.TitleText = text;
            parts.Title = word;

            // the tube: its rim 12 x 12 texels inside the body's, so its sprite (with the light's pad) stands 6 units in
            int tw = Mathf.RoundToInt(w / 2f) - 12, th = Mathf.RoundToInt(bodyH / 2f) - 12;
            int gap = Mathf.RoundToInt(crownW / 2f) - 8;
            var size = new Vector2((tw + 2 * ChromeArt.NeonPad) * 2f, (th + 2 * ChromeArt.NeonPad) * 2f);
            parts.Tube = new NeonTube(plate, "Tube", layer => MenuArt.FrameTube(tw, th, 2, gap, layer), size,
                new Vector2(0f, 1f), new Vector2(6f + size.x * 0.5f, -(crownH + 6f + size.y * 0.5f)));
            parts.Tube.Show(NeonIcons.State.Lit, UITheme.Magenta, true);

            var strike = plate.gameObject.AddComponent<NeonStrike>();
            strike.Title = word;
            strike.Tube = parts.Tube;
            strike.TubeHue = UITheme.Magenta;
            parts.Strike = strike;
            return parts;
        }

        /// <summary>A RECESS (or, with Night[0], a WELL) let into a plate, at field coordinates of
        /// <paramref name="parent"/>: MenuArt.Recess 9-sliced at 2x.</summary>
        private static Image MenuRecess(RectTransform parent, string name, float x, float y, float w, float h, Color face)
        {
            var rt = NewRect(name, parent);
            FieldRect(rt, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = MenuArt.Recess(face);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.5f;           // one texel is two units
            img.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// A SIGN KEY (SignKey) named <paramref name="id"/> - the name the tests find it by - <paramref name="h"/> tall and
        /// as wide as its word plus the mark's slot and air a side (SignKey.Pad), snapped to 4, never under
        /// <paramref name="minW"/>. Its pivot is its CENTRE (the PlayMode suite presses a key's pivot point, and it lands
        /// on the plate). A real Button carries the click and the key's dead state (interactable). The mark
        /// <paramref name="icon"/> is a NeonIcons role in <paramref name="hue"/>; <paramref name="ground"/> is the Night
        /// step the key stands on. Not placed: the caller sets anchoredPosition.
        /// </summary>
        private SignKey MenuSignKey(RectTransform parent, string id, string word, string icon, Color[] hue, bool primary,
            int ground, float h, float minW, Action onClick, float pad = SignKey.Pad)
        {
            var rt = NewRect(id, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(minW, h);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);         // the hit area, drawn as nothing: the plate is the Body's
            hit.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            var key = rt.gameObject.AddComponent<SignKey>();
            button.onClick.AddListener(key.Click);
            key.Button = button;
            key.Pressed = onClick;
            key.Hue = hue ?? UITheme.Magenta;
            key.Primary = primary;
            key.Ground = ground;
            key.Word = word;
            key.WordPad = pad;

            var body = NewRect("Body", rt);
            Stretch(body, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var plate = NewRect("Plate", body);
            Stretch(plate, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var plateImg = plate.gameObject.AddComponent<Image>();
            plateImg.type = Image.Type.Sliced;
            plateImg.pixelsPerUnitMultiplier = 0.5f;
            plateImg.raycastTarget = false;
            // the terrazzo (the author's pick for the ESC keys, 2026-09-26), inside the rim and under the lit row
            var surf = NewRect("Surface", body);
            Stretch(surf, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -4f));
            var surfImg = surf.gameObject.AddComponent<Image>();
            surfImg.sprite = ChromeArt.KeySurfaceTile(ChromeArt.KeySurface.Terrazzo);
            surfImg.type = Image.Type.Tiled;
            surfImg.pixelsPerUnitMultiplier = 0.5f;
            surfImg.raycastTarget = false;

            // THE WORD DEAD CENTRE ON THE WHOLE KEY (2026-09-16, the author: "yazılar butonların tam ortasında"): the
            // face's caps ride 1.24 under its line's middle and its glyphs one unit left of the box's (both measured on
            // the pack keys), so the box sits a unit up and a unit right.
            var label = NewText("Label", body, _body, 16, TextAnchor.MiddleCenter, UITheme.Cream[4]);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(1f, 2f), new Vector2(1f, 0f));
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.text = word;
            float width = Mathf.Max(minW, SnapUp(label.preferredWidth + 2f * pad, 4f));
            rt.sizeDelta = new Vector2(width, h);

            if (NeonIcons.Has(icon))
                key.Icon = new NeonIcons.View(body, "Icon", icon, new Vector2(0f, 0.5f),
                    new Vector2(6f + NeonIcons.Size, 0f));  // its left six in; Size is half the 42 mark, its centre
            int kw = Mathf.RoundToInt(width / 2f), kh = Mathf.RoundToInt(h / 2f);
            key.Hover = new NeonTube(body, "Hover", layer => ChromeArt.NeonPath(kw, kh, 1, 0, layer),
                new Vector2((kw + 2 * ChromeArt.NeonPad) * 2f, (kh + 2 * ChromeArt.NeonPad) * 2f),
                new Vector2(0.5f, 0.5f), Vector2.zero);
            key.Hover.Visible = false;
            label.transform.SetAsLastSibling();            // the word over the mark and the pointer's tube

            key.Body = body;
            key.Plate = plateImg;
            key.Surface = surfImg;
            key.Label = label;
            key.FitWord();
            key.Apply();
            return key;
        }
    }
}
