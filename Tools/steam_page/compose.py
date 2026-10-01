# -*- coding: utf-8 -*-
"""Compose the store's key art at OUTPUT resolution from layers that are each enlarged by a whole number:
the room at s=2 (the game's own 640x360 -> 1280x720 grid), the patrons at s=2, Roxy at s=3..5 (she is the
nearest thing to the viewer), the coupe in her hand at half her scale, the title sign 1:1 from its own set."""
import numpy as np
from PIL import Image
import scene as S
from logo_render import render as logo_render

ROXY = 'hostess'


def roxy(s, clip='idle', i=0, glass=True, rim='Cyan[4]', rim_side='left'):
    """Roxy at scale s, a coupe in her left hand (the viewer's right), rim-lit by the cyan neon."""
    f = S.sprite(ROXY, clip, i).crop((50, 0, 170, 216))
    img = S.up(f, s)
    if rim:
        img = S.up(S.rim_light(f, S.C(rim), rim_side), s)
    if glass:
        g = S.up(S.glass('coupe', 'Magenta[3]', 0.8, rim='sugar'), max(1, s // 2))
        # the stem's foot sits in her fist: hand ~ (99..102, 110..118) in the cropped frame
        gx = 99 * s - g.width // 2 + s
        gy = 116 * s - g.height + s * 2
        out = Image.new('RGBA', (max(img.width, gx + g.width), img.height), (0, 0, 0, 0))
        out.alpha_composite(img)
        out.alpha_composite(g, (gx, gy))
        # her fingers back over the stem
        hand = S.up(f.crop((95, 112, 106, 121)), s)
        out.alpha_composite(S.up(S.rim_light(f, S.C(rim), rim_side), s).crop((95 * s, 112 * s, 106 * s, 121 * s)) if rim else hand, (95 * s, 112 * s))
        img = out
    return img


def patrons_row(names, clip='idle', s=2, gap=-40):
    fs = [S.sprite(n, clip) for n in names]
    fs = [f.crop((50, 0, 170, 220)) for f in fs]
    w = sum(f.width for f in fs) + gap * (len(fs) - 1)
    row = Image.new('RGBA', (w, 220), (0, 0, 0, 0))
    x = 0
    for f in fs:
        row.alpha_composite(f, (x, 0)); x += f.width + gap
    return S.up(row, s)


def logo(width):
    """The title sign at the largest shipped set not wider than `width`, 1:1 - never enlarged."""
    sets = [('x1', 560), ('x1_25', 700), ('x1_5', 840), ('x2', 1120), ('x2_5', 1400), ('x3', 1680), ('x4', 2240)]
    pick = sets[0][0]
    for name, w in sets:
        if w <= width:
            pick = name
    return logo_render(pick)


def logo_fit(width):
    """The sign at exactly `width`: the next shipped set above it, reduced (never enlarged) with a box filter."""
    sets = [('x1', 560), ('x1_25', 700), ('x1_5', 840), ('x2', 1120), ('x2_5', 1400), ('x3', 1680), ('x4', 2240)]
    for name, w in sets:
        if w >= width:
            im = logo_render(name)
            break
    else:
        im = logo_render('x4')
    h = round(im.height * width / im.width)
    return im.resize((width, h), Image.BOX)


def room(W, H, s=2, horizon=0.56, sun_x=0.7, sun_r=0.2, patrons=None, patron_x=0.05, counter_y=0.74,
         props=True, seed=3, neon=True, patron_names=('clubgirl', 'guard', 'harajuku', 'leopard')):
    """The bar seen from behind its counter: dusk window, a row of patrons, the counter with drinks."""
    w, h = -(-W // s), -(-H // s)
    hz = int(h * horizon)
    img = S.window_room(w, h, hz, int(w * sun_x), int(h * sun_r), seed=seed)
    if neon:
        img = S.neon_bar(img, 8, 'Magenta[3]', 'Magenta[4]')
    big = S.up(img, s)
    cy = int(H * counter_y)
    if patrons:
        row = patrons_row(patrons, s=1 if s == 1 else 2 * s // 2)
        # patrons stand behind the counter; their waist meets the bar top
        py = cy - int(row.height * 0.55)
        big.alpha_composite(S.tint(row, S.C('Night[2]'), 0.12), (int(W * patron_x), py))
    ctr = S.up(S.counter(w, h - cy // s + 2), s)
    big.alpha_composite(ctr, (0, cy))
    # cyan under-glow on the bar's lip
    m = np.zeros((H, W), bool)
    m[cy + 10 * s: cy + 10 * s + s] = True
    big.alpha_composite(S.glow(m, S.C('Magenta[3]'), step=s * 2))
    return big, cy


def place_props(img, cy, s, xs, kinds):
    for x, k in zip(xs, kinds):
        if k[0] == 'bottle':
            b = S.up(S.bottle(k[1], k[2] if len(k) > 2 else 0.7, cellar=True), s)
        else:
            g = S.glass(k[1], k[2], 0.78, rim=k[3] if len(k) > 3 else None)
            if s >= 2:
                import about
                b = about.half(g)                  # onto the s px grid at the glass's own display size
            else:
                b = g
        img.alpha_composite(b, (x, cy + 6 * s - b.height))
    return img


def paste_glow(img, layer, x, y, colour='Magenta[3]', step=6, glow=True):
    """Paste `layer` at (x, y) with the four-band halo of its silhouette behind it; anything off-canvas is cut."""
    W, H = img.size
    if glow:
        pad = step * 4 + 2
        m = np.zeros((layer.height + 2 * pad, layer.width + 2 * pad), bool)
        m[pad:pad + layer.height, pad:pad + layer.width] = np.asarray(layer)[..., 3] > 0
        g = S.glow(m, S.C(colour), step=step)
        img.alpha_composite(g.crop((max(0, pad - x), max(0, pad - y), g.width, g.height)),
                            (max(0, x - pad), max(0, y - pad)))
    img.alpha_composite(layer.crop((max(0, -x), max(0, -y), layer.width, layer.height)), (max(0, x), max(0, y)))
    return img
