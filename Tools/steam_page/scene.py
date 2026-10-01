# -*- coding: utf-8 -*-
"""THE STORE ART'S ROOM - a pixel scene built only from the game's own sprites and palette tokens.

Everything is drawn at NATIVE pixel size (the game's 640x360 grid) and enlarged by whole multiples with
NEAREST, never resampled: the sky, the sun and the sea are palette bands with an ordered (Bayer) dither, the
skyline and the palms are the room's window sprites, the patrons and Roxy are the game's own 220x220 frames,
the bottles are the v4 sandwich (back + liquid under the mask + front). Glows are the title sign's four flat
alpha bands (150/78/34/12), so the art lights the same way the logo does.
"""
import os, glob
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
RES = os.path.join(ROOT, 'Assets', 'Resources')

RAMPS = {
    'Night':    [0x0D0813, 0x1A1023, 0x241830, 0x362447, 0x4A3160],
    'Magenta':  [0x5C1B45, 0x8F2464, 0xC23283, 0xE84DA6, 0xFF7DC6],
    'Cyan':     [0x123B45, 0x1B5F66, 0x26918F, 0x3BC8BE, 0x7DF0E3],
    'ClubBlue': [0x131B3D, 0x1F2E66, 0x2E4699, 0x4467CC, 0x6E93F0],
    'Cream':    [0x453E38, 0x6E6459, 0x9C8F80, 0xC9BCA8, 0xF2E8D5],
    'Graphite': [0x14161A, 0x24272D, 0x383D45, 0x545A64, 0x808893],
    'Amber':    [0x4A2E14, 0x8F5A1E, 0xC9822B, 0xE8A33D, 0xF5C97B],
}
GLOW = (150, 78, 34, 12)
BAYER4 = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0


def C(name, a=255):
    ramp, i = name.rstrip(']').split('[')
    h = RAMPS[ramp][int(i)]
    return ((h >> 16) & 255, (h >> 8) & 255, h & 255, a)


def load(rel):
    return Image.open(os.path.join(RES, rel)).convert('RGBA')


def up(img, s):
    return img if s == 1 else img.resize((img.width * s, img.height * s), Image.NEAREST)


def frames(who, clip):
    return [load(os.path.relpath(f, RES)) for f in sorted(glob.glob(os.path.join(RES, 'Patron', who, clip, '*.png')))]


def sprite(who, clip='idle', i=0):
    fs = frames(who, clip)
    return fs[i % len(fs)]


# ── sky / sun / sea ──────────────────────────────────────────────────────────────────────────────────
SKY = ['Night[1]', 'Night[2]', 'Night[3]', 'Magenta[0]', 'Magenta[1]', 'Magenta[2]', 'Magenta[3]', 'Amber[2]', 'Amber[3]']


def banded(w, h, stops, x0=0, y0=0):
    """Vertical run through `stops`, each step dithered into the next with the 4x4 Bayer matrix."""
    t = np.linspace(0, len(stops) - 1, h, endpoint=False)[:, None].repeat(w, 1)
    lo = np.floor(t).astype(int)
    fr = t - lo
    ys, xs = np.mgrid[0:h, 0:w]
    th = BAYER4[(ys + y0) % 4, (xs + x0) % 4]
    idx = np.clip(lo + (fr > th), 0, len(stops) - 1)
    pal = np.array([C(s) for s in stops], np.uint8)
    return Image.fromarray(pal[idx], 'RGBA')


def sun(r, stripes=True):
    """The synthwave sun: Amber[4] at the top to Magenta[3] at the waterline, its lower half cut by bars that
    thicken toward the horizon. Returns the upper half only (the sea takes the rest)."""
    d = 2 * r
    ys, xs = np.mgrid[0:r, 0:d]
    inside = (xs - r + 0.5) ** 2 + (ys - r + 0.5) ** 2 <= r * r
    stops = ['Amber[4]', 'Amber[3]', 'Amber[3]', 'Magenta[4]', 'Magenta[3]', 'Magenta[3]']
    body = np.asarray(banded(d, r, stops)).copy()
    body[~inside, 3] = 0
    if stripes:
        y = int(r * 0.42); gap = 1
        while y < r:
            body[y:y + gap, :, 3] = 0
            y += gap + max(2, int((r - y) * 0.28))
            gap += 1 if gap < 4 else 0
    return Image.fromarray(body, 'RGBA')


def glow(mask, colour, bands=GLOW, step=1):
    """Halo of `mask` (bool array) as four flat alpha bands, `step` px each - the title sign's own light."""
    h, w = mask.shape
    out = np.zeros((h, w, 4), np.uint8)
    cur = mask.copy()
    rings = []
    for _ in bands:
        for _ in range(step):
            n = cur.copy()
            n[1:] |= cur[:-1]; n[:-1] |= cur[1:]; n[:, 1:] |= cur[:, :-1]; n[:, :-1] |= cur[:, 1:]
            cur = n
        rings.append(cur.copy())
    prev = mask
    for ring, a in zip(rings, bands):
        k = ring & ~prev
        out[k] = colour[:3] + (a,)
        prev = ring
    return Image.fromarray(out, 'RGBA')


def sea(w, h, sun_cx, sun_r, y0=0):
    img = banded(w, h, ['ClubBlue[1]', 'ClubBlue[0]', 'Night[2]', 'Night[1]'], y0=y0)
    a = np.asarray(img).copy()
    # the sun's road: dashes that thin and spread toward the viewer
    rng = np.random.RandomState(7)
    for y in range(0, h, 2):
        half = int(sun_r * (0.55 + 0.9 * y / max(1, h)))
        n = max(1, int(6 - 4 * y / max(1, h)))
        for _ in range(n + 3):
            x = sun_cx + rng.randint(-half, half + 1)
            L = rng.randint(2, 7 + y // 6)
            col = C('Amber[3]') if y < h * 0.25 else C('Magenta[3]') if y < h * 0.6 else C('Magenta[1]')
            a[y, max(0, x):min(w, x + L)] = col
    # far whitecaps
    for _ in range(w // 9):
        x, y = rng.randint(0, w), rng.randint(0, h)
        a[y, x:x + rng.randint(2, 5)] = C('ClubBlue[2]')
    return Image.fromarray(a, 'RGBA')


def stars(img, top, bottom, n, seed=3):
    a = np.asarray(img).copy()
    rng = np.random.RandomState(seed)
    for _ in range(n):
        x, y = rng.randint(0, a.shape[1]), rng.randint(top, bottom)
        a[y, x] = C('Cream[4]') if rng.rand() < .3 else C('Cream[3]', 200)
    return Image.fromarray(a, 'RGBA')


def palm(side='l'):
    p = load('Scene/window_palm_%s.png' % side)
    p.alpha_composite(load('Scene/window_palm_%s_crown.png' % side))
    return p.crop(p.getbbox())


def silhouette(img, colour):
    a = np.asarray(img).copy()
    k = a[..., 3] > 0
    a[k, :3] = colour[:3]
    return Image.fromarray(a, 'RGBA')


# ── bottles and glasses (the v4 sandwich) ────────────────────────────────────────────────────────────
LIQ = {'gin': 'Cream[4]', 'vodka': 'Cream[4]', 'bourbon': 'Amber[2]', 'rum': 'Amber[1]', 'amaro': 'Magenta[0]',
       'tequila': 'Amber[4]', 'cola': 'Night[1]', 'energy': 'Cyan[3]', 'whisky': 'Amber[2]', 'liqueur': 'Magenta[2]'}


def bottle(card, fill=0.7, cellar=False):
    sfx = '_c' if cellar else ''
    back = load('Items/v4_%s_back%s.png' % (card, sfx))
    mask = np.asarray(load('Items/v4_%s_mask%s.png' % (card, sfx)))[..., 3] > 0
    front = load('Items/v4_%s_front%s.png' % (card, sfx))
    kind = card.split('_')[0]
    col = C(LIQ.get(kind, 'Amber[2]'), 210)
    ys = np.nonzero(mask.any(1))[0]
    line = ys.min() + int((ys.max() - ys.min()) * (1 - fill))
    a = np.zeros(mask.shape + (4,), np.uint8)
    k = mask.copy(); k[:line] = False
    a[k] = col
    out = back.copy()
    out.alpha_composite(Image.fromarray(a, 'RGBA'))
    out.alpha_composite(front)
    return out.crop(out.getbbox())


def glass(kind='coupe', liquid='Magenta[3]', fill=0.75, rim=None):
    g = load('Items/glass3d_%s.png' % kind)
    f = np.asarray(load('Items/glass3d_%s_fill.png' % kind))[..., 3] > 0
    ys = np.nonzero(f.any(1))[0]
    line = ys.min() + int((ys.max() - ys.min()) * (1 - fill))
    a = np.zeros(f.shape + (4,), np.uint8)
    k = f.copy(); k[:line] = False
    a[k] = C(liquid, 225)
    # one lighter row on the surface
    srow = np.nonzero(k.any(1))[0]
    if len(srow):
        a[srow.min()][k[srow.min()]] = C(liquid.split('[')[0] + '[4]', 235)
    out = Image.fromarray(a, 'RGBA')
    out.alpha_composite(g)
    if rim:
        out.alpha_composite(load('Items/glass3d_%s_rim_%s.png' % (kind, rim)))
    return out.crop(out.getbbox())


# ── the room ─────────────────────────────────────────────────────────────────────────────────────────
def window_room(w, h, horizon, sun_cx, sun_r, city=True, palms=True, frame=True, seed=3, sun_drop=0):
    """The bar's back window at dusk: sky, sun, skyline, sea, palms; posts and a neon strip over it."""
    img = banded(w, horizon, SKY)
    img = stars(img, 0, int(horizon * 0.35), w // 6, seed)
    s = sun(sun_r)
    if sun_drop < sun_r:
        img.alpha_composite(s.crop((0, 0, s.width, sun_r - sun_drop)), (sun_cx - sun_r, horizon - sun_r + sun_drop))
    if city:
        c = load('Scene/curtain_city.png')
        cc = c.crop(c.getbbox())
        # the skyline stands either side of the sun, never across it
        gapL, gapR = sun_cx - int(sun_r * 1.15), sun_cx + int(sun_r * 1.15)
        layer = Image.new('RGBA', img.size, (0, 0, 0, 0))
        x = gapR
        while x < w:
            layer.alpha_composite(cc, (x, horizon - cc.height + 2)); x += cc.width - 6
        x = gapL - cc.width
        while x + cc.width > 0:
            layer.alpha_composite(cc, (x, horizon - cc.height + 2)); x -= cc.width - 6
        la = np.asarray(layer).copy()
        la[:, max(0, gapL):min(w, gapR)] = 0
        img.alpha_composite(Image.fromarray(la, 'RGBA'))
    full = Image.new('RGBA', (w, h), C('Night[1]'))
    full.alpha_composite(img)
    full.alpha_composite(sea(w, h - horizon, sun_cx, max(2, sun_r - sun_drop), horizon), (0, horizon))
    if palms:
        pl, pr = palm('l'), palm('r')
        sil = C('Night[0]')
        for p, x in ((pl, -12), (pr, w - pr.width + 14)):
            big = silhouette(up(p, 2) if h > 300 else p, sil)
            full.alpha_composite(big, (x, horizon - big.height + int(h * 0.12)))
    if frame:
        a = np.asarray(full).copy()
        post = C('Night[0]')
        n = max(2, w // 150)
        for i in range(1, n):
            x = int(w * i / n)
            a[:, x - 2:x + 2] = post
            a[:, x - 3] = C('Night[2]')
        a[:6] = post
        full = Image.fromarray(a, 'RGBA')
    return full


def neon_bar(img, y, colour='Magenta[3]', core='Magenta[4]', thick=1):
    """A straight neon tube across the picture at row y, with the four-band halo."""
    w, h = img.size
    m = np.zeros((h, w), bool)
    m[y:y + thick + 1] = True
    img.alpha_composite(glow(m, C(colour), step=2))
    a = np.asarray(img).copy()
    a[y:y + thick + 1] = C(colour)
    a[y + thick // 2] = C(core)
    return Image.fromarray(a, 'RGBA')


def counter(w, h, top=10):
    """The bar top and its front: black stone, a magenta lip, panels, a cyan under-glow."""
    a = np.zeros((h, w, 4), np.uint8)
    a[:] = C('Night[1]')
    a[0:top] = C('Night[0]')
    a[1] = C('Graphite[2]')
    a[top - 3] = C('Magenta[2]')
    a[top - 2] = C('Magenta[3]')
    a[top:top + 2] = C('Night[0]')
    # panels
    pw = 48
    for x in range(0, w if h > top + 14 else 0, pw):
        a[top + 6:h - 6, x + 2:x + 3] = C('Night[3]')
        a[top + 6:h - 6, x + pw - 3:x + pw - 2] = C('Night[0]')
        a[top + 6, x + 2:x + pw - 2] = C('Night[3]')
        a[h - 7, x + 2:x + pw - 2] = C('Night[0]')
    img = Image.fromarray(a, 'RGBA')
    return img


def drop_shadow(img, dx=0, dy=0, alpha=110):
    a = np.asarray(img).copy()
    k = a[..., 3] > 0
    s = np.zeros_like(a)
    s[k] = C('Night[0]', alpha)
    return Image.fromarray(s, 'RGBA')


def rim_light(img, colour, side='right', alpha=255):
    """One-texel rim of `colour` on the figure's edge facing the light - the neon catching her. Blended over
    the sprite's own outline (alpha < 255 keeps the drawing underneath)."""
    a = np.asarray(img).copy()
    k = a[..., 3] > 128
    if side == 'right':
        edge = k & ~np.roll(k, -1, 1)
    elif side == 'left':
        edge = k & ~np.roll(k, 1, 1)
    else:
        edge = k & ~np.roll(k, 1, 0)
    src = a[edge, :3].astype(np.float32)
    a[edge, :3] = (src * (1 - alpha / 255) + np.array(colour[:3]) * (alpha / 255)).astype(np.uint8)
    return Image.fromarray(a, 'RGBA')


def tint(img, colour, amount):
    a = np.asarray(img).astype(np.float32).copy()
    c = np.array(colour[:3], np.float32)
    a[..., :3] = a[..., :3] * (1 - amount) + c * amount
    return Image.fromarray(a.clip(0, 255).astype(np.uint8), 'RGBA')


def vignette(img, strength=0.55, colour='Night[0]'):
    w, h = img.size
    ys, xs = np.mgrid[0:h, 0:w]
    d = np.sqrt(((xs - w / 2) / (w / 2)) ** 2 + ((ys - h / 2) / (h / 2)) ** 2) / 1.41
    lv = np.clip((d - 0.45) / 0.55, 0, 1) * strength
    # quantise to four steps so it stays banded like everything else
    lv = np.round(lv * 4) / 4
    a = np.zeros((h, w, 4), np.uint8)
    a[..., :3] = C(colour)[:3]
    a[..., 3] = (lv * 255).astype(np.uint8)
    out = img.copy()
    out.alpha_composite(Image.fromarray(a, 'RGBA'))
    return out


# ── the ID card (the game's licence shell, its band, a face, a flag, Silkscreen 8px) ─────────────────
FONT = os.path.join(ROOT, 'Assets', 'Fonts', 'Silkscreen-Regular.ttf')
FONT_BOLD = os.path.join(ROOT, 'Assets', 'Fonts', 'SilkscreenBold.ttf')


def text(img, xy, s, colour, size=8, bold=False):
    """Pixel text at a whole multiple of the face's 8px design size, aliasing off (threshold at half)."""
    from PIL import ImageDraw, ImageFont
    f = ImageFont.truetype(FONT_BOLD if bold else FONT, size)
    l, t, r, b = f.getbbox(s)
    m = Image.new('L', (r + 2, b + 2), 0)
    ImageDraw.Draw(m).text((0, 0), s, font=f, fill=255)
    m = m.point(lambda v: 255 if v > 127 else 0)
    ink = Image.new('RGBA', m.size, colour)
    ink.putalpha(m)
    img.alpha_composite(ink, xy)
    return r


def id_card(who, name, age, iso, country, born=None):
    card = load('Items/licence_shell3.png')
    band = load('Items/licence_band.png')
    card.alpha_composite(band.crop((0, 0, 200, 14)), (17, 12))
    text(card, (22, 15), 'MIAMI . PATRON LICENCE', C('Night[0]'))
    face = load('Patron/%s/face.png' % who)
    fb = face.getbbox()
    cx = (fb[0] + fb[2]) // 2
    ph = face.crop((cx - 24, fb[1] - 1, cx + 25, fb[1] + 48))
    bgp = Image.new('RGBA', ph.size, C('Cream[3]'))
    bgp.alpha_composite(ph)
    card.alpha_composite(bgp, (18, 31))
    ink = C('Night[2]')
    text(card, (75, 37), name.upper(), ink, bold=True)
    text(card, (75, 62), 'AGE %d' % age, ink)
    if born:
        text(card, (130, 62), born, C('Magenta[1]'))
    try:
        fl = load('Items/fl_%s.png' % iso)
        fl = fl.crop(fl.getbbox())
        fl = fl.crop((0, 0, min(fl.width, 47), min(fl.height, 22)))   # a window onto the flag, never shrunk
        card.alpha_composite(fl, (19 + (48 - fl.width) // 2, 86))
    except Exception:
        pass
    text(card, (75, 90), country.upper(), ink)
    text(card, (75, 115), 'NO. 0%05d' % (sum(map(ord, name)) * 37 % 99999), C('Cream[1]'))
    return card
