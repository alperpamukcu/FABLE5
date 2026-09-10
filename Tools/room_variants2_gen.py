# -*- coding: utf-8 -*-
"""SECOND ROUND OF ROOM VARIANTS, FOR THE AUTHOR TO PICK FROM (2026-09-10).

The author kept table_v1, table_v2 and art_city from the first round and asked for:

  * more WALL PICTURES, and they need not be triptychs — a single framed picture is fine;
  * POTTED PLANTS for the two plant hooks;
  * a CEILING upgrade: "şu an tavanımız bomboş krem yamuk ... bu tavanın boyutuna tam
    uygun farklı şekillerde tavanlar üret".

THE CEILING IS NOT GENERATED AS A TRAPEZOID. A model asked for a converging ceiling comes
back with a rectangle, or with its own idea of where the vanishing point is. So each
ceiling is generated FLAT — a square of tin, of boards, of mirror — and this file maps it
into the room's own trapezoid with a real one-point perspective: the shape is read off
fx_walls_*.png (the cream the room already draws), so the fit is the art's own and cannot
drift, and the pattern compresses toward the back wall the way a ceiling does.

NOTHING SHIPS FROM HERE. Takes land in Tools/room_variants2/ and a contact sheet is written
beside them; the author picks first (the house rule, memory bottle-art-v3-respec).

  py -3 -X utf8 Tools/room_variants2_gen.py take     submit / collect
  py -3 -X utf8 Tools/room_variants2_gen.py build    warp the ceilings into the room's shape
  py -3 -X utf8 Tools/room_variants2_gen.py sheet    the contact sheet
"""
import io
import json
import os
import re
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(HERE, 'room_variants2')
STATE = os.path.join(OUT, 'state.json')
WALLS = os.path.join(ROOT, 'Assets', 'Resources', 'Fixtures', 'fx_walls_1.png')
CREAM = (246, 234, 210)

sys.path.insert(0, HERE)
import pixellab                                   # noqa: E402

ROOM = ('pixel art, warm 1980s Miami bar interior palette, magenta and teal rim light on '
        'dark wood and brass, flat colours with hard pixel edges, no dithering, no text, '
        'no watermark')

# The first pass put the subject at the END of this sentence and four of six takes came
# back as something else entirely (the cassette drew a skyline, the sunglasses a landscape,
# the shark drew FOUR frames). So the subject leads now and the count is stated twice.
FRAMED_A = 'a pixel art painting of '
FRAMED_B = (', hanging on a wall in EXACTLY ONE rectangular frame, a single frame and a '
            'single picture, a thin brass frame round it, seen straight on, transparent '
            'background, no wall behind it, ')

POT = ('a house plant in a pot standing on the floor, seen straight on from a low eye level, '
       'transparent background, no floor, no shadow, ')

TILE = ('a flat ceiling texture seen straight from below, filling the whole frame edge to '
        'edge, tileable, even and repeating, no perspective, no vanishing point, ')

JOBS = {
    # ── single pictures for the wall (the author kept art_city) ────────────────
    'pic_neon_palm':  (84, 104, 401, FRAMED_A + 'a neon pink palm tree on a deep violet '
                       'night sky with one big moon behind it' + FRAMED_B + ROOM),
    # (503 and 511 came back with no frame at all — the subject on its own. The frame has
    #  to be described as a thing with a width, not as a property of the picture.)
    'pic_shark':      (84, 104, 603, 'a framed picture in a THICK wooden picture frame '
                       'with a wide visible border on all four sides, and inside the frame '
                       'a pixel art painting of one teal shark swimming across a pale coral '
                       'background' + FRAMED_B + ROOM),
    'pic_cassette':   (120, 84, 507, FRAMED_A + 'one magenta audio cassette tape seen close '
                       'up on a black background' + FRAMED_B + ROOM),
    'pic_sunglasses': (84, 104, 509, FRAMED_A + 'one pair of white sunglasses with an orange '
                       'sunset reflected in each lens' + FRAMED_B + ROOM),
    'pic_cocktail':   (84, 104, 611, 'a framed picture in a THICK brass picture frame with a '
                       'wide visible border on all four sides, and inside the frame a pixel '
                       'art painting of one cocktail glass with a cherry in it on a magenta '
                       'background' + FRAMED_B + ROOM),
    'pic_skyline':    (120, 84, 413, FRAMED_A + 'a night skyline of a coastal city, lit '
                       'windows and a teal horizon line' + FRAMED_B + ROOM),

    # ── potted plants (the hooks stand art 38..58 wide, 61..96 tall) ──────────
    'plant_monstera': (56, 92, 421, POT + 'a monstera with three big split leaves in a '
                       'terracotta pot, ' + ROOM),
    'plant_cactus':   (48, 84, 423, POT + 'a tall column cactus with two arms in a striped '
                       'clay pot, ' + ROOM),
    'plant_orchid':   (48, 88, 427, POT + 'a white orchid on two arching stems in a glazed '
                       'teal pot, ' + ROOM),
    'plant_fern':     (56, 76, 429, POT + 'a bushy boston fern in a brass planter on three '
                       'small legs, ' + ROOM),
    'plant_bird':     (56, 96, 431, POT + 'a bird of paradise plant with tall paddle leaves '
                       'in a cream ceramic pot, ' + ROOM),

    # ── ceilings, generated FLAT and warped below ──────────────────────────────
    'ceil_tin':       (128, 128, 441, TILE + 'pressed tin ceiling tiles, a square rosette '
                       'stamped in each tile, warm brass tone, ' + ROOM),
    # (443 came back nearly black — a ceiling has to catch the room's light or it just
    #  reads as a hole, so 445 asks for the lit face of the boards.)
    'ceil_beams':     (128, 128, 445, TILE + 'honey coloured wood planks lit from below, '
                       'running in one direction, a darker beam every few planks, ' + ROOM),
    'ceil_neon':      (128, 128, 451, TILE + 'a grid of frosted white lightbox panels in '
                       'black framing, a magenta neon tube along every second seam, ' + ROOM),
    'ceil_sky':       (128, 128, 453, TILE + 'a glass skylight in black steel glazing bars, '
                       'a deep violet night sky and small stars behind the glass, ' + ROOM),
    'ceil_mirror':    (128, 128, 447, TILE + 'square mirrored panels in thin brass framing, '
                       'each panel a slightly different violet tint, ' + ROOM),
    'ceil_stucco':    (128, 128, 449, TILE + 'pale plaster with a shallow coffered grid '
                       'pressed into it, soft cream and sand tones, ' + ROOM),
}

CEILINGS = ['ceil_tin', 'ceil_beams', 'ceil_mirror', 'ceil_stucco', 'ceil_neon', 'ceil_sky']


def _state():
    if os.path.exists(STATE):
        return json.load(io.open(STATE, encoding='utf-8'))
    return {}


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
        text, images = _call('create_image_pro', {
            'description': desc, 'width': w, 'height': h,
            'no_background': not key.startswith('ceil_'), 'seed': seed})
        if images:
            io.open(png, 'wb').write(images[0])
            print('  ->', key)
            continue
        m = re.search(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}', text, re.I)
        s.setdefault(key, {})['job'] = m.group(0) if m else None
        _save(s)
        print('  queued %s %s' % (key, m.group(0) if m else text.strip()[:80]))


# ── the room's own ceiling shape ────────────────────────────────────────────────

def ceiling_mask():
    """The trapezoid the ceiling actually is, taken from all four wall plates at once.

    Measured 2026-09-10: every tier paints the cream as an UNBROKEN run per row, and
    tiers 3-4 are tiers 1-2 shifted one row down — 71 rows against 72, y 0 x 4..637 down
    to y 70 x 146..493 on the first pair, y 0 x 2..637 down to y 71 x 146..491 on the
    second. So the plate is cut on the UNION of the runs: it covers the cream on every
    tier, and where a tier is a row shorter the extra row lands on that tier's own
    cornice shading (235,225,204) rather than on anything the eye reads as wall."""
    size = None
    span = {}
    for i in (1, 2, 3, 4):
        p = os.path.join(os.path.dirname(WALLS), 'fx_walls_%d.png' % i)
        im = Image.open(p).convert('RGBA')
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
    rows = {y: (lo, hi, range(lo, hi + 1)) for y, (lo, hi) in span.items()}
    return size, rows


def warp(tex, rows, size, squash=0.34, inset=5, repeat=2):
    """A flat tile mapped into the trapezoid with one-point perspective.

    Depth runs DOWN the picture — the wide edge at the top is the ceiling over the viewer,
    the narrow one at the foot is where it meets the back wall — so v is warped rather than
    linear: rows near the wall carry more of the texture, which is what makes a ceiling
    read as going away instead of as a fan.

    Two things the first pass got wrong, both measured: the tiles come back with a BORDER
    (ceil_tin's right column and bottom row are pure 255,252,243), and laid edge to edge
    that border lands along the room's own diagonal as a bright hairline against the dark
    wall — so `inset` crops it off before anything is sampled. And one 128 px tile pulled
    across 634 px of room is stretched past reading as a ceiling, so `repeat` lays it more
    than once across the width. That second copy is MIRRORED rather than repeated: the
    tiles are not truly seamless and a modulo laid the tile's own edge straight down the
    middle of the room as a visible join. A ping-pong has no join, and a ceiling symmetric
    about the room's centre line is right anyway — that line is where the vanishing point
    sits."""
    w, h = size
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    op = out.load()
    tex = tex.crop((inset, inset, tex.width - inset, tex.height - inset))
    tp = tex.load()
    tw, th = tex.size
    ymax = max(rows)
    for y, (x0, x1, run) in rows.items():
        t = y / float(ymax) if ymax else 0.0
        v = t / (t + (1.0 - t) / squash) if t < 1.0 else 1.0
        sy = min(th - 1, int(v * (th - 1) + 0.5))
        span = max(1, x1 - x0)
        for x in run:
            u = (x - x0) / float(span) * repeat
            u = abs(u % (2.0) - 1.0) if repeat > 1 else u        # ping-pong, no join
            sx = min(tw - 1, int(u * (tw - 1) + 0.5))
            c = tp[sx, sy]
            op[x, y] = (c[0], c[1], c[2], 255)
    return out


def build():
    size, rows = ceiling_mask()
    made = []
    for key in CEILINGS:
        p = os.path.join(OUT, key + '.png')
        if not os.path.exists(p):
            print('  !! no take for', key)
            continue
        tex = Image.open(p).convert('RGBA')
        plate = warp(tex, rows, size)
        dst = os.path.join(OUT, 'fx_' + key + '.png')
        plate.save(dst)
        made.append(dst)
        print('  warped %-12s -> %s' % (key, os.path.basename(dst)))
    return made


FIXT = os.path.join(ROOT, 'Assets', 'Resources', 'Fixtures')


def sheet():
    """Two contact sheets: the loose pieces at 3x, and every ceiling IN THE ROOM.

    A ceiling is 640 px of nearly flat colour — judged on its own it says nothing, and a
    swatch of it says less. So each one is composited onto the wall it will hang over and
    the top of the room is cropped out: what the author sees is the picture the player
    would see."""
    def load(name, where=OUT):
        p = os.path.join(where, name + '.png')
        return Image.open(p).convert('RGBA') if os.path.exists(p) else None

    # ── the loose pieces, with the plants the room already stands for scale ───
    rows = [
        ['pic_neon_palm', 'pic_shark', 'pic_cassette', 'pic_sunglasses',
         'pic_cocktail', 'pic_skyline'],
        ['plant_monstera', 'plant_cactus', 'plant_orchid', 'plant_fern', 'plant_bird'],
    ]
    have = [[load(n) for n in r] for r in rows]
    have[1] += [load('fx_plant_' + n, FIXT) for n in ('palm', 'fiddle', 'pothos',
                                                      'snake', 'agave')]
    have = [[b for b in r if b is not None] for r in have]
    k = 3
    W = 40 + max(sum(b.width * k + 14 for b in r) for r in have if r)
    H = 32 + sum(max(b.height * k for b in r) + 28 for r in have if r)
    sh = Image.new('RGBA', (W, H), (36, 22, 42, 255))
    y = 16
    for r in have:
        x, tall = 20, 0
        for b in r:
            big = b.resize((b.width * k, b.height * k), Image.NEAREST)
            sh.alpha_composite(big, (x, y + max(0, 0)))
            x += big.width + 14
            tall = max(tall, big.height)
        y += tall + 28
    p = os.path.join(HERE, 'room_variants2_preview.png')
    sh.save(p)
    print('sheet ->', p)

    # ── the ceilings, over the wall, cropped to the top of the room ───────────
    CROP = 108
    wall = Image.open(os.path.join(FIXT, 'fx_walls_3.png')).convert('RGBA')
    plates = [(n, load('fx_ceil_' + n)) for n in ('tin', 'beams', 'mirror', 'stucco', 'neon', 'sky')]
    plates = [(n, im) for n, im in plates if im is not None]
    bare = wall.crop((0, 0, wall.width, CROP))
    strips = [('now: bare', bare)]
    for n, im in plates:
        one = wall.copy()
        one.alpha_composite(im)
        strips.append((n, one.crop((0, 0, wall.width, CROP))))
    z = 2
    cs = Image.new('RGBA', (40 + wall.width * z, 24 + len(strips) * (CROP * z + 16)),
                   (36, 22, 42, 255))
    y = 16
    for n, im in strips:
        cs.alpha_composite(im.resize((im.width * z, im.height * z), Image.NEAREST), (20, y))
        y += CROP * z + 16
    p2 = os.path.join(HERE, 'room_variants2_ceilings.png')
    cs.save(p2)
    print('ceilings ->', p2, ' order: ' + ', '.join(n for n, _ in strips))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'take'
    if cmd == 'sheet':
        sheet()
    elif cmd == 'build':
        build()
    else:
        take(sys.argv[2:] or None)
