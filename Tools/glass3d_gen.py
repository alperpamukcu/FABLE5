# -*- coding: utf-8 -*-
"""THE BASE GLASSES, GENERATED (2026-09-06, the author: "yeni bardaklar kötü ve 2d. 3d bardak
üret görsel olarak tasarımıza uygun olsun" — the drawn v2 set was sent back).

Five empty glasses off PixelLab's `create_image_pro`, one job each, on the same ground rules
as the shaker (Tools/shaker_gen.py): a NUMBER for the viewpoint, the palette copied from the
old generated glass so the set stays in the family, hard pixel edges, no background. The
description asks for what the author asked for — thick heavy walls, frosted low-transparency
glass, a real mouth — and the canvases are drawn so the glass lands near 1:1 on the counter
(92 units tall) and about 2.7x on the pour stage (260), which is where the old set's
40-something-pixel drawings went soft.

  py -3 -X utf8 Tools/glass3d_gen.py take [rocks pint ...]   queue what is not landed yet
  py -3 -X utf8 Tools/glass3d_gen.py fetch                    collect cooked jobs into staging
  py -3 -X utf8 Tools/glass3d_gen.py sheet                    a 3x contact sheet of the staging

Nothing enters Assets from here: Tools/glass3d_ship.py takes the staging, cuts the cavity
and writes the Items set (the memory: new assets are REPORTED first, shipped after a pick).
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
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
STATE = os.path.join(HERE, 'glass3d_state.json')
STAGING = os.path.join(HERE, 'AssetPipeline', 'staging', 'glass3d')

BODY = ('A single empty {what}, pixel art, {view}. Thick heavy glass walls, frosted '
        'low-transparency glass with a cool grey-blue tint, the far wall faintly visible '
        'through it, one soft vertical highlight down the left wall, a bright rim at the '
        'mouth, a solid thick base. No liquid, no ice, no garnish, no reflection on the '
        'ground. Hard pixel edges, no anti-aliasing, one pixel dark outline all the way '
        'round, centered, the whole glass visible with a small margin on every side, '
        'transparent background.')

# canvas (w, h) — multiples of four — and the description's two blanks
JOBS = {
    'pint':     ((56, 96), 'pint glass, a tall tumbler wider at the mouth than the foot',
                 'seen from slightly above (a 15 degree elevation) so the round mouth shows as a shallow ellipse'),
    'highball': ((44, 96), 'highball glass, a tall narrow straight-sided tumbler',
                 'seen from slightly above (a 15 degree elevation) so the round mouth shows as a shallow ellipse'),
    'rocks':    ((64, 72), 'rocks glass, a short wide old-fashioned tumbler with a very thick base',
                 'seen from slightly above (a 15 degree elevation) so the round mouth shows as a shallow ellipse'),
    'martini':  ((72, 88), 'martini glass, a wide cone bowl on a thin stem and a round foot',
                 'seen from slightly above (a 15 degree elevation) so the round mouth shows as a shallow ellipse'),
    'coupe':    ((68, 88), 'coupe glass, a shallow round bowl on a thin stem and a round foot',
                 'seen from slightly above (a 15 degree elevation) so the round mouth shows as a shallow ellipse'),
}
SEEDS = {'pint': 11, 'highball': 17, 'rocks': 23, 'martini': 29, 'coupe': 31}


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


def b64(path):
    return base64.b64encode(io.open(path, 'rb').read()).decode('ascii')


def _keep(key, im):
    os.makedirs(STAGING, exist_ok=True)
    p = os.path.join(STAGING, key + '.png')
    im.save(p)
    s = load()
    s.setdefault(key, {})['png'] = os.path.relpath(p, HERE).replace('\\', '/')
    save(s)
    print('  %-9s landed %dx%d -> %s' % (key, im.width, im.height, p))


def take(only=None):
    s = load()
    style = os.path.join(ITEMS, 'glass3d_rocks.png')      # the old set, for its palette and outline
    for key, ((w, h), what, view) in JOBS.items():
        if only and key not in only:
            continue
        e = s.get(key) or {}
        if e.get('png'):
            print('  %-9s already landed' % key)
            continue
        if e.get('job_id'):
            print('  %-9s already queued (%s) — fetch it' % (key, e['job_id']))
            continue
        desc = BODY.format(what=what, view=view)
        assert len(desc) <= 2000
        args = {'description': desc, 'width': w, 'height': h, 'no_background': True,
                'seed': SEEDS[key]}
        if os.path.exists(style):
            args['style_image_base64'] = b64(style)
            args['style_copy'] = ['color_palette', 'outline']
        text, images = call('create_image_pro', args)
        if images:
            _keep(key, images[0])
            continue
        m = re.search(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}', text, re.I)
        s = load()
        s.setdefault(key, {})['job_id'] = m.group(0) if m else None
        save(s)
        print('  %-9s queued %s' % (key, m.group(0) if m else text.strip()[:160]))


def fetch():
    s = load()
    for key, e in list(s.items()):
        if e.get('png') or not e.get('job_id'):
            continue
        text, images = call('get_image', {'job_id': e['job_id']})
        if images:
            _keep(key, images[0])
        else:
            print('  %-9s cooking: %s' % (key, (text.strip().splitlines() or [''])[0][:120]))


def sheet():
    s = load()
    ims = [(k, Image.open(os.path.join(HERE, e['png'])).convert('RGBA'))
           for k, e in s.items() if e.get('png')]
    if not ims:
        print('nothing landed yet')
        return
    k = 3
    W = sum(im.width * k + 12 for _, im in ims) + 12
    H = max(im.height for _, im in ims) * k + 24
    out = Image.new('RGBA', (W, H), (36, 24, 48, 255))
    x = 12
    for _, im in ims:
        out.alpha_composite(im.resize((im.width * k, im.height * k), Image.NEAREST), (x, 12))
        x += im.width * k + 12
    p = os.path.join(HERE, 'glass3d_preview.png')
    out.save(p)
    print('sheet', p, [k for k, _ in ims])


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'take'
    if cmd == 'take':
        take(sys.argv[2:] or None)
    elif cmd == 'fetch':
        fetch()
    elif cmd == 'sheet':
        sheet()
    else:
        raise SystemExit(__doc__)
