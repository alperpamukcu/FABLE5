# -*- coding: utf-8 -*-
"""VARIANTS FOR THE UPGRADE LADDERS, FOR THE AUTHOR TO PICK FROM (2026-09-09).

The author: "arkadaki flamingo tablolarının alternatiflerini üret upgradeler için, üretip
oyuna eklemeden önce benden onay almak için bana ön izleme sun. Aynısını mevcut son seviye
masa ve tezgahın varyantlarını üret, aynı perspektifte aynı yapıya benzer olsunlar."

So: three alternative wall pictures for the wall_center ladder, three variants of the top
table set, and three of the counter — each in the room's own perspective and palette, each
built on the piece that is already in the game.

NOTHING SHIPS FROM HERE. The takes land in Tools/room_variants/ and a contact sheet is
written beside them; only after the author picks does anything reach Assets (the house rule,
memory bottle-art-v3-respec).

  py -3 -X utf8 Tools/room_variants_gen.py take       submit / collect
  py -3 -X utf8 Tools/room_variants_gen.py sheet      the contact sheet
"""
import io
import json
import os
import re
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(HERE, 'room_variants')
STATE = os.path.join(OUT, 'state.json')
sys.path.insert(0, HERE)
import pixellab                                   # noqa: E402

# The one sentence every piece in this room shares — the same agreement room_furniture_gen
# glues onto its jobs, so a variant cannot drift into another game's art.
ROOM = ('pixel art, seen straight from the front at a low eye level, warm 1980s Miami bar '
        'interior palette, magenta and teal rim light on dark wood and brass, flat colours '
        'with hard pixel edges, no dithering, transparent background, no floor, no shadow, '
        'no text, no watermark')

FRAMED = ('three framed pictures hanging in a row with a small gap between them, thin brass '
          'frames, the same size and the same height, ')

TABLE = ('a round bar table with one bar stool standing at each side of it, three objects in '
         'a row, the tabletop showing as a thin shallow ellipse, ')

COUNTER = ('a long bar counter seen straight from the front, the whole width of the picture, '
           'a flat top edge with a thin bright rim light running along it, a panelled front, '
           'a plinth at the floor, ')

JOBS = {
    # ── the wall's picture ladder (fx_triptych is 170x80) ──────────────────────
    'art_flamingo_neon': (170, 80, 101,
        FRAMED + 'each picture a neon pink flamingo standing on one leg against a deep '
        'violet night, the middle one larger, ' + ROOM),
    'art_palms': (170, 80, 103,
        FRAMED + 'each picture a sunset over palm trees, orange and magenta bands of sky, '
        'black palm silhouettes, ' + ROOM),
    'art_city': (170, 80, 107,
        FRAMED + 'each picture a night skyline of a coastal city with lit windows and a '
        'teal horizon, ' + ROOM),

    # ── the top table set (fx_table_t3 is 118x71; drawn at 132x78) ─────────────
    'table_v1': (132, 78, 211,
        TABLE + 'a black marble top on a fluted brass column with a round brass foot, two '
        'oxblood leather stools with brass footrails, ' + ROOM),
    'table_v2': (132, 78, 213,
        TABLE + 'a teal terrazzo top on a chrome column with a wide chrome disc foot, two '
        'teal vinyl gas-lift stools, ' + ROOM),
    'table_v3': (132, 78, 217,
        TABLE + 'a smoked glass top on a pair of crossed chrome legs, two clear acrylic '
        'stools with magenta cushions, ' + ROOM),

    # ── the counter (Assets/Art/Backgrounds/counter.png is 638x250) ────────────
    'counter_v1': (320, 126, 311,
        COUNTER + 'dark walnut panels with brass inlay strips, a black stone top, ' + ROOM),
    'counter_v2': (320, 126, 313,
        COUNTER + 'glossy oxblood lacquer panels with a chrome kick rail, a white marble '
        'top, ' + ROOM),
    'counter_v3': (320, 126, 317,
        COUNTER + 'deep teal panels with vertical fluting and brass feet, a plum terrazzo '
        'top, ' + ROOM),
}


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
            'no_background': True, 'seed': seed})
        if images:
            io.open(png, 'wb').write(images[0])
            print('  ->', key)
            continue
        m = re.search(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}', text, re.I)
        s.setdefault(key, {})['job'] = m.group(0) if m else None
        _save(s)
        print('  queued %s %s' % (key, m.group(0) if m else text.strip()[:80]))


def sheet():
    rows = [('WALL ART · now: fx_triptych', 'Assets/Resources/Fixtures/fx_triptych.png',
             ['art_flamingo_neon', 'art_palms', 'art_city'], 3),
            ('TOP TABLE · now: fx_table_t3', 'Assets/Resources/Fixtures/fx_table_t3.png',
             ['table_v1', 'table_v2', 'table_v3'], 3),
            ('COUNTER · now: counter.png', 'Assets/Art/Backgrounds/counter.png',
             ['counter_v1', 'counter_v2', 'counter_v3'], 2)]
    tiles = []
    for title, current, keys, k in rows:
        band = [Image.open(os.path.join(ROOT, current)).convert('RGBA')]
        for key in keys:
            p = os.path.join(OUT, key + '.png')
            band.append(Image.open(p).convert('RGBA') if os.path.exists(p) else None)
        tiles.append((title, band, k))
    W = 40 + max(sum((im.width if im else 60) * k + 16 for im in band) for _, band, k in tiles)
    H = 40 + sum(max((im.height if im else 40) * k for im in band) + 34 for _, band, k in tiles)
    sh = Image.new('RGBA', (W, H), (36, 22, 42, 255))
    y = 20
    for title, band, k in tiles:
        x = 20
        tall = 0
        for im in band:
            if im is None:
                continue
            big = im.resize((im.width * k, im.height * k), Image.NEAREST)
            sh.alpha_composite(big, (x, y + 18))
            x += big.width + 16
            tall = max(tall, big.height)
        y += tall + 34
    p = os.path.join(HERE, 'room_variants_preview.png')
    sh.save(p)
    print('sheet ->', p)


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'take'
    if cmd == 'sheet':
        sheet()
    else:
        take(sys.argv[2:] or None)
