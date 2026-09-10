# -*- coding: utf-8 -*-
"""THIRD ROUND OF ROOM VARIANTS (2026-09-10).

The author's answer to round two:

  * the ceilings landed — "tavan desenlerine bayıldım" — with ONE correction: no reflection
    is to be drawn into the art ("sadece yansıma olmasın üretimlerde, yansımayı oyun
    içerisinden yapacağız"). So every tile here is asked for MATTE, and the mirrored ceiling
    is gone: a mirror is reflection, there is nothing left of it once the reflection is the
    engine's job;
  * "aynı şekilde parkeleri de zemin olarak alternatiflerini üret" — the same treatment for
    the FLOOR;
  * every picture was liked, and two more kinds are wanted: SMALLER ones, and ones cut to
    the perspective of the RIGHT-HAND wall;
  * plant_bird and plant_monstera are the picks.

Ceilings and floors are generated FLAT and mapped into the room's own shape, the way round
two established. The right-wall pictures are mapped the same way but into a real one-point
perspective on that wall — measured off fx_walls_2/3/4, which agree to the pixel:

    top edge     y = 73 - 0.5000 * (x - 493)
    bottom edge  y = 217 + 0.4571 * (x - 493)
    vanishing point (342.5, 148.3), wall height h(x) = 144 + 0.9571 * (x - 493)

so the horizontal parameter is hyperbolic in screen x (1/d, d = x - x_vp), not linear —
a linear shear is the thing that makes a pasted-on picture read as a sticker.

NOTHING SHIPS FROM HERE; the author picks first.

  py -3 -X utf8 Tools/room_variants3_gen.py take     submit / collect
  py -3 -X utf8 Tools/room_variants3_gen.py build    map everything into the room
  py -3 -X utf8 Tools/room_variants3_gen.py sheet    the contact sheets
"""
import io
import json
import os
import re
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(HERE, 'room_variants3')
STATE = os.path.join(OUT, 'state.json')
FIXT = os.path.join(ROOT, 'Assets', 'Resources', 'Fixtures')
CREAM = (246, 234, 210)

sys.path.insert(0, HERE)
import pixellab                                   # noqa: E402

ROOM = ('pixel art, warm 1980s Miami bar interior palette, magenta and teal accents on '
        'dark wood and brass, flat colours with hard pixel edges, no dithering, no text, '
        'no watermark')

# The author will light and reflect the room in the engine, so the art must carry no light
# of its own: a baked highlight fights a real one and the surface reads as plastic.
MATTE = ('completely matte and flat, no reflections, no mirror, no gloss, no shine, no '
         'specular highlights, no reflected light, ')

TILE = ('a flat ceiling texture seen straight from below, filling the whole frame edge to '
        'edge, tileable, even and repeating, no perspective, no vanishing point, ' + MATTE)

DECK = ('a flat floor texture seen straight from above, filling the whole frame edge to '
        'edge, tileable, even and repeating, no perspective, no vanishing point, ' + MATTE)

FRAME_A = 'a framed picture in a picture frame with a visible border on all four sides, '
FRAME_B = (', EXACTLY ONE frame and one picture, seen straight on, transparent background, '
           'no wall behind it, ' + MATTE)

JOBS = {
    # ── ceilings again, matte (the mirror one is retired) ──────────────────────
    'ceil_tin':    (128, 128, 741, TILE + 'pressed tin ceiling tiles, a square rosette '
                    'stamped in each tile, warm brass tone, ' + ROOM),
    'ceil_beams':  (128, 128, 743, TILE + 'honey coloured wood planks running in one '
                    'direction, a darker beam every few planks, ' + ROOM),
    'ceil_stucco': (128, 128, 745, TILE + 'pale plaster with a shallow coffered grid '
                    'pressed into it, soft cream and sand tones, ' + ROOM),
    'ceil_neon':   (128, 128, 747, TILE + 'a grid of frosted white lightbox panels in black '
                    'framing, a magenta neon tube along every second seam, ' + ROOM),
    'ceil_sky':    (128, 128, 749, TILE + 'a skylight of dark violet glass panes in black '
                    'steel glazing bars, small stars painted on the glass, ' + ROOM),
    'ceil_deco':   (128, 128, 751, TILE + 'an art deco plaster ceiling, stepped square '
                    'panels with a fan motif in each corner, cream and pale coral, ' + ROOM),
    'ceil_palm':   (128, 128, 753, TILE + 'a painted ceiling of stylised palm fronds spread '
                    'flat, deep teal on dusty pink, ' + ROOM),

    # ── the floor ─────────────────────────────────────────────────────────────
    'floor_parquet': (128, 128, 761, DECK + 'herringbone parquet in warm honey oak, each '
                      'short block outlined, ' + ROOM),
    'floor_oak':     (128, 128, 763, DECK + 'wide dark oak boards running in one direction '
                      'with visible grain and butt joints, ' + ROOM),
    'floor_check':   (128, 128, 765, DECK + 'a checkerboard of black and cream square tiles '
                      'with thin grout lines, ' + ROOM),
    'floor_terrazzo': (128, 128, 767, DECK + 'teal terrazzo, pale chips of stone scattered '
                       'through it, divided by thin brass strips, ' + ROOM),
    'floor_marble':  (128, 128, 769, DECK + 'large pink marble tiles with soft white veining '
                      'and thin dark grout, ' + ROOM),
    'floor_carpet':  (128, 128, 771, DECK + 'a patterned hotel carpet, magenta and teal '
                      'geometric shapes on a deep plum ground, ' + ROOM),

    # ── smaller pictures for the back wall ────────────────────────────────────
    'picS_flamingo': (48, 56, 781, FRAME_A + 'a small pink flamingo head on a teal ground'
                      + FRAME_B + ROOM),
    'picS_moon':     (48, 56, 783, FRAME_A + 'a big cream moon over a flat violet sea'
                      + FRAME_B + ROOM),
    'picS_cherry':   (48, 48, 785, FRAME_A + 'two red cocktail cherries on a black ground'
                      + FRAME_B + ROOM),
    'picS_lips':     (56, 44, 787, FRAME_A + 'a pair of magenta lips on a cream ground'
                      + FRAME_B + ROOM),
    'picS_wave':     (56, 44, 789, FRAME_A + 'one curling teal wave on a pale coral ground'
                      + FRAME_B + ROOM),
    'picS_neonsign': (48, 56, 791, FRAME_A + 'a pink neon crescent and a star on a black '
                      'ground' + FRAME_B + ROOM),

    # ── pictures for the RIGHT wall: generated flat, warped by build() ─────────
    'picR_palmtree': (72, 88, 801, FRAME_A + 'a single dark palm tree against an orange '
                      'sunset sky' + FRAME_B + ROOM),
    'picR_portrait': (72, 88, 803, FRAME_A + 'the painted portrait of a woman in a wide '
                      'brimmed hat, seen from the front' + FRAME_B + ROOM),
    'picR_abstract': (72, 88, 805, FRAME_A + 'flat overlapping shapes in magenta, teal and '
                      'cream, an abstract composition' + FRAME_B + ROOM),
    'picR_pelican':  (72, 88, 807, FRAME_A + 'one white pelican standing on a wooden post '
                      'against a pale sky' + FRAME_B + ROOM),
}

CEILINGS = [k for k in JOBS if k.startswith('ceil_')]
FLOORS = [k for k in JOBS if k.startswith('floor_')]
RIGHTS = [k for k in JOBS if k.startswith('picR_')]


def _state():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def _save(s):
    os.makedirs(OUT, exist_ok=True)
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(s, indent=1))


def _call(tool, args, timeout=900):
    import contextlib
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        msgs = pixellab.call(tool, args, timeout=timeout)
    text = buf.getvalue()
    images = []
    for m in msgs or []:
        r = m.get('result') if isinstance(m, dict) else None
        for c in (r or {}).get('content', []):
            if c.get('type') == 'image' and c.get('data'):
                import base64
                images.append(base64.b64decode(c['data']))
    return text, images


def take(only=None):
    os.makedirs(OUT, exist_ok=True)
    s = _state()
    for key, (w, h, seed, desc) in JOBS.items():
        if only and key not in only:
            continue
        png = os.path.join(OUT, key + '.png')
        if os.path.exists(png):
            print('  have', key)
            continue
        e = s.get(key) or {}
        if e.get('job'):
            text, images = _call('get_image', {'job_id': e['job']}, timeout=180)
            if images:
                io.open(png, 'wb').write(images[0])
                print('  ->', key)
            else:
                print('  %s still cooking' % key)
            continue
        flat = key.startswith('ceil_') or key.startswith('floor_')
        text, images = _call('create_image_pro', {
            'description': desc, 'width': w, 'height': h,
            'no_background': not flat, 'seed': seed})
        if images:
            io.open(png, 'wb').write(images[0])
            print('  ->', key)
            continue
        m = re.search(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}',
                      text, re.I)
        s.setdefault(key, {})['job'] = m.group(0) if m else None
        _save(s)
        print('  queued %s %s' % (key, m.group(0) if m else text.strip()[:80]))


# ── the room's own shapes, read off the plates ─────────────────────────────────

def ceiling_rows():
    """Rows of the cream trapezoid, unioned over all four wall tiers (round two)."""
    span = {}
    size = None
    for i in (1, 2, 3, 4):
        im = Image.open(os.path.join(FIXT, 'fx_walls_%d.png' % i)).convert('RGBA')
        px = im.load()
        size = im.size
        for y in range(im.height):
            run = [x for x in range(im.width) if px[x, y][:3] == CREAM]
            if not run:
                continue
            lo, hi = min(run), max(run)
            if y in span:
                lo, hi = min(lo, span[y][0]), max(hi, span[y][1])
            span[y] = (lo, hi)
    return size, {y: (lo, hi) for y, (lo, hi) in span.items()}


def floor_rows():
    """Rows of the boards. Measured: every column of fx_walls_3 has a first board pixel
    and everything below it is board too — no holes — so the deck is just `y >= base[x]`,
    a flat back edge at y 221 across the middle with the two side walls cutting in."""
    im = Image.open(os.path.join(FIXT, 'fx_walls_3.png')).convert('RGBA')
    px = im.load()

    def board(c):
        r, g, b, a = c
        return a > 0 and r > b + 18 and r < 210 and b < 110 and g < r

    base = {}
    for x in range(im.width):
        col = [y for y in range(im.height) if board(px[x, y])]
        base[x] = min(col) if col else None
    rows = {}
    for y in range(im.height):
        run = [x for x in range(im.width) if base[x] is not None and base[x] <= y]
        if run:
            rows[y] = (min(run), max(run))
    return im.size, rows


def warp(tex, rows, size, near_at_bottom, squash=0.34, inset=5, repeat=2):
    """A flat tile mapped into a trapezoid with one-point perspective.

    `near_at_bottom` says which end of the picture is close to the eye: False for the
    ceiling (its wide edge is at the TOP of the frame) and True for the floor (its wide
    edge is at the bottom). Everything else is round two's, and for the same measured
    reasons: the tiles come back with a light border, so `inset` crops it before anything
    is sampled, and one 128px tile pulled across 634px is stretched past reading, so it is
    laid twice and MIRRORED — a modulo puts the tile's own seam down the middle of the
    room, and a surface symmetric about the centre line is right anyway."""
    w, h = size
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    op = out.load()
    tex = tex.crop((inset, inset, tex.width - inset, tex.height - inset))
    tp = tex.load()
    tw, th = tex.size
    ys = sorted(rows)
    y0, y1 = ys[0], ys[-1]
    for y in ys:
        x0, x1 = rows[y]
        t = (y - y0) / float(y1 - y0) if y1 > y0 else 0.0
        if near_at_bottom:
            t = 1.0 - t                       # depth runs UP the picture
        v = t / (t + (1.0 - t) / squash) if t < 1.0 else 1.0
        sy = min(th - 1, int(v * (th - 1) + 0.5))
        span = max(1, x1 - x0)
        for x in range(x0, x1 + 1):
            u = (x - x0) / float(span) * repeat
            u = abs(u % 2.0 - 1.0) if repeat > 1 else u
            sx = min(tw - 1, int(u * (tw - 1) + 0.5))
            c = tp[sx, sy]
            op[x, y] = (c[0], c[1], c[2], 255)
    return out


# ── the right-hand wall ────────────────────────────────────────────────────────

VP_X, VP_Y = 342.5, 148.3
WALL_X0 = 493.0                       # the corner: the wall's far edge
TOP_M, TOP_C = -0.5000, 73.0
BOT_M, BOT_C = 0.4571, 217.0


def wall_top(x):
    return TOP_C + TOP_M * (x - WALL_X0)


def wall_bot(x):
    return BOT_C + BOT_M * (x - WALL_X0)


def hang_right(pic, xa, xb, f0, f1):
    """Hang a flat picture on the right wall between screen columns xa..xb, covering the
    wall's height from fraction f0 to f1.

    The horizontal parameter is hyperbolic, not linear. On a wall receding to a vanishing
    point the distance along the wall goes as 1/d where d = x - x_vp, so equal steps of
    screen x are NOT equal steps of picture. Shearing it linearly instead is exactly what
    makes a pasted picture read as a sticker stuck on the glass."""
    out = Image.new('RGBA', (640, 360), (0, 0, 0, 0))
    op = out.load()
    pp = pic.convert('RGBA').load()
    pw, ph = pic.size
    da, db = xa - VP_X, xb - VP_X
    ka, kb = 1.0 / da, 1.0 / db
    for x in range(int(xa), int(xb) + 1):
        u = (ka - 1.0 / (x - VP_X)) / (ka - kb)
        sx = min(pw - 1, max(0, int(u * (pw - 1) + 0.5)))
        top, bot = wall_top(x), wall_bot(x)
        hh = bot - top
        ya, yb = top + f0 * hh, top + f1 * hh
        for y in range(int(round(ya)), int(round(yb)) + 1):
            v = (y - ya) / max(1.0, (yb - ya))
            sy = min(ph - 1, max(0, int(v * (ph - 1) + 0.5)))
            c = pp[sx, sy]
            if c[3] > 40 and 0 <= x < 640 and 0 <= y < 360:
                op[x, y] = (c[0], c[1], c[2], 255)
    return out


def build():
    csize, crows = ceiling_rows()
    fsize, frows = floor_rows()
    for key in CEILINGS:
        p = os.path.join(OUT, key + '.png')
        if os.path.exists(p):
            warp(Image.open(p).convert('RGBA'), crows, csize, False).save(
                os.path.join(OUT, 'fx_' + key + '.png'))
            print('  ceiling', key)
    for key in FLOORS:
        p = os.path.join(OUT, key + '.png')
        if os.path.exists(p):
            warp(Image.open(p).convert('RGBA'), frows, fsize, True).save(
                os.path.join(OUT, 'fx_' + key + '.png'))
            print('  floor  ', key)
    # the right wall carries two hangs: one over the counter end, one nearer the door
    for key in RIGHTS:
        p = os.path.join(OUT, key + '.png')
        if not os.path.exists(p):
            continue
        plate = hang_right(Image.open(p), 506, 556, 0.22, 0.62)
        plate.save(os.path.join(OUT, 'fx_' + key + '.png'))
        bb = plate.getbbox()
        print('  right   %-14s bbox %s' % (key, bb))


def sheet():
    def load(n, where=OUT):
        p = os.path.join(where, n + '.png')
        return Image.open(p).convert('RGBA') if os.path.exists(p) else None

    wall = Image.open(os.path.join(FIXT, 'fx_walls_3.png')).convert('RGBA')

    def strip(names, prefix, crop):
        out = [('now', wall.crop(crop))]
        for n in names:
            im = load(prefix + n)
            if im is None:
                continue
            one = wall.copy()
            one.alpha_composite(im)
            out.append((n, one.crop(crop)))
        return out

    def stack(strips, path, z=2):
        if len(strips) < 2:
            print('  (nothing for %s yet)' % os.path.basename(path))
            return
        w = strips[0][1].width
        h = strips[0][1].height
        sh = Image.new('RGBA', (40 + w * z, 24 + len(strips) * (h * z + 16)),
                       (36, 22, 42, 255))
        y = 16
        for _, im in strips:
            sh.alpha_composite(im.resize((im.width * z, im.height * z), Image.NEAREST),
                               (20, y))
            y += h * z + 16
        sh.save(path)
        print('%s  ->  %s' % (', '.join(n for n, _ in strips), path))

    stack(strip([k[5:] for k in CEILINGS], 'fx_ceil_', (0, 0, 640, 108)),
          os.path.join(HERE, 'room_variants3_ceilings.png'))
    stack(strip([k[6:] for k in FLOORS], 'fx_floor_', (0, 210, 640, 360)),
          os.path.join(HERE, 'room_variants3_floors.png'))
    stack(strip([k[5:] for k in RIGHTS], 'fx_picR_', (470, 40, 640, 280)),
          os.path.join(HERE, 'room_variants3_rightwall.png'), z=3)

    small = [load(k) for k in JOBS if k.startswith('picS_')]
    small = [s for s in small if s is not None]
    if small:
        k = 4
        sh = Image.new('RGBA', (40 + sum(s.width * k + 14 for s in small),
                                40 + max(s.height * k for s in small)), (36, 22, 42, 255))
        x = 20
        for s in small:
            sh.alpha_composite(s.resize((s.width * k, s.height * k), Image.NEAREST), (x, 20))
            x += s.width * k + 14
        p = os.path.join(HERE, 'room_variants3_small.png')
        sh.save(p)
        print('small ->', p)


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'take'
    if cmd == 'sheet':
        sheet()
    elif cmd == 'build':
        build()
    else:
        take(sys.argv[2:] or None)
