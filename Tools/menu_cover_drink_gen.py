# -*- coding: utf-8 -*-
"""THE TITLE PAGE'S OWN DRINK (2026-08-25, the author: "Giriş sayfasında kullanılan
bardak görseli için kendin güzel bir kokteyl görseli oluştur 1 adet").

The book's title plate borrowed glass3d_martini — a serving-glass sprite doing sign
work. This makes the sign: ONE illustrative cocktail, generated at its display size
(64 art px, drawn 2x on the page) and then QUANTIZED onto the house's 40-colour
palette, because a generated image ships in the room's own ink or not at all
(memory: pixellab-mcp-constraints — the quantize chain is not optional).

  py -3 Tools/menu_cover_drink_gen.py take        queue/collect the three seeds
  py -3 Tools/menu_cover_drink_gen.py preview     quantize + write the 6x strip
  py -3 Tools/menu_cover_drink_gen.py ship <n>    take n -> Resources/Items/menu_cover_drink.png
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
STATE = os.path.join(HERE, 'menu_cover_state.json')
STAGING = os.path.join(HERE, 'AssetPipeline', 'staging', 'menu_cover')
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Items', 'menu_cover_drink.png')
PREVIEW = os.path.join(HERE, 'menu_cover_preview_6x.png')

SIZE = 64
SEEDS = [11, 47, 83]

DESCRIPTION = (
    'one tropical cocktail in a tall hurricane glass, coral pink drink with an '
    'orange sunset gradient, a lime wheel on the rim, one red cherry, a small teal '
    'paper umbrella leaning out of the glass, two ice cubes visible through the '
    'glass, pixel art, clean silhouette, single object centered, no text'
)

# The 40-colour palette, straight out of UITheme (14 v3 §3) — every ramp, step 0 dark.
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


def take():
    s = load()
    for seed in SEEDS:
        key = 'seed%02d' % seed
        e = s.get(key) or {}
        if e.get('png'):
            print('  %s already landed' % key)
            continue
        if e.get('job_id'):
            text, images = call('get_image', {'job_id': e['job_id']})
            if images:
                _keep(key, images[0])
            else:
                print('  %s still cooking: %s' % (key, (text.strip().splitlines() or [''])[0][:100]))
            continue
        text, images = call('create_image_pro', {
            'description': DESCRIPTION,
            'width': SIZE, 'height': SIZE,
            'no_background': True,
            'seed': seed,
        })
        if images:
            _keep(key, images[0])
            continue
        m = re.search(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}', text, re.I)
        s = load()
        s.setdefault(key, {})['job_id'] = m.group(0) if m else None
        save(s)
        print('  %s queued %s' % (key, m.group(0) if m else text.strip()[:120]))


def quantize(im):
    """Every opaque pixel onto its nearest of the 40 house colours; alpha hardened
    to on/off, because the room's ink has no soft edges (16 §6.10)."""
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


def preview():
    s = load()
    keys = sorted(k for k in s if s[k].get('png'))
    if not keys:
        raise SystemExit('no takes landed yet — run: take')
    cells = []
    for k in keys:
        raw = Image.open(os.path.join(HERE, s[k]['png'])).convert('RGBA')
        q = quantize(raw.copy())
        qp = os.path.join(STAGING, k + '_q.png')
        q.save(qp)
        s[k]['quantized'] = os.path.relpath(qp, HERE)
        cells.append((k, raw, q))
    save(s)
    cw, ch, z = SIZE * 6, SIZE * 6, 6
    strip = Image.new('RGBA', (cw * len(cells), ch * 2 + 8), (26, 16, 35, 255))
    for i, (k, raw, q) in enumerate(cells):
        strip.paste(raw.resize((cw, ch), Image.NEAREST), (i * cw, 0))
        strip.paste(q.resize((cw, ch), Image.NEAREST), (i * cw, ch + 8))
    strip.save(PREVIEW)
    print('wrote', PREVIEW, 'takes:', ', '.join(keys), '(top raw, bottom quantized)')


def ship(key):
    s = load()
    e = s.get(key) or {}
    if not e.get('quantized'):
        raise SystemExit('take %s has no quantized png — run: preview' % key)
    im = Image.open(os.path.join(HERE, e['quantized'])).convert('RGBA')
    im.save(OUT)
    s['shipped'] = key
    save(s)
    print('shipped', key, '->', OUT)


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'take'
    if cmd == 'take':
        take()
    elif cmd == 'preview':
        preview()
    elif cmd == 'ship':
        ship(sys.argv[2])
    else:
        raise SystemExit('take | preview | ship <key>')
