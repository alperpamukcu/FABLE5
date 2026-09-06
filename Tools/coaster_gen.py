# -*- coding: utf-8 -*-
"""THE COASTERS (2026-09-06, the author: "artık yeni müşteri geldiğinde siparişi alındıktan
sonra önünde kare bardak altlığı belirecek, bardak altlığı çok dikkat çekici olmamalı bunun
görselini üretebilirsin pixellabden, birkaç adet aynı boyda farklı görsellikte bardak altlığı
olabilir, boyut aynı olmalı ama duruşu farklı olabilir, kimi yamuk durucak, kimi dik, duruş
pozisyonları rastgele olacak").

Four square mats, all 32x32, all quiet: the point is that a stool with an order on it looks
DIFFERENT from an empty one, not that the counter grows a feature. The tilt is the room's, not
the drawing's — the stage turns each one a few degrees on the seat's own hash, which is what
makes four mats look like a night's worth.

  py -3 Tools/coaster_gen.py take | preview | ship [names...]
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
STATE = os.path.join(HERE, 'coaster_state.json')
STAGING = os.path.join(HERE, 'AssetPipeline', 'staging', 'coasters')
PREVIEW = os.path.join(HERE, 'coaster_preview.png')
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Items')

COMMON = ('square drink coaster seen from a low angle on a dark bar, flat and thin, '
          'plain and unremarkable, no writing, no logo, single object centered, '
          'transparent background, no text')

# name -> (w, h, seed, description)
JOBS = {
    'coaster_a': (32, 32, 21, 'pixel art ' + COMMON + ', brown pulp cardboard beer mat, '
                              'slightly frayed edge, matte'),
    'coaster_b': (32, 32, 34, 'pixel art ' + COMMON + ', pale cork coaster with speckled grain, '
                              'rounded corners'),
    'coaster_c': (32, 32, 47, 'pixel art ' + COMMON + ', dark slate stone coaster, faint '
                              'lighter veining, sharp corners'),
    'coaster_d': (32, 32, 58, 'pixel art ' + COMMON + ', deep green felt coaster with a thin '
                              'darker border stitched around the edge'),
}

# The house palette, exactly as every other generator quantizes to it.
RAMPS = [
    (0x0D0813, 0x1A1024, 0x2A1B3D, 0x3E2A57, 0x553C73),   # Night
    (0x2B1220, 0x4A1B34, 0x6E2547, 0x9B315E, 0xC94F80),   # Vice
    (0x1B2A2A, 0x24413F, 0x2F5A56, 0x3D7A72, 0x57A79B),   # Deep
    (0x3D2A12, 0x5E4220, 0x855E2C, 0xB0813E, 0xD9A757),   # Amber
    (0x11333A, 0x14505C, 0x1A727F, 0x24A0AF, 0x57D6E3),   # Cyan
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
        tiles.append((key, im.resize((im.width * 6, im.height * 6), Image.NEAREST)))
    if not tiles:
        return
    w = sum(t.width + 16 for _, t in tiles) + 16
    h = max(t.height for _, t in tiles) + 24
    sheet = Image.new('RGBA', (w, h), (26, 16, 36, 255))     # the counter's own dark
    x = 16
    for key, t in tiles:
        sheet.paste(t, (x, 12), t)
        x += t.width + 16
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
        dest = os.path.join(OUT, '%s.png' % name)
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
