# -*- coding: utf-8 -*-
"""The key art's three props, drawn as native pixel art at Roxy's scale (her coupe is ~50 px, a highball ~40):
a highball with ice, straw and lime, and the patron licence standing on the bar - the game's hook. Inks are the
game's UITheme ramps; a one-texel dark outline (Night[0]) rings each so they read at thumbnail size."""
import numpy as np
from PIL import Image
import plate as PL

hx = PL.hx


def _outline(a, ink='#0D0813', shade=None):
    """A one-texel ring on the EXTERIOR only (the flood-filled outside), so inner gaps never get a black lid.
    shade: an ink for the ring's right/lower half (a selective outline), the rest takes `ink`."""
    m = a[..., 3] > 0
    H, W = m.shape
    outside = np.zeros((H + 2, W + 2), bool)
    pad = np.pad(~m, 1, constant_values=True)
    stack = [(0, 0)]
    outside[0, 0] = True
    while stack:
        y, x = stack.pop()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            yy, xx = y + dy, x + dx
            if 0 <= yy < H + 2 and 0 <= xx < W + 2 and pad[yy, xx] and not outside[yy, xx]:
                outside[yy, xx] = True
                stack.append((yy, xx))
    outside = outside[1:-1, 1:-1]
    ring = np.zeros_like(m)
    ring[1:] |= m[:-1]; ring[:-1] |= m[1:]; ring[:, 1:] |= m[:, :-1]; ring[:, :-1] |= m[:, 1:]
    ring &= outside
    a[ring] = hx(ink) + (255,)
    if shade:
        cx = W / 2
        xs = np.arange(W)[None, :].repeat(H, 0)
        a[ring & (xs > cx)] = hx(shade) + (255,)
    return a


def highball(h=40, w=15):
    """A tequila sunrise in a highball: amber over a magenta foot, a cyan-dark headspace with one glint, ice
    cubes, a Cream rim and base, a cyan straw and a lime wheel in the game's Lime ramp."""
    W, H = w + 10, h + 8
    a = np.zeros((H, W, 4), np.uint8)
    x0, x1, y0, y1 = 3, 3 + w, 6, 6 + h
    a[y0 + 1:y1 - 2, x0 + 1:x1 - 1] = hx('#123B45') + (255,)                    # headspace (Cyan[0])
    a[y0 + 2, x0 + 2] = hx('#3BC8BE') + (255,)                                   # its glint
    top_liq = y0 + 5
    foot = y1 - 2 - int((y1 - 2 - top_liq) * 0.25)
    a[top_liq:foot, x0 + 1:x1 - 1] = hx('#E8A33D') + (255,)                     # amber
    a[top_liq, x0 + 1:x1 - 1] = hx('#F5C97B') + (255,)                          # the surface
    a[foot:y1 - 2, x0 + 1:x1 - 1] = hx('#E84DA6') + (255,)                      # the grenadine foot
    a[foot - 1, x0 + 1:x1 - 1] = hx('#C9822B') + (255,)
    for cy, cx in ((top_liq + 2, x0 + 3), (top_liq + 7, x0 + 8), (top_liq + 12, x0 + 4)):   # ice
        a[cy:cy + 3, cx:cx + 3] = hx('#C9BCA8') + (255,)
        a[cy, cx] = hx('#F2E8D5') + (255,)
    a[y0:y1, x0] = hx('#F2E8D5') + (255,)                                        # walls: lit near side
    a[y0 + 1:y1 - 2, x0 + 1] = hx('#1F2E66') + (255,)
    a[y0:y1, x1 - 1] = hx('#9C8F80') + (255,)
    a[y1 - 2:y1, x0:x1] = hx('#9C8F80') + (255,)                                 # base
    a[y0, x0:x1] = hx('#F2E8D5') + (255,)                                        # rim
    for k in range(9):                                                           # straw
        a[y0 - 5 + k, x1 - 4 + (k // 4)] = hx('#7DF0E3') + (255,)
    a[y0 - 2:y0 + 2, x0 - 2:x0 + 3] = hx('#6FCC4B') + (255,)                     # lime wheel
    a[y0 - 1:y0 + 1, x0 - 1:x0 + 2] = hx('#A8F077') + (255,)
    return Image.fromarray(_outline(a, '#0D0813', '#8F2464'), 'RGBA')


def _portrait(face, w=8, h=10):
    """A patron's face area-averaged to w x h and snapped to the game's inks (never NEAREST-squashed)."""
    f = face.convert('RGBA')
    bb = f.getbbox()
    f = f.crop(bb)
    f = f.crop((0, 0, f.width, min(f.height, int(f.width * h / w) + 2)))
    rgb = Image.new('RGB', f.size, hx('#9C8F80'))
    rgb.paste(f, mask=f.split()[3])
    small = np.asarray(rgb.resize((w, h), Image.BOX)).astype(np.float32)
    import pixelsnap as PS
    pal = np.vstack([PS.game_inks(), np.array([hx('#F39561'), hx('#C96A44'), hx('#7A3B2A'), hx('#2B1A14')], np.float32)])
    return PS.snap(small.astype(np.uint8), pal)


def id_card(w=36, h=28, face=None):
    """The patron licence, upright on the bar: Cream card, the licence's sunset band and its
    navy end block, a photo, a flag under it (Cream and ViceRed stripes, a ClubBlue canton), three lines."""
    a = np.zeros((h + 2, w + 2, 4), np.uint8)
    a[1:h + 1, 1:w + 1] = hx('#F2E8D5') + (255,)
    a[1:6, 1:w + 1] = hx('#E8A33D') + (255,)
    a[1:3, 1:w + 1] = hx('#F5C97B') + (255,)
    a[5, 1:w + 1] = hx('#C9822B') + (255,)
    a[1:6, w - 5:w + 1] = hx('#1F2E66') + (255,)                               # navy end block
    a[8:18, 3:11] = hx('#9C8F80') + (255,)                                     # photo
    if face is not None:
        a[8:18, 3:11, :3] = _portrait(face)[..., :3]
    for y in range(19, 24):                                                    # flag
        a[y, 3:13] = hx('#F2E8D5') + (255,) if y % 2 == 0 else hx('#D9455C') + (255,)
    a[19:22, 3:7] = hx('#2E4699') + (255,)
    for y, L in ((9, 18), (13, 14), (17, 16), (21, 12)):
        a[y, 15:15 + L] = hx('#6E6459') + (255,)
    a[h, 1:w + 1] = hx('#C9BCA8') + (255,)
    return Image.fromarray(_outline(a, '#0D0813', '#8F2464'), 'RGBA')


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
