# -*- coding: utf-8 -*-
"""The CELLAR copy is GENERATED at its own size (32x64), never shrunk (plan v4 decision 2).

Round six's algorithmic redraw (coverage silhouette + flat body + measured label block) came
out skewed and lifeless in the room — the author, 2026-09-04: "şişeler yamık ve kaliteleri
çok düşük, üstlerinde etiket yok". A pixel artist draws each size by hand; so does the
generator. Fidelity to the master comes from img2img: the init image is the master itself
(body restored) box-filtered to 32x64 — proportions, perspective, colours and the label's
position all come from it — and PixelLab redraws it as clean pixels at that size with the
55-colour palette forced. The outline pass and the liquid plates come after (process.py).

  py -3 -X utf8 Tools/v4_bottles/cellar_gen.py pilot  <card> [<card>...]   # strengths 200/300
  py -3 -X utf8 Tools/v4_bottles/cellar_gen.py all                          # one strength
"""
import base64
import io
import json
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
from PIL import Image                      # noqa: E402
import brief                               # noqa: E402
import gen                                 # noqa: E402
import process                             # noqa: E402

CW, CH = 32, 64
STRENGTH = 250
PALETTE_PNG = os.path.join(HERE, 'palette55.png')
STATE = os.path.join(HERE, 'cellar_gen_state.json')


def init_image(card_id, seed=23):
    """The master (centred, body restored) box-filtered to 32x64, alpha hardened."""
    raw = Image.open(os.path.join(gen.RAW, card_id, 's%d.png' % seed)).convert('RGBA')
    im = process.centre(raw)
    im, _ = process.restore_body(im, card_id)
    small = im.resize((CW, CH), Image.BOX)
    px = small.load()
    for y in range(CH):
        for x in range(CW):
            r, g, b, a = px[x, y]
            px[x, y] = (r, g, b, 255) if a >= 96 else (0, 0, 0, 0)
    return small


def describe(card_id):
    fam, ratio, look, label_ramp, band_ramp, emblem = brief.CARDS[card_id]
    word = brief.BRAND_WORD.get(card_id, card_id.split('_')[-1]).upper()
    vessel = {'can': 'aluminium drink can', 'carton': 'gable-top juice carton',
              'beer': 'beer bottle with a crown cap'}.get(fam, 'glass bottle with its cap on')
    return ('tiny 32x64 pixel art sprite of the same %s as the input image, %s, front view, '
            'slight top-down camera, one pixel black outline, flat matte colours, very little '
            'shine, a clearly visible brand label on the body with a simple emblem, '
            'clean crisp pixels, transparent background' % (vessel, look))


def _b64(im):
    buf = io.BytesIO(); im.save(buf, 'PNG'); return base64.b64encode(buf.getvalue()).decode('ascii')


def take(card_id, strength=STRENGTH, seed=23, tag=None):
    init = init_image(card_id, seed)
    args = {'description': describe(card_id), 'width': CW, 'height': CH,
            'no_background': True, 'init_image_base64': _b64(init),
            'init_image_strength': int(strength), 'seed': int(seed),
            'color_image_base64': gen._b64(PALETTE_PNG),
            'view': 'low top-down', 'outline': 'single color black outline',
            'shading': 'flat shading', 'detail': 'low detail'}
    t0 = time.time()
    text, msgs = gen._call('create_image_pixflux', args, timeout=600)
    imgs = gen._images_from(text, msgs)
    out_dir = os.path.join(gen.RAW, card_id)
    os.makedirs(out_dir, exist_ok=True)
    init.save(os.path.join(out_dir, 'c_init.png'))
    tag = tag or ('c_s%d' % strength)
    if not imgs:
        jid = gen._job_id(text, msgs)
        if jid:
            for _ in range(60):
                time.sleep(5)
                t2, m2 = gen._call('get_image', {'job_id': jid}, timeout=120)
                imgs = gen._images_from(t2, m2)
                if imgs:
                    break
    if not imgs:
        print('  !! %s: %s' % (card_id, text[:200].replace('\n', ' ')))
        return None
    path = os.path.join(out_dir, tag + '.png')
    io.open(path, 'wb').write(imgs[0])
    im = Image.open(path)
    print('  -> %-18s %s %dx%d  %.0fs' % (card_id, tag, im.width, im.height, time.time() - t0))
    return path


def balance():
    text, _ = gen._call('get_balance', {})
    for line in text.splitlines():
        if 'generations_used' in line:
            return line.strip()
    return text[:120]


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'pilot'
    if cmd == 'pilot':
        ids = sys.argv[2:] or ['vodka_astra', 'cola_marlow', 'orange_grove', 'beer_kestrel']
        print('before:', balance())
        for cid in ids:
            for s in (200, 300):
                take(cid, s)
        print('after: ', balance())
    elif cmd == 'all':
        ids = [c for c in brief.CARDS if not os.path.exists(os.path.join(gen.RAW, c, 'c_s%d.png' % STRENGTH))]
        print('%d cards to take' % len(ids))
        for cid in ids:
            take(cid, STRENGTH)
        print('after: ', balance())
