# -*- coding: utf-8 -*-
"""THE KEY ART: every store and library picture laid out from ONE master composition.

The master is a beach bar opening onto the steam_kit sunset, seen from behind its counter:

    plate   - plate.py: the author's page-background sunset, sea, skyline and palms, dusk bands above for the title
    counter - ClubBlue marble (the steam_kit panel's navy-black, veined) with the panels' magenta neon lip; on a
              tall front, 12-texel MiMo flutes and the kit's neon rule
    Roxy    - roxy_layer.py: one cut-out, her fingers on the marble, the slab passing in front of her waist
    props   - props.py: a tequila-sunrise highball and the patron licence under her fingertips (the game's hook)
    title   - the MALIBU CLUB sign, never resampled: a shipped set 1:1, or a set DRAWN at the needed width by the
              sign's own builder (logo_native.py)

Each asset is drawn on its own native canvas (output / density, a whole number), its edge texels against the
sunset get one dark outline texel, the canvas is snapped to one palette (near-duplicate inks merged), then it is
enlarged with NEAREST; the title goes on last at full size.

Numbers come from the research and critique passes (Docs/STEAM_SAYFASI.md): logo widths and slots, Roxy's face
on the right third, the sun behind her, the logo on the darkest calm band with nothing crossing it, at most three
genre props, the small capsule logo-first, stacked verticals, the hero's face in the 860x380 safe area with a
calm bottom-left, densities 2/4 where Steam serves half-size copies (main 3, hero 3, so Roxy can stand large).

    python3 keyart.py [asset ...]   ->  out/keyart/<asset>.png  (+ out/keyart/_layout/*.png wireframes)
"""
import json, math, os, sys
import numpy as np
from PIL import Image, ImageDraw

import plate as PL
import scene as S
import props as PR
from logo_render import render as logo_render

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'out', 'keyart')
LAYERS = os.path.join(HERE, 'out', 'layers')
LOGO_SETS = [('x1', 560), ('x1_25', 700), ('x1_5', 840), ('x2', 1120), ('x2_5', 1400), ('x3', 1680), ('x4', 2240)]
HAND_X = 49            # Roxy layer: columns left of this are her resting arm and hand


def hx(s):
    return PL.hx(s)


# ── the counter ───────────────────────────────────────────────────────────────────────────────────────
def counter(W, H, top, seed=2, plain_below=None):
    """The bar: `top` rows of ClubBlue marble seen from above (far edge Graphite[2], long meandering veins in
    ClubBlue[1], a few ClubBlue[2] glints), the magenta neon lip (3 rows), then the front face. A tall front
    gets 12-texel flutes (Night[3] highlight, Night[0] shadow) and ends on the kit's neon rule, inset 6.
    plain_below: rows from here down stay flat #12081A (under Steam's play bar on the hero)."""
    rng = np.random.RandomState(seed)
    a = np.zeros((H, W, 4), np.uint8)
    a[..., 3] = 255
    a[:top, :, :3] = hx('#131B3D')
    for _ in range(max(1, int(W / 100 * 2.5))):                 # veins: meandering polylines
        x, y = rng.randint(0, W), rng.randint(1, max(2, top))
        for _ in range(rng.randint(12, 40)):
            if 1 <= y < top and 0 <= x < W:
                a[y, x, :3] = hx('#1F2E66')
            x += 1
            if rng.rand() < 0.3:
                y += rng.choice((-1, 1))
    for _ in range(max(1, W // 60)):
        x, y = rng.randint(0, W), rng.randint(1, max(2, top))
        if y < top:
            a[y, x, :3] = hx('#2E4699')
    a[0, :, :3] = hx('#383D45')
    # the lip's light spills up onto the slab: Magenta[1] at the sign's alpha steps
    for k, al in enumerate((150, 78, 34)):
        y = top - 1 - k
        if 1 <= y < top:
            a[y, :, :3] = (a[y, :, :3] * (1 - al / 255) + np.array(hx('#8F2464')) * (al / 255)).astype(np.uint8)
    for k, c in enumerate(('#E84DA6', '#FF7DC6', '#C23283')):
        if top + k < H:
            a[top + k, :, :3] = hx(c)
    y0 = top + 3
    a[y0:, :, :3] = hx('#1A1023')
    if H - y0 > 24:
        for x in range(0, W, 12):
            a[y0 + 2:H - 10, x, :3] = hx('#362447')
            a[y0 + 2:H - 10, (x + 11) % W, :3] = hx('#0D0813')
        r = H - 7
        for k, c in enumerate(('#C03382', '#FF7CC7', '#C03382')):
            a[r + k, 6:W - 6, :3] = hx(c)
        a[H - 3:, :, :3] = hx('#12081A')
    if plain_below is not None and plain_below < H:
        a[plain_below:, :, :3] = hx('#12081A')
    return Image.fromarray(a, 'RGBA')


def lip_glow(W, H, y):
    """The lip's light below it, onto the front: the four alpha bands, one native row each."""
    m = np.zeros((H, W), bool)
    m[y:y + 2] = True
    g = np.asarray(S.glow(m, S.C('Magenta[3]'), step=1)).copy()
    g[:y + 2] = 0
    return Image.fromarray(g, 'RGBA')


# ── the title ─────────────────────────────────────────────────────────────────────────────────────────
def title(width):
    """The sign at `width` px, never resampled: a shipped set 1:1 when one is that wide, otherwise a set drawn at
    that width by the sign's own builder (four flat glow bands, aliased tubes, 7 alpha levels)."""
    for name, w in LOGO_SETS:
        if w == width:
            return logo_render(name)
    import logo_native
    return logo_native.render(width)


# ── the layouts ───────────────────────────────────────────────────────────────────────────────────────
# size: output px; d: native density; plate: horizon, night share, sun x / r (native), skyline spans and height,
# palms (side, x anchor, scale[, crown top as a fraction of H]); counter_y: top of the slab (fraction of H);
# top: slab depth in native rows; roxy: face x (fraction) - she always stands at the counter; title: (x, y, width
# px) with x None = centred; props: 'hand' puts the licence under her fingertips and the highball beside it, or
# (('highball', x),) adds one at a fraction of the width.
LAYOUTS = {
    'main_capsule': dict(size=(1232, 706), d=3, horizon=0.56, night=0.52, sun_x=0.66, sun_r=75,
                         skyline=((0.0, 0.46),), skyline_h=48, palms=(('right', 1.03, 1.4),),
                         counter_y=0.86, top=8, roxy=0.66, title=(0.075, 0.04, 560), props='hand'),
    'header_capsule': dict(size=(920, 430), d=2, horizon=0.70, night=0.78, sun_x=0.79, sun_r=64,
                           skyline=((0.0, 0.50),), skyline_h=28, palms=(),
                           counter_y=0.91, top=6, roxy=0.79, title=(0.035, 0.04, 560), props='hand'),
    'small_capsule': dict(size=(462, 174), d=2, horizon=0.95, night=0.97, sun_x=1.02, sun_r=22,
                          skyline=(), palms=(('left', -0.06, 0.7), ('right', 1.06, 0.7)),
                          counter_y=None, roxy=None, title=(None, 0.02, 388), props=None),
    'vertical_capsule': dict(size=(748, 896), d=2, horizon=0.56, night=0.55, sun_x=0.5, sun_r=66,
                             skyline=((0.0, 0.24), (0.76, 1.0)),
                             palms=(('left', -0.04, 1.0, 0.33), ('right', 1.04, 1.0, 0.33)),
                             counter_y=0.785, top=10, roxy=0.5, title=(None, 0.05, 560), props='hand'),
    'library_capsule': dict(size=(600, 900), d=2, horizon=0.58, night=0.55, sun_x=0.5, sun_r=60,
                            skyline=((0.0, 0.20), (0.80, 1.0)),
                            palms=(('left', -0.06, 1.0, 0.30), ('right', 1.06, 1.0, 0.30)),
                            counter_y=0.80, top=10, roxy=0.56, title=(None, 0.04, 560), props='hand'),
    'library_hero': dict(size=(3840, 1240), d=3, horizon=0.52, night=0.42, sun_x=0.50, sun_r=80,
                         skyline=((0.02, 0.36),), palms=(('right', 1.0, 2.0),),
                         counter_y=0.775, top=6, roxy=0.50, title=None, props='hand', plain_below=1017),
    'library_header': None,      # = header_capsule
    'event_cover': dict(size=(800, 450), d=2, horizon=0.74, night=0.70, sun_x=0.27, sun_r=64,
                        skyline=((0.55, 1.0),), skyline_h=32, palms=(('left', 0.0, 1.0),),
                        counter_y=0.95, top=4, roxy=0.27, title=None, props=None),
    'event_header': dict(size=(1920, 622), d=2, horizon=0.58, night=0.68, sun_x=0.74, sun_r=70,
                         skyline=((0.34, 0.70),), palms=(('right', 1.0, 1.3),),
                         counter_y=0.95, top=6, roxy=0.74, title=(0.03, 0.06, 560), props='hand'),
    'community_icon': 'icon',
    'client_icon': 'icon',
}


def roxy_layer():
    img = Image.open(os.path.join(LAYERS, 'roxy_coupe.png')).convert('RGBA')
    meta = json.load(open(os.path.join(LAYERS, 'roxy_coupe.json')))
    return img, meta


def sunset_outline(canvas, plate_img, rx, ry, rox, horizon_row):
    """Roxy's OUTER silhouette texels against the sunset get one opaque dark texel (Magenta[0]) - her skin is
    only a step from the orange band - and a hair texel against the night bands gets Magenta[1]. No blending,
    nothing below the horizon."""
    W, H = canvas.size
    a = np.asarray(canvas).copy()
    pl = np.asarray(plate_img).astype(int)
    m = np.zeros((H, W), bool)
    ra = np.asarray(rox)[..., 3] > 0
    x0, y0 = max(0, rx), max(0, ry)
    x1, y1 = min(W, rx + rox.width), min(H, ry + rox.height)
    m[y0:y1, x0:x1] = ra[y0 - ry:y1 - ry, x0 - rx:x1 - rx]
    warm = {hx(c) for c in ('#FEEC7B', '#FEB555', '#FFB457', '#FF8C56', '#FFCD61', '#FEEF7E', '#FFB356',
                            '#FE8D4F', '#FF8958', '#F35389', '#EA4AA0', '#E74BA2', '#AE489A')}
    for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
        nb = np.zeros_like(m)
        if dy:
            nb[max(0, -dy):H - max(0, dy)] = m[max(0, dy):H - max(0, -dy)]
        else:
            nb[:, max(0, -dx):W - max(0, dx)] = m[:, max(0, dx):W - max(0, -dx)]
        edge = m & ~nb                                   # Roxy texels whose neighbour (y+dy, x+dx) is not Roxy
        ys, xs = np.nonzero(edge)
        oy, ox = ys + dy, xs + dx
        ok = (oy >= 0) & (oy < H) & (ox >= 0) & (ox < W) & (ys < horizon_row)
        ys, xs, oy, ox = ys[ok], xs[ok], oy[ok], ox[ok]
        out = pl[oy, ox, :3]
        is_warm = np.array([tuple(c) in warm for c in out]) if len(out) else np.zeros(0, bool)
        a[ys[is_warm], xs[is_warm], :3] = hx('#5C1B45')
        mine = a[ys, xs, :3].astype(int)
        hair = (mine[:, 0] > mine[:, 1] + 20) & ((mine @ np.array([.299, .587, .114])) < 110)
        dark = (out @ np.array([.299, .587, .114])) < 100
        sel = ~is_warm & hair & dark
        a[ys[sel], xs[sel], :3] = hx('#8F2464')
    return Image.fromarray(a, 'RGBA')


def unify_palette(img, de=4.0):
    """One palette per picture: merge every pair of inks closer than `de` (CIE76, Lab) into the more-used one."""
    import pixelsnap as PS
    a = np.asarray(img.convert('RGB'))
    cols, counts = np.unique(a.reshape(-1, 3), axis=0, return_counts=True)
    order = np.argsort(-counts)
    cols, counts = cols[order], counts[order]
    lab = PS._lab(cols.astype(np.float32))
    target = np.arange(len(cols))
    keep = []
    for i in range(len(cols)):
        if keep:
            d = ((lab[keep] - lab[i]) ** 2).sum(-1)
            j = int(np.argmin(d))
            if d[j] < de * de:
                target[i] = keep[j]
                continue
        keep.append(i)
    lut = {tuple(cols[i]): tuple(cols[target[i]]) for i in range(len(cols)) if target[i] != i}
    if lut:
        flat = a.reshape(-1, 3).copy()
        keyv = flat[:, 0].astype(np.int64) << 16 | flat[:, 1].astype(np.int64) << 8 | flat[:, 2]
        for src, dst in lut.items():
            k = src[0] << 16 | src[1] << 8 | src[2]
            flat[keyv == k] = dst
        a = flat.reshape(a.shape)
    return Image.fromarray(a.astype(np.uint8), 'RGB').convert('RGBA'), len(keep)


def icon(name):
    """Client icon: the logo's coupe from the shipped x2_5 set 1:1, outer two glow bands dropped, on a Night[0]
    tile with the sunset behind the bowl's lower third. Community icon: Roxy's face, 46x46 native, x4."""
    size = 184 if name == 'community_icon' else 256
    if name == 'community_icon':
        rox, meta = roxy_layer()
        n = 92
        img = PL.render(n, n, horizon=0.78, night=0.30, sun_x=0.5, sun_r=30, skyline_spans=(), palms=())
        fx, fy = meta['face']
        plate_only = img.copy()
        rx, ry = n // 2 - fx, n // 2 - fy + 6
        img.alpha_composite(rox, (rx, ry))
        img = sunset_outline(img, plate_only, rx, ry, rox, int(n * 0.78))
        return img.resize((n * 2, n * 2), Image.NEAREST).crop((0, 0, size, size)).convert('RGB')
    m = np.asarray(Image.open(os.path.join(PL.HERE, '..', '..', 'Assets', 'Resources', 'Logo',
                                           'logo_x2_5_map.bytes')).convert('RGB')).copy()
    cls, grp = m[..., 0], m[..., 1]
    coupe = (grp == 12) | (cls == 8)
    m[~coupe] = 0
    m[(cls == 1) | (cls == 2)] = 0                     # the two outer glow bands: the tube fills the tile
    from logo_render import render_map
    cp = render_map(m)
    cp = cp.crop(cp.getbbox())
    n = 64
    tile = PL.render(n, n, horizon=0.80, night=0.62, sun_x=0.5, sun_r=14, skyline_spans=(), palms=(),
                     reflect=False).resize((256, 256), Image.NEAREST)
    k = min(220 / cp.width, 220 / cp.height, 1.0)
    if k < 1.0:
        n2 = None
    tile.alpha_composite(cp, ((256 - cp.width) // 2, (256 - cp.height) // 2))
    return tile.convert('RGB')


def build(name):
    L = LAYOUTS[name] or LAYOUTS['header_capsule']
    if L == 'icon':
        return icon(name), {}
    W, H = L['size']
    d = L['d']
    w, h = math.ceil(W / d), math.ceil(H / d)
    hz = int(round(h * L['horizon']))
    img = PL.render(w, h, horizon=L['horizon'], night=L['night'], sun_x=L['sun_x'], sun_r=L['sun_r'],
                    skyline_spans=L['skyline'], palms=L['palms'],
                    skyline_h=L.get('skyline_h') or max(10, int(h * L['horizon'] * 0.42)))
    plate_only = img.copy()
    boxes = {}
    rox, meta = roxy_layer()
    cy = int(round(h * L['counter_y'])) if L['counter_y'] else None
    if cy is not None:
        top = L.get('top', 6)
        pb = (L['plain_below'] // d - cy) if L.get('plain_below') else None
        ctr = counter(w, h - cy, top, plain_below=pb)
        img.alpha_composite(ctr, (0, cy))
        rx = ry = None
        if L['roxy'] is not None:
            face_x, face_y = meta['face']
            rx = int(round(w * L['roxy'] - face_x))
            ry = cy + top - 3 - meta['counter_y']           # her fingers on the marble, 3 rows from the lip
            img.alpha_composite(rox.crop((max(0, -rx), max(0, -ry), rox.width, rox.height)), (max(0, rx), max(0, ry)))
            img = sunset_outline(img, plate_only, rx, ry, rox, hz)
            # the slab passes in front of her waist (torso columns only, so the hand stays on top), lip and front
            sl = ctr.crop((max(0, rx + HAND_X), 0, min(w, rx + rox.width), top))
            img.alpha_composite(sl, (max(0, rx + HAND_X), cy))
            boxes['roxy'] = (rx * d, ry * d, (rx + rox.width) * d, (ry + rox.height) * d)
            a = np.asarray(img).copy()                       # her contact on the marble
            hx0, hx1 = max(0, rx), max(0, min(w, rx + 34))
            a[cy + top - 2, hx0:hx1, :3] = hx('#0D0813')
            img = Image.fromarray(a, 'RGBA')
        img.alpha_composite(ctr.crop((0, top, w, h - cy)), (0, cy + top))
        img.alpha_composite(lip_glow(w, h, cy + top))
        if L['props']:
            face = Image.open(os.path.join(S.RES, 'Patron', 'clubgirl', 'face.png'))
            card = PR.id_card(face=face)
            hb = PR.highball()
            base = cy + top - 2                               # props stand on the marble
            if L['props'] == 'hand' and rx is not None:
                cx = rx + 6 - card.width                       # her fingertips overlap the card's right edge
                bx = cx - hb.width - 2
            else:
                cx, bx = int(w * 0.3), int(w * 0.24)
            img.alpha_composite(hb, (bx, base - hb.height + 1))
            img.alpha_composite(card, (cx, base - card.height + 1))
            if rx is not None:                                 # her fingers back over the card's edge
                fing = rox.crop((0, rox.height - 12, 10, rox.height))
                img.alpha_composite(fing, (rx, ry + rox.height - 12))
            a = np.asarray(img).copy()
            a[base + 1, max(0, bx):max(0, bx + hb.width), :3] = hx('#0D0813')
            a[base + 1, max(0, cx):max(0, cx + card.width), :3] = hx('#0D0813')
            img = Image.fromarray(a, 'RGBA')
        boxes['counter'] = (0, cy * d, W, H)
    img, inks = unify_palette(img)
    boxes['_inks'] = inks
    big = img.resize((w * d, h * d), Image.NEAREST).crop((0, 0, W, H))
    if L['title']:
        tx, ty, tw = L['title']
        lg = title(tw)
        x = int(round(W * tx)) if tx is not None else (W - lg.width) // 2
        y = int(round(H * ty))
        big.alpha_composite(lg, (x, y))
        boxes['title'] = (x, y, x + lg.width, y + lg.height)
    return big.convert('RGB'), boxes


def library_logo():
    """The library logo: the sign and its halo trimmed tight, 8 px of clear edge (Steam anchors the whole box,
    so empty padding would shrink the logo and pull it off its anchor), 1280 wide."""
    lg = logo_render('x2')          # the shipped x2 set 1:1 (Steam scales the library logo itself)
    a = np.asarray(lg)
    ys, xs = np.nonzero(a[..., 3] > 0)
    lg = lg.crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
    out = Image.new('RGBA', (1280, lg.height + 16), (0, 0, 0, 0))
    out.alpha_composite(lg, ((1280 - lg.width) // 2, 8))
    return out


def layout_sheet(names):
    """Every asset's wireframe on one sheet, scaled to a common height per row, labelled with its slot."""
    tiles = [Image.open(os.path.join(OUT, '_layout', n + '.png')) for n in names]
    W = 2400
    rows, row, x = [], [], 0
    for n, t in zip(names, tiles):
        h = 360 if t.width / t.height > 1.2 else 520
        t2 = t.resize((int(t.width * h / t.height), h), Image.LANCZOS)
        if x + t2.width > W and row:
            rows.append(row); row, x = [], 0
        row.append(t2); x += t2.width + 24
    rows.append(row)
    H = sum(max(t.height for t in r) + 24 for r in rows) + 24
    sheet = Image.new('RGB', (W, H), (16, 10, 24))
    y = 24
    for r in rows:
        x = 24
        for t in r:
            sheet.paste(t, (x, y)); x += t.width + 24
        y += max(t.height for t in r) + 24
    return sheet


# Steam's own zones per asset (research pass): what Steam covers, crops or reserves
ZONES = {
    'main_capsule': [('EDGE 5%', (0, 0, 0.05, 1.0)), ('EDGE 5%', (0.95, 0, 1.0, 1.0))],
    'vertical_capsule': [('KEEP CLEAR 8%', (0, 0.92, 1.0, 1.0))],
    'library_capsule': [('CLIENT OVERLAYS 10%', (0, 0.90, 1.0, 1.0))],
    'library_hero': [('SAFE AREA 860x380', (1490 / 3840, 430 / 1240, 2350 / 3840, 810 / 1240)),
                     ('STEAM LOGO (BOTTOM-LEFT)', (0.02, 0.45, 0.45, 0.80)),
                     ('PLAY BAR', (0, 0.82, 1.0, 1.0))],
    'event_cover': [('EVENT TEXT', (0.53, 0.06, 0.96, 0.50))],
}


def wireframe(name, img, boxes):
    """The layout drawn over a dimmed render: title, Roxy and counter boxes, Steam's zones, the third lines."""
    from PIL import ImageFont
    W, H = img.size
    wf = Image.blend(img, Image.new('RGB', img.size, (13, 8, 19)), 0.55).convert('RGBA')
    dr = ImageDraw.Draw(wf)
    fs = max(10, min(W, H) // 22)
    font = ImageFont.truetype(S.FONT, fs)
    for label_, (x0, y0, x1, y1) in ZONES.get(name, []):
        box = (int(x0 * W), int(y0 * H), int(x1 * W) - 1, int(y1 * H) - 1)
        dr.rectangle(box, outline=(245, 201, 123, 255), width=max(2, W // 500))
        dr.text((box[0] + 6, box[3] - fs - 6), label_, fill=(245, 201, 123, 255), font=font)
    for i in (1, 2):
        dr.line([(W * i // 3, 0), (W * i // 3, H)], fill=(110, 147, 240, 160), width=1)
        dr.line([(0, H * i // 3), (W, H * i // 3)], fill=(110, 147, 240, 160), width=1)
    col = {'title': (125, 240, 227), 'roxy': (255, 125, 198), 'counter': (110, 147, 240)}
    for k, b in boxes.items():
        if k.startswith('_'):
            continue
        dr.rectangle(b, outline=col[k] + (255,), width=max(2, W // 400))
        dr.text((b[0] + 6, b[1] + 4), {'title': 'LOGO', 'roxy': 'ROXY', 'counter': 'BAR'}[k],
                fill=col[k] + (255,), font=font)
    dr.text((8, 6), '%s  %dx%d' % (name, W, H), fill=(242, 232, 213, 255), font=font)
    return wf.convert('RGB')


def client_ico():
    """The shortcut icon as one .ico: the 256 tile, plus small frames drawn from the shipped x1 coupe 1:1 (its
    tube and core inks, no glow) on Night[0], so 16-48 px are pixels, not a blur of the big one."""
    big = icon('client_icon')
    m = np.asarray(Image.open(os.path.join(PL.HERE, '..', '..', 'Assets', 'Resources', 'Logo',
                                           'logo_x1_map.bytes')).convert('RGB')).copy()
    cls, grp = m[..., 0], m[..., 1]
    keep = ((grp == 12) & (cls >= 5)) | (cls == 8)
    m[~keep] = 0
    from logo_render import render_map
    cp = render_map(m)
    cp = cp.crop(cp.getbbox())
    frames = []
    for n in (16, 24, 32, 48):
        t = Image.new('RGBA', (n, n), hx('#0D0813') + (255,))
        c = cp
        if max(c.size) > n - 2:                      # the x1 coupe is ~40 px: fit by whole-pixel decimation
            k = math.ceil(max(c.size) / (n - 2))
            c = c.resize((c.width // k, c.height // k), Image.NEAREST)
        t.alpha_composite(c, ((n - c.width) // 2, (n - c.height) // 2))
        frames.append(t)
    big.convert('RGBA').save(os.path.join(OUT, 'client_icon.ico'), sizes=[(256, 256), (48, 48), (32, 32),
                                                                         (24, 24), (16, 16)],
                             append_images=frames)


def main(names):
    os.makedirs(os.path.join(OUT, '_layout'), exist_ok=True)
    library_logo().save(os.path.join(OUT, 'library_logo.png'), optimize=True)
    try:
        client_ico()
    except Exception as e:                           # an .ico is a convenience; the PNG is what Steam takes
        print('client_icon.ico skipped:', e)
    for n in names:
        img, boxes = build(n)
        img.save(os.path.join(OUT, n + '.png'), optimize=True)
        wireframe(n, img, boxes).save(os.path.join(OUT, '_layout', n + '.png'))
        print('%-18s %dx%d  %s inks' % (n, img.width, img.height, boxes.get('_inks', '-')),
              {k: tuple(int(v) for v in b) for k, b in boxes.items() if not k.startswith('_')})
    if len(names) > 3:
        drawn = [n for n in names if os.path.exists(os.path.join(OUT, '_layout', n + '.png'))]
        layout_sheet(drawn).save(os.path.join(OUT, '_layout', 'LAYOUT_SHEET.png'), optimize=True)


if __name__ == '__main__':
    main(sys.argv[1:] or list(LAYOUTS))
