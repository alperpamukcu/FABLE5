# -*- coding: utf-8 -*-
"""THE BENCH REWORK'S PROPS (2026-08-25, the author: "Pour ve shakera alkol koyma
sahnelerini yenileyeceğiz... kaşık için uygun bir görsel üretilecek, mevcut ana
sahnede kullandığımız bira musluklarının büyük versiyonu üretilecek... tuz, limon,
şeker, buz gibi şeyler güncellenecek... tezgah boyuna oranlı görseller üretilecek").

One take per asset — the author's own brief ("görselden birçok adet üretme") — through
the standard quantize chain. Ten pieces:

  spoon        the bar spoon, at last a drawing instead of three grey rectangles
  tap_big      the room's own single-font draught tower, grown for the tap bench
  dish_salt    a rimming PLATE — wide and shallow, because the new skill turns the
  dish_sugar   glass in it; the old cellar was a thing you could not turn anything in
  bucket_ice   an open bucket with the cubes on show, reached into over and over
  bowl_lemon   the wedges, in a bowl that reads at arm's length
  mini_*       the same four stations at counter scale, for the room's own counter —
               the safety net for a drink served before it was finished

  py -3 Tools/bench_props_gen.py take | preview | ship [names...]
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
STATE = os.path.join(HERE, 'bench_props_state.json')
STAGING = os.path.join(HERE, 'AssetPipeline', 'staging', 'bench_props')
PREVIEW = os.path.join(HERE, 'bench_props_preview.png')
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Items')

# name -> (w, h, seed, description)
JOBS = {
    'spoon': (32, 128, 7, (
        'pixel art long bar spoon standing vertical, twisted spiral stainless steel '
        'stem, small oval bowl at the bottom, teardrop weight at the top, thin and '
        'elegant, single object centered, transparent background, no text')),
    'tap_big': (120, 240, 11, (
        'pixel art brass beer tap tower on a round base, tall polished brass column, '
        'single chrome faucet spout pointing left, black ball pull handle on top, '
        'art deco style, warm amber metal with dark reflections, single object '
        'centered, transparent background, no text')),
    'dish_salt': (80, 40, 11, (
        'pixel art wide shallow round dish seen from a low angle, filled with fine '
        'white salt, a circular groove pressed into the salt, dark ceramic plate, '
        'single object centered, transparent background, no text')),
    'dish_sugar': (80, 40, 23, (
        'pixel art wide shallow round dish seen from a low angle, filled with pale '
        'golden sugar crystals, a circular groove pressed into the sugar, dark '
        'ceramic plate, single object centered, transparent background, no text')),
    'bucket_ice': (72, 64, 11, (
        'pixel art open stainless steel ice bucket seen from a low angle, clear ice '
        'cubes heaped over the rim, small metal scoop leaning in it, cold blue '
        'highlights, single object centered, transparent background, no text')),
    'bowl_lemon': (72, 56, 11, (
        'pixel art dark bowl full of fresh lemon wedges, bright yellow wedges with '
        'white pith, one wedge resting against the rim, single object centered, '
        'transparent background, no text')),
    'mini_ice': (32, 32, 5, (
        'tiny pixel art open steel ice bucket with ice cubes over the rim, simple, '
        'single object centered, transparent background, no text')),
    'mini_lemon': (32, 32, 5, (
        'tiny pixel art dark bowl of yellow lemon wedges, simple, single object '
        'centered, transparent background, no text')),
    'mini_salt': (32, 32, 5, (
        'tiny pixel art shallow dish of white salt, simple, single object centered, '
        'transparent background, no text')),
    'mini_sugar': (32, 32, 9, (
        'tiny pixel art shallow dish of pale golden sugar, simple, single object '
        'centered, transparent background, no text')),
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


def take():
    s = load()
    for key, (w, h, seed, desc) in JOBS.items():
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


def preview():
    s = load()
    tiles = []
    for key in JOBS:
        e = s.get(key) or {}
        if not e.get('png'):
            print('  %s not landed yet' % key)
            continue
        im = quantize(Image.open(os.path.join(HERE, e['png'])))
        k = 4 if im.width <= 40 else 2
        tiles.append((key, im.resize((im.width * k, im.height * k), Image.NEAREST)))
    if not tiles:
        return
    w = max(t.width for _, t in tiles) + 16
    h = sum(t.height + 20 for _, t in tiles)
    sheet = Image.new('RGBA', (w, h), (88, 88, 100, 255))
    y = 0
    for key, t in tiles:
        sheet.paste(t, (8, y + 12), t)
        y += t.height + 20
    sheet.save(PREVIEW)
    print('wrote', PREVIEW)


def ship(names):
    s = load()
    for name in names:
        e = s.get(name) or {}
        if not e.get('png'):
            print('  %s has not landed' % name)
            continue
        im = quantize(Image.open(os.path.join(HERE, e['png'])))
        dest = os.path.join(OUT, 'bench_%s.png' % name)
        im.save(dest)
        print('  %s -> %s (%dx%d)' % (name, dest, im.width, im.height))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'take'
    if cmd == 'take':
        take()
    elif cmd == 'preview':
        preview()
    elif cmd == 'ship':
        ship(sys.argv[2:] if len(sys.argv) > 2 else list(JOBS))
    else:
        print(__doc__)
