# -*- coding: utf-8 -*-
"""THE KEY ART: every store and library picture laid out from ONE master composition.

The master is a beach bar opening onto the steam_kit sunset (plate.py), seen from behind its counter:

    plate   - the page background's own sunset, sea, skyline and palms, with dusk bands above for the title
    counter - black marble bar top with the magenta neon lip of the steam_kit panels, a few of the game's bottles
    Roxy    - the one cut-out (roxy_layer.py), her hand resting on that counter, a coupe raised, eyes on the viewer
    title   - the shipped MALIBU CLUB sign (logo_render.py), placed at final resolution, never redrawn

Each asset is drawn on its own NATIVE canvas (output size / its pixel density, a whole number) and enlarged with
NEAREST, so inside one picture every layer shares one grid and one palette. The title is laid on at full size:
the largest shipped set that fits is used 1:1 (no resampling); only where none fits is the next set reduced.

    python3 keyart.py [asset ...]   ->  out/keyart/<asset>.png  (+ out/keyart/_layout/<asset>.png wireframes)
"""
import json, math, os, sys
import numpy as np
from PIL import Image, ImageDraw

import plate as PL
import scene as S
from logo_render import render as logo_render

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'out', 'keyart')
LAYERS = os.path.join(HERE, 'out', 'layers')
LOGO_SETS = [('x1', 560), ('x1_25', 700), ('x1_5', 840), ('x2', 1120), ('x2_5', 1400), ('x3', 1680), ('x4', 2240)]


def hx(s):
    return PL.hx(s)


# ── the counter ───────────────────────────────────────────────────────────────────────────────────────
def counter(W, H, seed=2):
    """Black marble top (veins in the Night ramp), a magenta neon lip with the title sign's four-band light, and
    the front face falling to near-black."""
    rng = np.random.RandomState(seed)
    a = np.zeros((H, W, 4), np.uint8)
    a[..., 3] = 255
    top = max(1, min(max(3, H // 6), H - 4))   # depth of the bar top seen from above (a sliver on a low bar)
    a[:top, :, :3] = hx('#14161A')
    a[0, :, :3] = hx('#383D45')                # far edge catches the sky
    for _ in range(max(2, W // 18)):           # marble veins: short diagonal runs
        x = rng.randint(0, W); y = rng.randint(1, max(2, top - 1)); L = rng.randint(4, 14)
        ink = hx('#24272D') if rng.rand() < 0.7 else hx('#545A64')
        for k in range(L):
            xx, yy = x + k, y + (k // 4)
            if 0 <= xx < W and 1 <= yy < top:
                a[yy, xx, :3] = ink
    # the neon lip
    a[top, :, :3] = hx('#E84DA6')
    a[top + 1, :, :3] = hx('#FF7DC6')
    a[top + 2, :, :3] = hx('#C23283')
    # front face
    face = PL.profile(max(1, H - top - 3), [('#1A1023', 3, 'band'), ('#241830', 1, 'seam'), ('#1C1226', 1, 'seam'),
                                    ('#120A19', 6, 'band')])
    for i, c in enumerate(face):
        if top + 3 + i < H:
            a[top + 3 + i, :, :3] = c
    fh = H - top - 3
    if fh > 24:
        # a MiMo bar front: a brass rail, fluted panels, a cyan kick light along the floor
        y0 = top + 3
        a[y0 + 4, :, :3] = hx('#8F5A1E'); a[y0 + 5, :, :3] = hx('#C9822B'); a[y0 + 6, :, :3] = hx('#4A2E14')
        for x in range(0, W, 6):
            a[y0 + 9:H - 6, x, :3] = hx('#241830')
            a[y0 + 9:H - 6, (x + 1) % W, :3] = hx('#1F1329')
        a[H - 5, :, :3] = hx('#1B5F66'); a[H - 4, :, :3] = hx('#3BC8BE'); a[H - 3, :, :3] = hx('#1B5F66')
    img = Image.fromarray(a, 'RGBA')
    return img, top


def lip_glow(W, H, y):
    """The lip's light on whatever is above and below it: the four alpha bands, one native row each."""
    m = np.zeros((H, W), bool)
    m[y:y + 2] = True
    return S.glow(m, S.C('Magenta[3]'), step=1)


def bottles(cards):
    return [S.bottle(c, f, cellar=True) for c, f in cards]


# ── the title ─────────────────────────────────────────────────────────────────────────────────────────
def title(width):
    """The sign at `width` px: the largest shipped set not wider (1:1) if it is within 6% of the target,
    otherwise the next set up reduced with a box filter. Never enlarged."""
    best = None
    for name, w in LOGO_SETS:
        if w <= width:
            best = (name, w)
    if best and best[1] >= width * 0.94:
        return logo_render(best[0])
    for name, w in LOGO_SETS:
        if w >= width:
            im = logo_render(name)
            return im.resize((width, round(im.height * width / im.width)), Image.BOX)
    im = logo_render('x4')
    return im.resize((width, round(im.height * width / im.width)), Image.BOX)


# ── the layouts ───────────────────────────────────────────────────────────────────────────────────────
# size: output px. d: native pixel density. All boxes are fractions of the OUTPUT canvas.
#   plate: horizon, night share, sun x, sun r (native px), skyline spans, palms (side, x, scale)
#   counter_y: top of the bar top (fraction of height), or None for no counter
#   roxy: (face x fraction, mode) - mode 'counter' rests her hand on the counter; ('head', y) puts her face at y
#   title: (x, y, width) fractions; x None = centred
#   props: [(x fraction, card, fill)] bottles on the bar top
# Numbers from the research pass (Tools/steam_page/research/, 2026-10-01): logo widths and slots, Roxy's face
# position, the sun behind her, a calm night field behind the title, at most three genre props (the counter edge,
# a highball, the ID card), the hero's 860x380 safe area and a calm bottom-left for Steam's own logo.
# Densities: 2 (or 4) wherever Steam serves a half-size copy, so that copy stays on whole pixels; the main
# capsule takes 3 so Roxy stands crown-to-counter in ~86% of its height.
LAYOUTS = {
    'main_capsule': dict(size=(1232, 706), d=3, horizon=0.56, night=0.52, sun_x=0.66, sun_r=60,
                         skyline=((0.0, 0.46),), palms=(('right', 1.0, 1.4),),
                         counter_y=0.90, roxy=(0.66, 'counter'), title=(0.05, 0.05, 0.455),
                         props=[('highball', 0.23), ('card', 0.31)]),
    'header_capsule': dict(size=(920, 430), d=2, horizon=0.60, night=0.52, sun_x=0.76, sun_r=52,
                           skyline=((0.0, 0.50),), palms=(('right', 1.0, 1.2),),
                           counter_y=0.95, roxy=(0.76, 'counter'), title=(0.045, 0.12, 0.50),
                           props=[('highball', 0.36), ('card', 0.43)]),
    'small_capsule': dict(size=(462, 174), d=2, horizon=0.70, night=0.62, sun_x=1.02, sun_r=22,
                          skyline=(), palms=(('left', -0.06, 0.7), ('right', 1.06, 0.7)),
                          counter_y=None, roxy=None, title=(None, 'middle', 0.84), props=[]),
    'vertical_capsule': dict(size=(748, 896), d=2, horizon=0.56, night=0.55, sun_x=0.5, sun_r=66,
                             skyline=((0.0, 0.24), (0.76, 1.0)), palms=(('left', -0.04, 1.4), ('right', 1.04, 1.4)),
                             counter_y=0.785, roxy=(0.5, 'counter'), title=(None, 0.05, 0.84),
                             props=[('card', 0.04), ('highball', 0.86)]),
    'library_capsule': dict(size=(600, 900), d=2, horizon=0.55, night=0.55, sun_x=0.5, sun_r=60,
                            skyline=((0.0, 0.20), (0.80, 1.0)), palms=(('left', -0.06, 1.3), ('right', 1.06, 1.3)),
                            counter_y=0.73, roxy=(0.5, 'counter'), title=(None, 0.05, 0.85),
                            props=[('card', 0.02), ('highball', 0.86)]),
    'library_hero': dict(size=(3840, 1240), d=4, horizon=0.45, night=0.42, sun_x=0.573, sun_r=44,
                         skyline=((0.02, 0.36), (0.80, 1.0)), palms=(('left', 0.0, 1.5), ('right', 1.0, 1.5)),
                         counter_y=0.962, roxy=(0.573, 'counter'), title=None, props=[]),
    'library_header': None,      # = header_capsule
    'event_cover': dict(size=(800, 450), d=2, horizon=0.58, night=0.45, sun_x=0.22, sun_r=30,
                        skyline=((0.45, 1.0),), palms=(('right', 1.0, 1.1),),
                        counter_y=0.95, roxy=(0.22, 'counter'), title=None, props=[]),
    'event_header': dict(size=(1920, 622), d=2, horizon=0.58, night=0.50, sun_x=0.84, sun_r=40,
                         skyline=((0.0, 0.30), (0.62, 0.74)), palms=(('right', 1.0, 1.3),),
                         counter_y=0.96, roxy=(0.84, 'counter'), title=(0.03, 0.14, 0.30),
                         props=[('highball', 0.66), ('card', 0.70)]),
    'community_icon': 'coupe',
    'client_icon': 'coupe',
}

def roxy_layer():
    img = Image.open(os.path.join(LAYERS, 'roxy_coupe.png')).convert('RGBA')
    meta = json.load(open(os.path.join(LAYERS, 'roxy_coupe.json')))
    return img, meta


def build(name):
    L = LAYOUTS[name] or LAYOUTS['header_capsule']
    if L == 'coupe':
        import props as PR
        size = 184 if name == 'community_icon' else 256
        return PR.coupe_mark(size).convert('RGB'), {}
    W, H = L['size']
    d = L['d']
    w, h = math.ceil(W / d), math.ceil(H / d)
    img = PL.render(w, h, horizon=L['horizon'], night=L['night'], sun_x=L['sun_x'], sun_r=L['sun_r'],
                    skyline_spans=L['skyline'], palms=L['palms'], skyline_h=max(10, int(h * L['horizon'] * 0.42)))
    boxes = {}
    import props as PR
    rox, meta = roxy_layer()
    cy = int(round(h * L['counter_y'])) if L['counter_y'] else None
    top = 0
    if cy is not None:
        ctr, top = counter(w, h - cy)
        img.alpha_composite(ctr, (0, cy))
        img.alpha_composite(lip_glow(w, h, cy + top))
        # the props stand on the bar top
        face = Image.open(os.path.join(S.RES, 'Patron', 'clubgirl', 'face.png'))
        for kind, px in L['props']:
            pr = PR.highball() if kind == 'highball' else PR.id_card(face=face)
            img.alpha_composite(pr, (int(round(w * px)), cy + max(1, top // 2) - pr.height + 1))
    if L['roxy']:
        fx, mode = L['roxy']
        face_x, face_y = meta['face']
        rx = int(round(w * fx - face_x))
        ry = cy - meta['counter_y'] + 1 if mode == 'counter' else int(round(h * mode[1] - face_y))
        # backlit by the sun: a warm rim on one edge, the neon's cyan on the other
        rr = S.rim_light(S.rim_light(rox, S.C('Amber[4]'), 'left', 150), S.C('Cyan[4]'), 'right', 110)
        img.alpha_composite(rr.crop((max(0, -rx), max(0, -ry), rr.width, rr.height)), (max(0, rx), max(0, ry)))
        boxes['roxy'] = (rx * d, ry * d, (rx + rox.width) * d, (ry + rox.height) * d)
    if cy is not None:
        # the counter's front face again over her waist (she stands behind the bar), neon lip on top
        ctr2, top = counter(w, h - cy)
        front = ctr2.crop((0, top, w, h - cy))
        img.alpha_composite(front, (0, cy + top))
    if cy is not None:
        boxes['counter'] = (0, cy * d, W, H)
    big = img.resize((w * d, h * d), Image.NEAREST).crop((0, 0, W, H))
    if L['title']:
        tx, ty, tw = L['title']
        lg = title(int(round(W * tw)))
        x = int(round(W * tx)) if tx is not None else (W - lg.width) // 2
        if ty == 'middle':
            y = (H - lg.height) // 2
        elif ty == 'counter':
            # on the bar's front face: centred in the space under the neon lip
            lip = (cy + top + 3) * d
            lg = title(min(int(round(W * tw)), int((H - lip - 2 * 12) * lg.width / lg.height)))
            x = (W - lg.width) // 2 if tx is None else x
            y = lip + (H - lip - lg.height) // 2
        else:
            y = int(round(H * ty))
        big.alpha_composite(lg, (x, y))
        boxes['title'] = (x, y, x + lg.width, y + lg.height)
    return big.convert('RGB'), boxes


def library_logo():
    """The library logo: the sign and its halo trimmed tight, 8 px of clear edge (Steam anchors the whole box,
    so empty padding would shrink the logo and pull it off its anchor), 1280 wide."""
    lg = title(1264)
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
    'event_cover': [('EVENT TEXT', (0.42, 0.10, 0.96, 0.80))],
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
        dr.rectangle(b, outline=col[k] + (255,), width=max(2, W // 400))
        dr.text((b[0] + 6, b[1] + 4), {'title': 'LOGO', 'roxy': 'ROXY', 'counter': 'BAR'}[k],
                fill=col[k] + (255,), font=font)
    dr.text((8, 6), '%s  %dx%d' % (name, W, H), fill=(242, 232, 213, 255), font=font)
    return wf.convert('RGB')


def main(names):
    os.makedirs(os.path.join(OUT, '_layout'), exist_ok=True)
    library_logo().save(os.path.join(OUT, 'library_logo.png'), optimize=True)
    for n in names:
        img, boxes = build(n)
        img.save(os.path.join(OUT, n + '.png'), optimize=True)
        wireframe(n, img, boxes).save(os.path.join(OUT, '_layout', n + '.png'))
        print('%-18s %dx%d' % (n, img.width, img.height), {k: tuple(int(v) for v in b) for k, b in boxes.items()})
    if len(names) > 3:
        drawn = [n for n in names if os.path.exists(os.path.join(OUT, '_layout', n + '.png'))]
        layout_sheet(drawn).save(os.path.join(OUT, '_layout', 'LAYOUT_SHEET.png'), optimize=True)


if __name__ == '__main__':
    main(sys.argv[1:] or list(LAYOUTS))
