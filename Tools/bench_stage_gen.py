# -*- coding: utf-8 -*-
"""THE NINTH ROUND'S ART (2026-08-26).

The author, in one message: the bench is empty and needs a background; the things
that go IN a glass belong on the main counter and must be dragged onto it; the ice
and the garnish drawn inside the glass are wrong and must be re-cut; salt and sugar
should sit at the counter's own angle ("biraz daha masanin acisiyla ayni aciya
sahip"); and the two boards beside the night's slip want proper backgrounds
("fatura ekraninda yandaki iki bari UI'ye uygun duzgun arkaplan gorselleri ile").

FOUR FAMILIES, one take each (the standing rule, "gorselden bircok adet uretme"):

  bench_back   the service bench's own room — a back wall and a work counter, drawn
               at the stage's own 640x360 so it lands at a whole 2x. It is a
               BACKDROP: nothing on it may compete with the tin, the glass or the
               bottle that stand in front of it, and the middle band is kept plain
               on purpose because that is where they stand.
  counter_*    the prep set at the MAIN counter's angle. The author called the
               existing mini set good and asked for the same eye line as the room's
               bar top — lower, more from the side, less from above.
  glass_*      what floats IN the drink. The old cube was the licence's pictogram
               and the old lemon was a WHEEL seen face-on, which is a slice lying
               on a plate, not a wedge hooked over a rim.
  board_plate  the night boards' plate: navy metal with a teal cap, the same
               fascia language the week strip already speaks. One drawing, stood
               twice — they are a matched pair and a pair that does not match is
               two instruments.

    py -3 Tools/bench_stage_gen.py take | preview | ship [names...]
"""
import base64
import io
import json
import os
import re
import sys

from PIL import Image

import pixellab

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
STATE = os.path.join(HERE, 'bench_stage_state.json')
STAGING = os.path.join(HERE, 'AssetPipeline', 'staging', 'bench_stage')
PREVIEW = os.path.join(HERE, 'bench_stage_preview.png')
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
SCENE = os.path.join(ROOT, 'Assets', 'Resources', 'Scene')

# The palette every piece is spoken in, written once.
VICE = ('1980s Miami bar palette, dark navy and plum with brass and warm amber, '
        'magenta and teal neon rim light, flat colours with hard pixel edges')

# The eye line the room's bar top is drawn at, written once (the author's note).
ANGLE = ('seen from slightly above at a low three-quarter angle, the way a bottle on '
         'a bar looks to someone standing behind it, not from directly overhead')

# name -> (w, h, seed, description, destination folder, shipped filename)
JOBS = {
    # ── the bench's own room ────────────────────────────────────────────────────
    'bench_back': (640, 360, 31, (
        'pixel art interior background of the working side of a cocktail bar, the top '
        'two thirds a dark wood panelled back wall with a brass rail and a narrow shelf '
        'of shadowed bottles along the very top, the bottom third a wide empty polished '
        'bar counter running edge to edge with a magenta neon strip along its far edge, '
        'the middle of the picture deliberately plain and uncluttered, no people, no '
        'glasses, no bottles standing on the counter, ' + VICE + ', no text'),
        SCENE, 'bench_back.png'),

    # ── the prep set, at the main counter's angle ───────────────────────────────
    'counter_salt': (64, 40, 13, (
        'a shallow wide ceramic dish heaped with fine white salt standing on a bar, '
        + ANGLE + ', single object centred, transparent background, ' + VICE + ', no text'),
        ITEMS, 'counter_salt.png'),
    'counter_sugar': (64, 40, 17, (
        'a shallow wide ceramic dish heaped with pale golden sugar crystals standing on '
        'a bar, ' + ANGLE + ', single object centred, transparent background, '
        + VICE + ', no text'),
        ITEMS, 'counter_sugar.png'),
    'counter_olive': (64, 44, 19, (
        'a short glass jar packed with green cocktail olives, a few cocktail picks '
        'standing in it, on a bar, ' + ANGLE + ', single object centred, transparent '
        'background, ' + VICE + ', no text'),
        ITEMS, 'counter_olive.png'),
    'counter_mint': (64, 52, 23, (
        'a small glass tumbler holding a bunch of fresh green mint sprigs, on a bar, '
        + ANGLE + ', single object centred, transparent background, ' + VICE + ', no text'),
        ITEMS, 'counter_mint.png'),

    # ── what floats in the drink ────────────────────────────────────────────────
    'glass_ice': (32, 32, 29, (
        'a single clear ice cube, pale blue and white with sharp facets and a bright '
        'highlight on one corner, seen at a slight angle, single object centred, '
        'transparent background, pixel art, no text'),
        ITEMS, 'glass_ice.png'),
    'glass_lemon': (36, 48, 37, (
        'a single fresh lemon WEDGE cut from a whole lemon, a quarter segment with '
        'yellow rind along the curved back and pale juicy flesh on the flat face, '
        'standing on its rind, single object centred, transparent background, pixel '
        'art, no text'),
        ITEMS, 'glass_lemon.png'),
    'glass_olive': (28, 52, 41, (
        'two green cocktail olives with red pimento threaded on a thin wooden cocktail '
        'pick standing upright, single object centred, transparent background, pixel '
        'art, no text'),
        ITEMS, 'glass_olive.png'),
    'glass_mint': (40, 40, 43, (
        'a small sprig of fresh mint, four or five bright green leaves on a short stem, '
        'single object centred, transparent background, pixel art, no text'),
        ITEMS, 'glass_mint.png'),

    # ── the coaster the finished drink always stands on ──────────────────
    'counter_coaster': (56, 22, 53, (
        'a round cork drinks coaster lying flat on a bar, seen from a low three-quarter '
        'angle so it reads as a shallow ellipse, dark cork with a thin brass rim and a '
        'faint ring worn into the middle, single object centred, transparent background, '
        + VICE + ', no text'),
        ITEMS, 'counter_coaster.png'),

    # ── the night boards' plate ─────────────────────────────────────────────────
    'board_plate': (178, 192, 47, (
        'pixel art blank rectangular instrument panel for a game UI, dark navy brushed '
        'metal face with a teal capped header bar across the top, a thin brass hairline '
        'under the header, rounded corners, four small rivets, the whole face below the '
        'header EMPTY and flat so writing can be laid on it, ' + VICE + ', no text, no '
        'letters, no numbers, no icons'),
        ITEMS, 'board_plate.png'),
}

RAMPS = [
    (0x0D0813, 0x1A1023, 0x241830, 0x362447, 0x4A3160),   # Night
    (0x5C1B45, 0x8F2464, 0xC23283, 0xE84DA6, 0xFF7DC6),   # Magenta
    (0x123B45, 0x1B5F66, 0x26918F, 0x3BC8BE, 0x7DF0E3),   # Cyan
    (0x4A2E14, 0x8F5A1E, 0xC9822B, 0xE8A33D, 0xF5C97B),   # Amber
    (0x3D1220, 0x6E1B32, 0xA62B44, 0xD9455C, 0xF27D8A),   # ViceRed
    (0x131B3D, 0x1F2E66, 0x2E4699, 0x4467CC, 0x6E93F0),   # ClubBlue
    (0x16331B, 0x2A5926, 0x479938, 0x6FCC4B, 0xA8F077),   # Lime
    (0x453E38, 0x6E6459, 0x9C8F80, 0xC9BCA8, 0xF2E8D5),   # Cream
]
PALETTE = [((v >> 16) & 255, (v >> 8) & 255, v & 255) for ramp in RAMPS for v in ramp]

# The pieces that must NOT be trimmed: a backdrop is its own canvas, and a plate is
# sized to the rect it fills.
KEEP_CANVAS = {'bench_back', 'board_plate'}


def call(tool, args, timeout=900):
    _, body = pixellab.post({'jsonrpc': '2.0', 'id': 1, 'method': 'tools/call',
                             'params': {'name': tool, 'arguments': args}}, timeout=timeout)
    text, images = '', []
    for m in pixellab.sse(body):
        for c in ((m.get('result') or {}).get('content') or []):
            if c.get('type') == 'text':
                text += c['text'] + '\n'
            elif c.get('type') == 'image':
                images.append(Image.open(io.BytesIO(base64.b64decode(c['data']))).convert('RGBA'))
    return text, images


def load():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def save(s):
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(s, indent=1))


def _keep(key, im):
    os.makedirs(STAGING, exist_ok=True)
    p = os.path.join(STAGING, key + '.png')
    im.save(p)
    s = load()
    s.setdefault(key, {})['png'] = os.path.relpath(p, HERE)
    save(s)
    print('  %s -> %s' % (key, p))


def take(only=None):
    s = load()
    for key, job in JOBS.items():
        if only and key not in only:
            continue
        w, h, seed, desc = job[0], job[1], job[2], job[3]
        e = s.get(key) or {}
        if e.get('png'):
            print('  %s already landed' % key)
            continue
        if e.get('job_id'):
            text, images = call('get_image', {'job_id': e['job_id']})
            if images:
                _keep(key, images[0])
            else:
                print('  %s still cooking: %s' % (key, (text.strip().splitlines() or [''])[0][:90]))
            continue
        args = {'description': desc, 'width': w, 'height': h, 'seed': seed}
        # A backdrop keeps its ground; everything else is cut out.
        if key not in KEEP_CANVAS:
            args['no_background'] = True
        text, images = call('create_image_pro', args)
        if images:
            _keep(key, images[0])
            continue
        m = re.search(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}', text, re.I)
        s = load()
        s.setdefault(key, {})['job_id'] = m.group(0) if m else None
        save(s)
        print('  %s queued %s' % (key, m.group(0) if m else text.strip()[:110]))


def quantize(im):
    im = im.convert('RGBA')
    px = im.load()
    cache = {}
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a < 128:
                px[x, y] = (0, 0, 0, 0)
                continue
            got = cache.get((r, g, b))
            if got is None:
                got = min(PALETTE, key=lambda c: (c[0] - r) ** 2 * 3
                          + (c[1] - g) ** 2 * 6 + (c[2] - b) ** 2)
                cache[(r, g, b)] = got
            px[x, y] = (got[0], got[1], got[2], 255)
    return im


def finish(name, im):
    im = quantize(im)
    if name in KEEP_CANVAS:
        # FULL WIDTH, REAL HEIGHT. A backdrop keeps its columns — cropping them would
        # narrow the room — but the bench's wall came back drawn down to row 255 with
        # nothing under it, and shipping the empty rows would put a band of hole between
        # the wall and the counter the bench draws for itself.
        #
        # By COVERAGE, not by bbox: five stray cream pixels a row ran all the way to the
        # bottom edge and held the bounding box open over a hundred rows of nothing. A row
        # is part of the picture when most of it is.
        # ...and the FIRST UNBROKEN RUN of them, because the wall came back with a hollow
        # band under its skirting and a stray strip of counter below that: first-to-last
        # would ship the hole between them. The wall is what this asset is; the counter
        # under it is drawn by the bench, off the room's own sampled colours.
        px = im.load()
        full = [sum(1 for x in range(im.width) if px[x, y][3] > 8) >= im.width * 0.5
                for y in range(im.height)]
        if not any(full):
            return im
        top = full.index(True)
        bot = top
        while bot + 1 < im.height and full[bot + 1]:
            bot += 1
        return im.crop((0, top, im.width, bot + 1))
    bb = im.getbbox()
    return im.crop(bb) if bb else im


def preview():
    s = load()
    tiles = []
    for key in JOBS:
        e = s.get(key) or {}
        if not e.get('png'):
            print('  %s not landed yet' % key)
            continue
        im = finish(key, Image.open(os.path.join(HERE, e['png'])))
        k = 1 if im.width > 200 else (4 if im.width <= 48 else 3)
        tiles.append((key, im.resize((im.width * k, im.height * k), Image.NEAREST)))
    if not tiles:
        return
    w = sum(t.width + 14 for _, t in tiles) + 14
    h = max(t.height for _, t in tiles) + 28
    sheet = Image.new('RGBA', (w, h), (70, 70, 84, 255))
    x = 14
    for key, t in tiles:
        sheet.paste(t, (x, h - 14 - t.height), t)
        x += t.width + 14
    sheet.save(PREVIEW)
    print('wrote', PREVIEW)


def ship(names):
    s = load()
    for name in names:
        job = JOBS.get(name)
        if job is None:
            print('  %s is not a job' % name)
            continue
        e = s.get(name) or {}
        if not e.get('png'):
            print('  %s has not landed' % name)
            continue
        im = finish(name, Image.open(os.path.join(HERE, e['png'])))
        os.makedirs(job[4], exist_ok=True)
        dest = os.path.join(job[4], job[5])
        im.save(dest)
        print('  %s -> %s (%dx%d)' % (name, dest, im.width, im.height))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'take'
    rest = sys.argv[2:]
    if cmd == 'take':
        take(rest or None)
    elif cmd == 'preview':
        preview()
    elif cmd == 'ship':
        ship(rest or list(JOBS))
    else:
        print(__doc__)
