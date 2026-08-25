# -*- coding: utf-8 -*-
"""THE DAY-PASS SCENE'S OWN ART (2026-08-25, the author: "kullanılan mevcut görsel
profesyonelce durmuyor, gerekirse görsel ve animasyonu üret ama ekran profesyonelce
dursun").

The first cut of the time-skip drew its city as random procedural boxes, which read
as programmer art next to the room's own window. This makes the scene's three
illustrative pieces — the skyline the day crosses, the sun and the moon that cross
it — generated at display size and QUANTIZED onto the house's 40 colours, the same
chain every generated image in this game ships through. The sky behind them stays
procedural: it is twenty palette-token bands driven by the hour, which is a reading
and not an illustration.

  py -3 Tools/day_sky_gen.py take        queue/collect every take
  py -3 Tools/day_sky_gen.py preview     quantize + write the contact sheet
  py -3 Tools/day_sky_gen.py ship city_a sun_a moon_a
                                         chosen takes -> Resources/Scene/curtain_*.png
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
STATE = os.path.join(HERE, 'day_sky_state.json')
STAGING = os.path.join(HERE, 'AssetPipeline', 'staging', 'day_sky')
PREVIEW = os.path.join(HERE, 'day_sky_preview.png')
OUT = {
    'city': os.path.join(ROOT, 'Assets', 'Resources', 'Scene', 'curtain_city.png'),
    'sun': os.path.join(ROOT, 'Assets', 'Resources', 'Scene', 'curtain_sun.png'),
    'moon': os.path.join(ROOT, 'Assets', 'Resources', 'Scene', 'curtain_moon.png'),
}

# The panel is 640x220 HUD units and every sprite in it draws at a whole 2x, so the
# art is made at exactly half the size it stands at — never resampled.
JOBS = {
    # The skyline fills the panel's lower half: towers, palms, and the bay in front,
    # everything DARK, because the sky behind it is the thing that carries the hour.
    'city_a': (320, 96, 101, (
        'wide pixel art silhouette of a miami city skyline at dusk seen across calm '
        'water, dark purple-black art deco towers with small lit amber windows, two '
        'palm trees leaning at the sides, thin water strip at the bottom with faint '
        'horizontal light reflections, flat dark silhouette against empty transparent '
        'sky, no sky, no clouds, no sun, no text')),
    'city_b': (320, 96, 137, None),   # same brief, second roll
    # The sun: a clean disc with a soft inner shade — its halo stays the game's own
    # drawn glow, so only the body is asked for.
    'sun_a': (32, 32, 11, (
        'pixel art sun disc, warm pale yellow center with soft orange rim shading, '
        'simple round sun, no rays, no face, single object centered, transparent '
        'background, no text')),
    'sun_b': (32, 32, 47, None),
    # The moon: a waning crescent with a hint of the dark limb, which is the drawing
    # the old two-disc bite trick was approximating.
    'moon_a': (24, 24, 11, (
        'pixel art crescent moon, pale cream crescent open to the left, faint dark '
        'blue shadowed limb, small, single object centered, transparent background, '
        'no text')),
    'moon_b': (24, 24, 47, None),
}
for k in list(JOBS):
    w, h, seed, desc = JOBS[k]
    if desc is None:
        JOBS[k] = (w, h, seed, JOBS[k[:-1] + 'a'][3])

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
                print('  %s still cooking: %s' % (key, (text.strip().splitlines() or [''])[0][:100]))
            continue
        text, images = call('create_image_pro', {
            'description': desc,
            'width': w, 'height': h,
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
    tiles = []
    for key in JOBS:
        e = s.get(key) or {}
        if not e.get('png'):
            print('  %s not landed yet' % key)
            continue
        im = quantize(Image.open(os.path.join(HERE, e['png'])))
        k = 2 if im.width > 100 else 4
        tiles.append((key, im.resize((im.width * k, im.height * k), Image.NEAREST)))
    if not tiles:
        return
    w = max(t.width for _, t in tiles) + 16
    h = sum(t.height + 24 for _, t in tiles)
    # Mid-grey ground, so both the dark towers and the pale sun read against it.
    sheet = Image.new('RGBA', (w, h), (90, 90, 100, 255))
    y = 0
    for key, t in tiles:
        sheet.paste(t, (8, y + 16), t)
        y += t.height + 24
        print('  %s at strip y %d' % (key, y))
    sheet.save(PREVIEW)
    print('wrote', PREVIEW)


def ship(picks):
    s = load()
    for pick in picks:
        kind = pick.split('_')[0]
        e = s.get(pick) or {}
        if not e.get('png'):
            print('  %s has not landed' % pick)
            continue
        im = quantize(Image.open(os.path.join(HERE, e['png'])))
        im.save(OUT[kind])
        print('  %s -> %s (%dx%d)' % (pick, OUT[kind], im.width, im.height))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'take'
    if cmd == 'take':
        take()
    elif cmd == 'preview':
        preview()
    elif cmd == 'ship':
        ship(sys.argv[2:])
    else:
        print(__doc__)
