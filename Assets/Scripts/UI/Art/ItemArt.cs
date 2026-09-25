using System.Collections.Generic;
using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// The drink item sprites (2026-07-23): bottles by style, the shaker, the serving glass,
    /// and the ice/lemon/salt/sugar preparations — hi-bit pixel art in Assets/Resources/Items,
    /// point-imported by PatronArtPostprocessor. Loaded once and cached; the service flow shows
    /// these in the menu boxes, in the pouring hand, and as the vessels themselves (GDD 24 §2–3).
    /// </summary>
    public static class ItemArt
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Load(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            // A MISS is never cached, and a destroyed sprite does not count as a hit
            // (Unity's fake-null). Both bit at once on 2026-08-05: plates shipped
            // while the editor sat in play, and a front plate asked for before its
            // import was remembered as "does not exist" for the whole session — the
            // sandwich then had no front, and the drink floated OVER the bottle.
            if (Cache.TryGetValue(name, out var s) && s != null) return s;
            s = Resources.Load<Sprite>($"Items/{name}");
            if (s != null) Cache[name] = s;
            else Cache.Remove(name);
            return s;
        }

        // (BoardPlate retired on 2026-08-26. The plate was generated art doing chrome's
        //  job and its frame was three different rails on one rectangle, none repeating —
        //  a nine-slice of noise. It is drawn now: ChromeArt.Instrument.)

        /// <summary>Forget every cached sprite — a new run re-resolves the art. The
        /// measurements go with them: a re-imported drawing is a new sprite and its old
        /// box would be an answer about a texture nobody is holding any more.</summary>
        public static void ClearCache() { Cache.Clear(); Opaque.Clear(); }

        // ── what a drawing actually covers (2026-09-04) ──────────────────────────
        //
        // Every pixel drawing in this game is a CANVAS with a drawing somewhere inside it,
        // and the two are not the same box: a 32x32 ice bucket carries two transparent rows
        // under its base, a v4 cellar copy is a 32x64 canvas round a 16px bottle. Anything
        // that stands a sprite ON something — the cellar's shelf, the counter's foot line —
        // has to ask where the DRAWING stops, or it lines up canvases and calls it level.
        //
        // It lived in DiegeticStage, where the cellar wrote it; it is here because the
        // counter needs the same answer, and two readings of one texture is how two props
        // on one bar end up on two lines.
        private static readonly Dictionary<Sprite, Rect> Opaque = new Dictionary<Sprite, Rect>();

        /// <summary>
        /// The opaque bounding box inside a sprite's canvas, in art pixels from its
        /// bottom-left. Measured once per sprite and kept — a texture read is not free and
        /// the answer cannot change while the sprite lives.
        ///
        /// A texture that is not readable answers with its whole canvas, which is the same
        /// thing every caller assumed before this existed: no worse than not asking.
        /// </summary>
        public static Rect OpaqueBounds(Sprite sp)
        {
            if (sp == null) return Rect.zero;
            if (Opaque.TryGetValue(sp, out var hit)) return hit;
            var r = Measure(sp);
            Opaque[sp] = r;
            return r;
        }

        /// <summary>How many transparent art rows sit UNDER the drawing — what a prop has to
        /// be lifted by for its lowest drawn pixel to land on a line.</summary>
        public static float FootPadding(Sprite sp)
        {
            var ob = OpaqueBounds(sp);
            return ob.width > 0f ? ob.y : 0f;
        }

        private static Rect Measure(Sprite sp)
        {
            var tex = sp.texture;
            if (tex == null || !tex.isReadable) return new Rect(0, 0, sp.rect.width, sp.rect.height);
            int x0 = Mathf.RoundToInt(sp.rect.x), y0 = Mathf.RoundToInt(sp.rect.y);
            int w = Mathf.RoundToInt(sp.rect.width), h = Mathf.RoundToInt(sp.rect.height);
            var px = tex.GetPixels32();
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (px[(y0 + y) * tex.width + x0 + x].a > 127)
                    {
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
            if (maxX < 0) return Rect.zero;
            return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        // ── the house's two icons (2026-09-04) ───────────────────────────────────
        //
        // ONE STAR AND ONE HEART, EVERYWHERE (the author: "bundan sonra oyunda kalp ve
        // yıldız iconu olarak her yerde bunları kullanacaksın"). The game used to count in
        // three different stars — the author's shaded star3d in the top bar, a flat white
        // Items/star tinted per caller, and a 16px silhouette drawn in ChromeArt — which is
        // three answers to "how many stars is this bar". There is one now, in two states
        // (lit, and the empty socket), and the heart is drawn to the same construction
        // (Tools/heart_icon.py).
        //
        // THEY CARRY THEIR OWN COLOUR. A caller may dim one — the alpha still reads — but
        // nothing tints them any more: a gold star tinted with a line's ink is a different
        // star, which is the thing that was wrong with the flat one.
        //
        // Each ships at two sizes and this picks between them, because a 32px shaded icon
        // squeezed onto a 14px square under a point filter is mud (the bottle lesson,
        // PLAN_bottle_art_v4 §9.18): the 16 is drawn for the small rows, the 32 for the
        // night's big gauge. Falls back to the old flat star if the art is missing, so a
        // half-imported project still counts.

        /// <summary>The star, lit or empty, at the size it is about to be drawn.</summary>
        // THE AUTHOR'S OWN (2026-09-08: "yeni yıldız ve kalp görsellerini veriyorum, oyunda
        // bunları kullanacağız her yerde"). Two stars — 14x12 for the small rows, 18x17 for
        // the big ones — each with its socket; one heart with a lit, a half and a socket
        // state. Every caller already comes through here, so the swap is this one place;
        // the star3d/heart3d files stay on disk until the author retires them.
        public static Sprite Star(bool lit, float px) =>
            Load(px <= 16f ? (lit ? "star_small" : "star_small_socket")
                           : (lit ? "star_big" : "star_big_socket"))
            ?? Load(Name("star3d", lit, px)) ?? Load("star");

        /// <summary>The heart, lit or empty, at the size it is about to be drawn.</summary>
        public static Sprite Heart(bool lit, float px) =>
            Load(lit ? "heart_lit" : "heart_socket") ?? Load(Name("heart3d", lit, px));

        /// <summary>The half-earned heart — the author drew one (2026-09-08).</summary>
        public static Sprite HeartHalf() => Load("heart_half") ?? Heart(true, 16f);

        /// <summary>The medallion — COMFORT's symbol (GDD 27 §2.2), drawn in the star's own
        /// language by Tools/medallion_icon.py — lit or empty, at the size it is drawn at.</summary>
        public static Sprite Medal(bool lit, float px) =>
            Load(Name("medal3d", lit, px));

        /// <summary>THE PERFECT MARK (2026-09-09, the author: "perfect tarif için bir icon
        /// oluştur bu iconu perfect tarif için kullanalım") — a platinum rosette drawn in the
        /// star's own language by Tools/perfect_icon.py, at the two sizes the set keeps. It
        /// has one state: a recipe is perfected or the mark is not drawn.</summary>
        public static Sprite Perfect(float px) =>
            Load(px <= 16f ? "perfect3d_16" : "perfect3d") ?? Load("perfect3d");

        /// <summary>MONEY'S OWN MARK (2026-09-07, the author: "2D siyah kontras yesil ic planli
        /// dolar iconu uret ve oyunda para gosteren her yere $ yerine o iconu koy"). Every
        /// figure that meant money used to be printed with a typed dollar sign, so the till
        /// read in one face, the market in another and the slip in a third — three dollars for
        /// one currency, none of them an object. This is the fourth member of the star / heart
        /// / medallion set: same canvas, same keyline, the interior in Lime because money is
        /// the only green thing in the chrome. Drawn at both sizes by Tools/coin_icon.py rather
        /// than derived, because the derivation peels ink and this icon's glyph IS ink.</summary>
        /// <summary>THE HOUSE'S DOLLAR (2026-09-08, the author: "oyunda her yerde
        /// kullanacağımız $ ve para iconunu paylaştım, tüm oyuna entegre et"): the author's
        /// green dollar, 32 as drawn, its own 19x23 glyph centred on a 24 for the tiles
        /// and the boards, and a 2:1 majority cut for the 16. The old green coin
        /// (coin3d) is retired; the accessor is the same, so every caller changed at once.</summary>
        public static Sprite Coin(float px = 24f) =>
            Load("money" + SizeSuffix(px))
            ?? Load("dollar" + SizeSuffix(px))
            ?? Load("coin3d" + SizeSuffix(px));

        /// <summary>
        /// THE MONEY MARK (2026-09-25, the author: "YENİ $ İCONU OLUŞTUR VE ONU KULLANALIM OYUNDA"): the pick from
        /// Tools/money_icon.py once it ships as Items/money[_24|_16].png. Until then the drawn coin the book and the
        /// buffs already wear - so nothing moves before the author has chosen, and everything moves together after.
        /// <see cref="Coin"/> prefers the same file, so the top bar and the tips follow the same pick.
        /// </summary>
        public static Sprite Money(float px = 24f) =>
            Load("money" + SizeSuffix(px)) ?? Load("coin3d" + SizeSuffix(px)) ?? Coin(px);

        private static string SizeSuffix(float px) => px >= 30f ? "" : px >= 22f ? "_24" : "_16";

        /// <summary>
        /// THE PRICE MARK (2026-09-25, the author: "fiyatlarda da D işaret'i kullanabiliriz"): the $ itself, heavy,
        /// green and keylined (Tools/money_icon.py, candidate D), at the size it is drawn at. A PRICE wears it - the
        /// menu's plinth, a market tile, a fitting card, the basket's total - where the account and the till wear the
        /// stack (<see cref="Money"/>).
        /// </summary>
        public static Sprite Price(float px = 16f) => Load("price" + SizeSuffix(px)) ?? Money(px);

        /// <summary>THE ONE BILL the stack is made of (26x16): what flies when money moves (TycoonHud.MoneyFlight).</summary>
        public static Sprite MoneyBill() => Load("money_bill");

        private static string Name(string icon, bool lit, float px) =>
            icon + (lit ? "" : "_socket") + (px <= 20f ? "_16" : "");

        /// <summary>
        /// A bottle OF A STYLE, for a line whose style the shelf does not carry: the first
        /// card of that style in the catalogue handed in. Null when there is none — the
        /// old <c>Items/{style}.png</c> bottles this used to fall back on were the v2 set,
        /// swept on 2026-09-05 once every pourable card had its v4 sandwich.
        /// </summary>
        public static Sprite StyleBottle(IReadOnlyList<LastCall.Core.IngredientCard> catalogue, string style)
        {
            if (catalogue == null || string.IsNullOrEmpty(style)) return null;
            foreach (var c in catalogue)
                if (c?.Info?.Style == style)
                {
                    var a = Bottle(c);
                    if (a != null) return a;
                }
            return null;
        }

        // ── the v4 sandwich (2026-09-04, Docs/PLAN_bottle_art_v4.md) ──────────────
        //
        // A v4 bottle is THREE plates on one 96x192 canvas: the interior (back), the drink's
        // mask (shoulder to base — full means the shoulder), and the glass front with the
        // label pressed on it. The cellar carries the same three at 32x64, rebuilt at that
        // size rather than shrunk, with a drawn cap. Nothing here composes them: the hand
        // bench builds a BottleArt, the cellar builds sprite renderers under a SpriteMask.

        /// <summary>The v4 plates for a card, or null if the card has no v4 art yet.
        /// <paramref name="cellar"/> picks the 32x64 set.</summary>
        public static BottlePlates Plates(LastCall.Core.IngredientCard card, bool cellar = false)
        {
            if (card == null) return null;
            string s = cellar ? "_c" : "";
            var front = Load("v4_" + card.Id + "_front" + s);
            if (front == null) return null;
            var mask = Load("v4_" + card.Id + "_mask" + s);
            var plates = new BottlePlates(Load("v4_" + card.Id + "_back" + s), mask, front);
            return mask != null ? FootFilled(plates, "v4_" + card.Id + s) : plates;
        }

        /// <summary>
        /// THE DRINK TO THE OUTLINE (2026-09-16, the author: "şişelerin hala altı düz gidiyor şişenin altı 3 boyutlu
        /// düşünülmeli ve görseldeki siyah pixeller sınırında dolmalı tüm şişelerde aynı hata var"). The shipped mask
        /// stops three rows over the foot (Tools/v4_bottles/process.py BASE) and the front's base rows are opaque
        /// glass, so every bottle showed a flat line of empty base under its drink. Fixed here, as the plates are
        /// loaded, rather than by re-shipping over the author's hand-edited fronts: column by column the mask is
        /// carried down from its lowest row to the outline's ink (the drink then takes the base's rounded shape from
        /// the stencil), and the front's pixels over that band go to a film so the drink shows through the base's
        /// own shading. Cached against the sprite's name.
        /// </summary>
        private static BottlePlates FootFilled(BottlePlates p, string key)
        {
            string mk = key + ":foot:mask", fk = key + ":foot:front";
            if (Cache.TryGetValue(mk, out var gm) && gm != null && Cache.TryGetValue(fk, out var gf) && gf != null)
                return new BottlePlates(p.Back, gm, gf);
            var mt = p.Mask.texture; var ft = p.Front.texture;
            if (mt == null || ft == null || mt.width != ft.width || mt.height != ft.height) return p;
            int w = mt.width, h = mt.height;
            Color32[] m, f;
            try { m = mt.GetPixels32(); f = ft.GetPixels32(); }
            catch (UnityException) { return p; }   // an unreadable texture keeps its plates as shipped
            const byte Film = 96;
            int changed = 0;
            for (int x = 0; x < w; x++)
            {
                int low = -1;                                  // the mask's lowest opaque row (y counts up)
                for (int y = 0; y < h; y++) if (m[y * w + x].a >= 128) { low = y; break; }
                if (low <= 0) continue;
                for (int y = low - 1; y >= 0; y--)
                {
                    var c = f[y * w + x];
                    if (c.a == 0) break;                                                   // off the silhouette
                    if (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f < 40f) break;            // the outline's ink
                    m[y * w + x] = new Color32(255, 255, 255, 255);
                    if (c.a > Film) f[y * w + x] = new Color32(c.r, c.g, c.b, Film);
                    changed++;
                }
            }
            if (changed == 0) return p;
            Sprite Copy(Texture2D src, Color32[] px, Sprite like)
            {
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = src.filterMode, wrapMode = src.wrapMode, name = src.name + "_foot" };
                t.SetPixels32(px); t.Apply(false, false);
                var sp = Sprite.Create(t, like.rect, new Vector2(like.pivot.x / like.rect.width, like.pivot.y / like.rect.height),
                    like.pixelsPerUnit, 0, SpriteMeshType.FullRect, like.border);
                sp.name = like.name + "_foot";
                return sp;
            }
            var mask2 = Copy(mt, m, p.Mask);
            var front2 = Copy(ft, f, p.Front);
            Cache[mk] = mask2; Cache[fk] = front2;
            return new BottlePlates(p.Back, mask2, front2);
        }

        public sealed class BottlePlates
        {
            public readonly Sprite Back, Mask, Front;
            public BottlePlates(Sprite back, Sprite mask, Sprite front) { Back = back; Mask = mask; Front = front; }
            /// <summary>A sealed vessel (carton, can) ships one sprite and no cavity.</summary>
            public bool Sealed => Mask == null;
        }

        public static Sprite Bottle(LastCall.Core.IngredientCard card)
        {
            if (card == null) return null;
            // The CELLAR front is the capped, closed bottle — the icon of the brand
            // wherever a closed bottle is asked for (market, hover card, the cellar).
            var v4 = Load("v4_" + card.Id + "_front_c") ?? Load("v4_" + card.Id + "_c");
            if (v4 != null) return v4;
            // A garnish has no bottle: its dish on the counter is its picture, on the
            // market board as on the bar. (The flat v3 plates, the bot_{id} takes and the
            // v2 style bottles that used to stand behind this line were swept on
            // 2026-09-05 — every pourable card has its v4 sandwich, so nothing reached them.)
            // LIFTED LIKE THE RAIL'S (2026-09-23, the author: "Garnishler hem menü görsellerinde
            // hem de ana sahnede çok karanlık kalıyorlar"): the market board, the cellar's hover
            // card and the day-end rows show the same brightened dish the counter does, so a
            // bowl of olives is one picture wherever it is met. GarnishArt never writes the PNG.
            if (card.Type == LastCall.Core.IngredientType.Garnish)
                return GarnishArt.Lift(Load("counter_" + card.Info?.Style));
            return null;
        }

        /// <summary>
        /// THE BOTTLE WITH ITS DRINK IN IT, AS ONE PICTURE (2026-09-25, the author: "Menüde tariflerde gösterilen
        /// alkol şişeleri dolu olsun"). <see cref="Bottle"/> is the cellar's FRONT plate alone — the glass, the label
        /// and the cap — so on the book's rows every bottle stood there empty. The market fills its bottles with a live
        /// <see cref="BottleArt"/> (a stencil mask and five layers), which is right for one tile and wrong for fifty
        /// rows: this flattens the same three cellar plates once — the interior, the drink through its cavity at full,
        /// the glass over it — into one sprite, cached against the card. A card with no v4 plates (a carton, a garnish
        /// dish) is its flat picture, which is already full.
        /// </summary>
        public static Sprite BottleFull(LastCall.Core.IngredientCard card)
        {
            if (card == null) return null;
            string key = "full:" + card.Id;
            if (Cache.TryGetValue(key, out var hit) && hit != null) return hit;
            var plates = Plates(card, cellar: true);
            if (plates == null || plates.Mask == null || plates.Back == null) return Bottle(card);
            var bt = plates.Back.texture; var mt = plates.Mask.texture; var ft = plates.Front.texture;
            Rect r = plates.Front.rect;
            int w = (int)r.width, h = (int)r.height, x0 = (int)r.x, y0 = (int)r.y;
            Color32[] b, m, f;
            try
            {
                b = bt.GetPixels32(); m = mt.GetPixels32(); f = ft.GetPixels32();
            }
            catch (UnityException) { return Bottle(card); }   // an unreadable plate keeps the bare front
            if (bt.width != ft.width || mt.width != ft.width || bt.height != ft.height || mt.height != ft.height)
                return Bottle(card);
            Color drink = UITheme.LiquidColor(card.Info?.Style, card.Type);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = (y0 + y) * ft.width + x0 + x;
                    Color c = b[i];
                    // The drink fills the cavity to its shoulder (the mask IS the full bottle) and is opaque, as the
                    // market's full bottle draws it; the front's film and label then lie over it at their own alpha.
                    if (m[i].a >= 128) c = new Color(drink.r, drink.g, drink.b, 1f);
                    Color o = f[i];
                    float a = o.a + c.a * (1f - o.a);
                    Color outC = a <= 0f ? new Color(0, 0, 0, 0)
                        : new Color((o.r * o.a + c.r * c.a * (1f - o.a)) / a,
                                    (o.g * o.a + c.g * c.a * (1f - o.a)) / a,
                                    (o.b * o.a + c.b * c.a * (1f - o.a)) / a, a);
                    px[y * w + x] = outC;
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "full_" + card.Id,
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var like = plates.Front;
            var sp = Sprite.Create(tex, new Rect(0, 0, w, h),
                new Vector2(like.pivot.x / like.rect.width, like.pivot.y / like.rect.height), like.pixelsPerUnit);
            sp.name = "full_" + card.Id;
            return Cache[key] = sp;
        }

        /// <summary>
        /// THE GLASS AS THE GAME DRAWS IT, SMALL (2026-09-25, the author: "The glass kısmında menüde kullanılan
        /// bardaklar oyundaki bardakların iconu olmalı şu an oyundaki bardağa benzemeyen bardaklar kullanılıyor").
        /// The book's GLASS chip wore one generic stemmed mark for every glass — a coupe and a highball were the same
        /// wine glass. This is the bench's own glass (<c>glass3d_&lt;id&gt;</c>, the author's drawing) brought down
        /// to <paramref name="maxH"/> by a whole factor: each block of source pixels is averaged in linear light over
        /// its opaque pixels, kept only where at least half the block is glass, and snapped back to the nearest colour
        /// the drawing itself uses, so the small one is made of the big one's own inks rather than of a blur.
        /// </summary>
        public static Sprite GlassIcon(string glassId, int maxH = 16)
        {
            if (string.IsNullOrEmpty(glassId)) return null;
            string key = "glassicon:" + glassId + ":" + maxH;
            if (Cache.TryGetValue(key, out var hit) && hit != null) return hit;
            var src = Load("glass3d_" + glassId);
            if (src == null) return null;
            var t = src.texture;
            Color32[] all;
            try { all = t.GetPixels32(); }
            catch (UnityException) { return null; }
            Rect sr = src.rect;
            int sx = (int)sr.x, sy = (int)sr.y, sw = (int)sr.width, sh = (int)sr.height;
            // The drawing's own box: its canvas carries air around the glass.
            int minX = sw, minY = sh, maxX = -1, maxY = -1;
            for (int y = 0; y < sh; y++)
                for (int x = 0; x < sw; x++)
                    if (all[(sy + y) * t.width + sx + x].a >= 128)
                    {
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
            if (maxX < 0) return null;
            int cw = maxX - minX + 1, ch = maxY - minY + 1;
            int k = Mathf.Max(1, Mathf.CeilToInt(ch / (float)maxH));
            int ow = Mathf.CeilToInt(cw / (float)k), oh = Mathf.CeilToInt(ch / (float)k);
            var palette = new List<Color32>();
            var seen = new HashSet<int>();
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    var c = all[(sy + y) * t.width + sx + x];
                    if (c.a < 128) continue;
                    int id = (c.r << 16) | (c.g << 8) | c.b;
                    if (seen.Add(id)) palette.Add(c);
                }
            var px = new Color32[ow * oh];
            for (int oy = 0; oy < oh; oy++)
                for (int ox = 0; ox < ow; ox++)
                {
                    float lr = 0, lg = 0, lb = 0; int solid = 0, cells = 0;
                    for (int dy = 0; dy < k; dy++)
                        for (int dx = 0; dx < k; dx++)
                        {
                            int x = minX + ox * k + dx, y = minY + oy * k + dy;
                            if (x > maxX || y > maxY) continue;
                            cells++;
                            var c = all[(sy + y) * t.width + sx + x];
                            if (c.a < 128) continue;
                            solid++;
                            lr += Mathf.GammaToLinearSpace(c.r / 255f);
                            lg += Mathf.GammaToLinearSpace(c.g / 255f);
                            lb += Mathf.GammaToLinearSpace(c.b / 255f);
                        }
                    if (cells == 0 || solid * 2 < cells) { px[oy * ow + ox] = new Color32(0, 0, 0, 0); continue; }
                    var avg = new Color(Mathf.LinearToGammaSpace(lr / solid), Mathf.LinearToGammaSpace(lg / solid),
                        Mathf.LinearToGammaSpace(lb / solid));
                    Color32 best = palette[0]; float bestD = float.MaxValue;
                    foreach (var p in palette)
                    {
                        float d = (p.r / 255f - avg.r) * (p.r / 255f - avg.r) + (p.g / 255f - avg.g) * (p.g / 255f - avg.g)
                                + (p.b / 255f - avg.b) * (p.b / 255f - avg.b);
                        if (d < bestD) { bestD = d; best = p; }
                    }
                    px[oy * ow + ox] = new Color32(best.r, best.g, best.b, 255);
                }
            // AND ITS OUTLINE BACK. The drawing's 1-px ink is averaged away by any factor over one, and a pale
            // glass on the book's cream paper then reads as nothing at all (measured on the five glasses). Every
            // pixel of the silhouette's edge takes the drawing's own darkest ink, so the small glass is keylined
            // the way the big one is - and a stem, which is all edge, is drawn as a line.
            Color32 ink = palette[0];
            foreach (var p in palette)
                if (p.r * 299 + p.g * 587 + p.b * 114 < ink.r * 299 + ink.g * 587 + ink.b * 114) ink = p;
            var edged = (Color32[])px.Clone();
            for (int oy = 0; oy < oh; oy++)
                for (int ox = 0; ox < ow; ox++)
                {
                    if (px[oy * ow + ox].a == 0) continue;
                    bool Clear(int x, int y) => x < 0 || y < 0 || x >= ow || y >= oh || px[y * ow + x].a == 0;
                    if (Clear(ox + 1, oy) || Clear(ox - 1, oy) || Clear(ox, oy + 1) || Clear(ox, oy - 1))
                        edged[oy * ow + ox] = new Color32(ink.r, ink.g, ink.b, 255);
                }
            px = edged;
            var tex = new Texture2D(ow, oh, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "glassicon_" + glassId,
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sp = Sprite.Create(tex, new Rect(0, 0, ow, oh), new Vector2(0.5f, 0f), 100f);
            sp.name = "glassicon_" + glassId;
            return Cache[key] = sp;
        }

        /// <summary>The same brand with its closure off — what the pour stage shows, because
        /// a bottle you are pouring from is open.</summary>
        public static Sprite BottleOpen(LastCall.Core.IngredientCard card)
        {
            if (card == null) return null;
            // v4: the master IS the open bottle (generated uncapped), so the hand front is it.
            var v4 = Load("v4_" + card.Id + "_front") ?? Load("v4_" + card.Id);
            return v4 != null ? v4 : Bottle(card);
        }

        public static Sprite Shaker => Load("shaker");

        // (Bucket retired 2026-08-27 with the eight drawings behind it. Its one caller
        //  was the serve bench's AddFinishTub, cut with the finishing table; the room's
        //  counter draws its own dishes from the rail table in TycoonHud.Seats.)
    }
}
