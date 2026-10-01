# -*- coding: utf-8 -*-
"""The key art's three props, drawn as native pixel art at Roxy's scale (her coupe is ~50 px, a highball ~40):
a highball with ice, straw and lime, and the patron licence standing on the bar - the game's hook. Inks are the
game's UITheme ramps; a one-texel dark outline (Night[0]) rings each so they read at thumbnail size."""
import numpy as np
from PIL import Image
import plate as PL

hx = PL.hx


def _outline(a, ink='#0D0813'):
    m = a[..., 3] > 0
    o = np.zeros_like(m)
    o[1:] |= m[:-1]; o[:-1] |= m[1:]; o[:, 1:] |= m[:, :-1]; o[:, :-1] |= m[:, 1:]
    o &= ~m
    a[o] = hx(ink) + (255,)
    return a


def highball(h=40, w=15, liquid=('#E84DA6', '#FF7DC6', '#C23283')):
    """A tall glass: cream-cyan rim and walls, a magenta drink with two ice cubes, a cyan straw, a lime wheel."""
    W, H = w + 10, h + 8
    a = np.zeros((H, W, 4), np.uint8)
    x0, x1, y0, y1 = 3, 3 + w, 6, 6 + h
    # liquid
    for y in range(y0 + 5, y1 - 2):
        a[y, x0 + 1:x1 - 1] = hx(liquid[0]) + (255,)
    a[y0 + 5, x0 + 1:x1 - 1] = hx(liquid[1]) + (255,)
    a[y0 + 6:y1 - 2, x0 + 2] = hx(liquid[1]) + (255,)                       # light down the near side
    a[y0 + 6:y1 - 2, x1 - 3] = hx(liquid[2]) + (255,)
    for cy, cx in ((y0 + 7, x0 + 3), (y0 + 13, x0 + 7)):                    # ice
        a[cy:cy + 4, cx:cx + 4] = hx('#C9BCA8') + (255,)
        a[cy, cx:cx + 4] = hx('#F2E8D5') + (255,)
    # glass walls, base and rim
    a[y0:y1, x0] = hx('#7DF0E3') + (255,)
    a[y0:y1, x1 - 1] = hx('#3BC8BE') + (255,)
    a[y1 - 2:y1, x0:x1] = hx('#C9BCA8') + (255,)
    a[y0, x0:x1] = hx('#F2E8D5') + (255,)
    # straw
    for k in range(9):
        a[y0 - 5 + k, x1 - 4 + (k // 4)] = hx('#7DF0E3') + (255,)
    # lime wheel on the rim
    a[y0 - 2:y0 + 2, x0 - 2:x0 + 3] = hx('#8FD14F') + (255,)
    a[y0 - 1:y0 + 1, x0 - 1:x0 + 2] = hx('#E9F5A0') + (255,)
    return Image.fromarray(_outline(a), 'RGBA')


def id_card(w=36, h=24, face=None):
    """The patron licence, upright: cream card, the licence band (sunset orange, a palm), a photo, three lines."""
    a = np.zeros((h + 2, w + 2, 4), np.uint8)
    a[1:h + 1, 1:w + 1] = hx('#F2E8D5') + (255,)
    a[1:6, 1:w + 1] = hx('#E8A33D') + (255,)
    a[1:3, 1:w + 1] = hx('#F5C97B') + (255,)
    a[5, 1:w + 1] = hx('#C9822B') + (255,)
    a[8:20, 3:13] = hx('#9C8F80') + (255,)                                   # photo
    if face is not None:
        f = face.convert('RGBA').resize((10, 12), Image.NEAREST)
        a[8:20, 3:13] = np.asarray(f)
    else:
        a[10:15, 6:10] = hx('#C9822B') + (255,)
        a[15:20, 4:12] = hx('#8F2464') + (255,)
    for y, L in ((9, 18), (13, 14), (17, 16)):
        a[y, 15:15 + L] = hx('#6E6459') + (255,)
    a[h, 1:w + 1] = hx('#C9BCA8') + (255,)
    return Image.fromarray(_outline(a), 'RGBA')


def coupe_mark(size):
    """The logo's own neon coupe (map group 12 + its liquid), on Night[0], for the small icons."""
    from logo_render import render
    import os
    from PIL import Image as I
    m = np.asarray(I.open(os.path.join(PL.HERE, '..', '..', 'Assets', 'Resources', 'Logo', 'logo_x4_map.bytes'))
                   .convert('RGB'))
    grp = m[..., 1]
    full = np.asarray(render('x4'))
    keep = (grp == 12) | ((m[..., 0] == 8))
    ys, xs = np.nonzero(keep)
    pad = 40
    box = (max(0, xs.min() - pad), max(0, ys.min() - pad), xs.max() + pad, ys.max() + pad)
    out = full.copy()
    out[~keep] = 0
    # keep the halo around the coupe: anything within the box whose group is 12 or 0 (halo has group 0? keep all
    # non-letter texels near the coupe)
    near = np.zeros_like(keep)
    near[box[1]:box[3], box[0]:box[2]] = True
    halo = near & (grp == 0) & (m[..., 0] > 0) & (m[..., 0] <= 4)
    out[halo] = full[halo]
    crop = I.fromarray(out, 'RGBA').crop(box)
    bg = I.new('RGBA', (size, size), hx('#0D0813') + (255,))
    k = (size * 0.86) / max(crop.size)
    c2 = crop.resize((max(1, int(crop.width * k)), max(1, int(crop.height * k))), I.BOX)
    bg.alpha_composite(c2, ((size - c2.width) // 2, (size - c2.height) // 2))
    return bg
