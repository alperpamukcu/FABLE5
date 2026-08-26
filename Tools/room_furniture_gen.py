# -*- coding: utf-8 -*-
"""THE ROOM'S FURNITURE, RE-CUT (2026-08-26, the author: "Masa ve tabure gorselleri
ayni perspektifte oyunun sanatina uygun tekrar olusturulsun ... oyuna eklenen bira
koyma sahnesinde kullanilan buyuk boy fici hem yanlis hem de bozuk gozukuyor,
3 seviyeye uygun buyutulmus halini olusturman gerekiyor").

TWO FAULTS, ONE ROUND.

1. THE TABLE SETS. Three of them stand in the room and no two agree: t1's table is a
   bare steel frame with no top at all and its stools are raw pine, t2 is a brass
   pedestal drawn at a different eye height, t3 is another see-through frame. The
   author is right that they read as three pieces of furniture from three games. They
   are re-cut here as ONE set at ONE eye line: a table with two stools, straight from
   the front, the top showing as a shallow ellipse, in the room's own palette.

2. THE BENCH'S FONT. bench_tap_big was struck last round from "the big version of the
   beer taps we use in the main scene" and it is neither: it wears TWO faucets facing
   opposite ways, carries a red smear where a baked-on handle was erased at ship time,
   and matches none of the three towers the market actually sells. The bar can stand a
   single column, a brass arch or a triple tee, and which one it stands is a purchase
   the player made. So there are three now, each the room's own tower at the bench's
   own grain, and Tap.cs picks by the run's tap level.

ONE TAKE PER ASSET, the standing rule ("gorselden bircok adet uretme"), through the
standard 40-colour quantize.

THE FOOT IS THE CANVAS BOTTOM. PlaceFixtures stands a fixture by the bottom edge of
its sprite, not by its ink, so a generated piece with three empty rows under it floats
three units off the floor. Shipping re-seats every piece: trim to the ink, centre it
across, and sit it on the last row.

    py -3 Tools/room_furniture_gen.py take | preview | ship [names...]
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
STATE = os.path.join(HERE, 'room_furniture_state.json')
STAGING = os.path.join(HERE, 'AssetPipeline', 'staging', 'room_furniture')
PREVIEW = os.path.join(HERE, 'room_furniture_preview.png')

FIXTURES = os.path.join(ROOT, 'Assets', 'Resources', 'Fixtures')
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')

# ONE SENTENCE EVERY PIECE OF FURNITURE SHARES. The whole complaint is that the three
# sets do not agree, so the agreement is written once and glued onto each of them.
SET = ('pixel art, seen straight from the front at a low eye level, the tabletop showing '
       'as a thin shallow ellipse, warm 1980s Miami bar interior palette, magenta and '
       'teal rim light on dark wood and brass, flat colours with hard pixel edges, '
       'transparent background, no floor, no shadow, no text')

TOWER = ('pixel art beer tap tower standing on a bar counter, polished brass and gold, '
         'art deco, seen straight from the front, ONE pull handle only, flat colours '
         'with hard pixel edges, transparent background, no text')

# name -> (w, h, seed, description, destination folder, shipped filename)
JOBS = {
    # ── the three table sets: table in the middle, one stool each side ──────────
    'table_t1': (132, 78, 17, (
        'a small square reclaimed oak bar table with a SOLID thick wooden top and a '
        'welded black iron frame, one plain wooden stool standing at each side of it, '
        'three objects in a row, ' + SET), FIXTURES, 'fx_table_t1.png'),
    'table_t2': (132, 78, 29, (
        'a round bar table with a dark green marble top on a single brass pedestal '
        'column, one green buttoned leather bar stool standing at each side of it, '
        'three objects in a row, ' + SET), FIXTURES, 'fx_table_t2.png'),
    'table_t3': (132, 78, 41, (
        'a round bar table with a glossy plum vinyl top on a polished chrome column '
        'and a wide chrome disc foot, one plum vinyl gas-lift bar stool standing at '
        'each side of it, three objects in a row, ' + SET), FIXTURES, 'fx_table_t3.png'),

    # ── the three fonts, at the bench's own size ────────────────────────────────
    'tap_single_big': (120, 260, 13, (
        'a single slim brass column tap tower on a round brass base, ONE chrome faucet '
        'spout pointing left near the top, ' + TOWER), ITEMS, 'bench_tap_single.png'),
    'tap_arch_big': (260, 240, 19, (
        'a brass arched bridge tap tower, two thick brass legs on round bases joined by '
        'an arch over the top, THREE short faucet spouts hanging down from the arch, '
        + TOWER), ITEMS, 'bench_tap_arch.png'),
    'tap_tee_big': (240, 250, 23, (
        'a brass T-shaped tap tower, one thick brass column on a round base with a '
        'horizontal crossbar at the top, one faucet spout at each end of the crossbar '
        'and one under the middle, ' + TOWER), ITEMS, 'bench_tap_tee.png'),
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
        text, images = call('create_image_pro', {
            'description': desc, 'width': w, 'height': h,
            'no_background': True, 'seed': seed,
        })
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


def seat(im):
    """Trim to the ink, centre it across, sit it on the last row of its own canvas."""
    bb = im.getbbox()
    if not bb:
        return im
    ink = im.crop(bb)
    out = Image.new('RGBA', (ink.width, ink.height), (0, 0, 0, 0))
    out.paste(ink, (0, 0), ink)
    return out


# ── the fonts need two cuts a table does not ────────────────────────────────────
#
# THE GROUND. The arch came back standing on a slab of floor, drawn edge to edge under
# it. A tower stands on the BENCH's counter, which the bench draws itself, so any floor
# in the sprite is a second one. A row that is opaque from edge to edge is not part of
# a tower — a tower is columns and air — so the trailing run of full rows comes off.
#
# THE HANDLE. Both the single and the tee came back with a pull handle drawn ON. The
# handle has to MOVE — pulling it is the whole mechanic — so it is a separate sprite
# mounted at the valve, and a rig must not wear two handles. This is the same erase the
# first big font needed on 2026-08-25, written down this time instead of done by hand.
#
# name -> (erase box in the QUANTIZED art, or None)
CLEANUP = {
    'tap_single_big': (2, 0, 14, 27),
    # The arch's middle spout came back with a drawn finial between its wheel and the
    # arch. That is the handle's seat, so the finial goes and the mounted lever stands
    # in its place — it reads as the linkage the wheel hangs from.
    'tap_arch_big': (84, 12, 101, 50),
    'tap_tee_big': (81, 0, 102, 47),
}


def strip_ground(im):
    w, h = im.size
    px = im.load()
    cut = h
    for y in range(h - 1, -1, -1):
        if sum(1 for x in range(w) if px[x, y][3] > 8) >= w * 0.95:
            cut = y
        else:
            break
    return im if cut >= h else im.crop((0, 0, w, cut))


def erase(im, box):
    px = im.load()
    for y in range(box[1], min(box[3], im.height)):
        for x in range(box[0], min(box[2], im.width)):
            px[x, y] = (0, 0, 0, 0)
    return im


def finish(name, im):
    """Quantized art -> the picture that ships: ground off, handle off, seated.

    THE ORDER IS THE CONTRACT. The erase boxes were measured on art that had already
    been trimmed to its ink and had its floor taken off, so the trims happen FIRST and
    the box coordinates mean what they meant on the ruler. Trimming last as well is
    what closes the hole the handle leaves at the top.
    """
    im = seat(im)
    if name in CLEANUP:
        im = seat(strip_ground(im))
        box = CLEANUP[name]
        if box:
            im = erase(im, box)
    return seat(im)


def preview():
    s = load()
    tiles = []
    for key in JOBS:
        e = s.get(key) or {}
        if not e.get('png'):
            print('  %s not landed yet' % key)
            continue
        im = finish(key, quantize(Image.open(os.path.join(HERE, e['png']))))
        k = 3 if im.width <= 140 else 2
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
        im = finish(name, quantize(Image.open(os.path.join(HERE, e['png']))))
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
