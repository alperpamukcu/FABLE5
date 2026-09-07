# -*- coding: utf-8 -*-
"""THE NINTH LIST'S GENERATED PIECES (2026-09-06): the licence band's beach panorama, the
settings key's cog icon in the star3d family, and the glasses' upgrade dress - the same
generated base glass EDITED (edit_image) and then cut back to the base's own silhouette, so
the cavity table keeps applying (dress never moves the cavity, GlassArt.FromGenerated).

  py -3 -X utf8 Tools/ninth_art_gen.py take [band cog tiers] [--only rocks_t2 ...]
  py -3 -X utf8 Tools/ninth_art_gen.py fetch
  py -3 -X utf8 Tools/ninth_art_gen.py sheet
  py -3 -X utf8 Tools/ninth_art_gen.py ship [band cog tiers]

Nothing enters Assets except through `ship`, after the sheet has been looked at.
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
STATE = os.path.join(HERE, 'ninth_art_state.json')
STAGING = os.path.join(HERE, 'AssetPipeline', 'staging', 'ninth')

GLASSES = ['rocks', 'pint', 'highball', 'martini', 'coupe']
# The ladder (GDD 23 s8): tier 1 is the base set; 2..5 are the 1-star..4-star dress, 6 the legendary.
TIERS = {
    2: 'the same glass polished to clear bright crystal, less frosted, a crisper white highlight down the wall and a brighter rim',
    3: 'the same glass in cut crystal: a band of diamond-cut facets around the lower body catching the light, clear glass above',
    4: 'the same glass with a thin polished silver metal band around the rim and a silver-capped foot, clear crystal body',
    5: 'the same glass with a bright gold metal band around the rim, fine gold etched lines around the body, clear crystal',
    6: 'the same glass in smoked black crystal with a thick gold rim and a gold foot, luxurious and dark, a faint warm glow in the glass',
}
TIER_TAIL = ('. Keep exactly the same shape, size, outline and viewpoint as the input image, the '
             'same empty glass on a transparent background, pixel art, hard pixel edges, one '
             'pixel dark outline, no liquid, no ice, no text.')

BAND = ('A very wide pixel art panorama strip of a Miami beach at golden hour, seen from the '
        'shore: a warm gradient sky from orange at the horizon to violet at the top, a low '
        'sun half set on the sea, the calm ocean in teal and blue with a few thin white wave '
        'lines, a strip of pale sand along the bottom, two dark palm tree silhouettes leaning '
        'in from the right edge, a tiny sailboat far out. Flat side view, no characters, no '
        'text, no border, hard pixel edges, no anti-aliasing, fills the whole canvas.')

COG = ('A single shiny 3D gold gear cog icon, pixel art, seen straight on, eight rounded teeth, '
       'a round hole in the middle, bevelled gold metal with a bright highlight on the upper '
       'left and a dark shade on the lower right, one pixel dark outline, centred, transparent '
       'background, no text.')


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
    print('  %-14s landed %dx%d -> %s' % (key, im.width, im.height, p))


def _queue(key, tool, args):
    s = load()
    e = s.get(key) or {}
    if e.get('png'):
        print('  %-14s already landed' % key)
        return
    if e.get('job_id'):
        print('  %-14s already queued (%s) - fetch it' % (key, e['job_id']))
        return
    text, images = call(tool, args)
    if images:
        _keep(key, images[0])
        return
    m = re.search(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}', text, re.I)
    s = load()
    s.setdefault(key, {})['job_id'] = m.group(0) if m else None
    s[key]['tool'] = tool
    save(s)
    print('  %-14s queued %s' % (key, m.group(0) if m else text.strip()[:300]))


def take(families, only=None):
    if 'band' in families:
        _queue('band', 'create_image_pro', {
            'description': BAND, 'width': 200, 'height': 32, 'no_background': False, 'seed': 7})
    if 'cog' in families:
        star = os.path.join(ITEMS, 'star3d.png')
        args = {'description': COG, 'width': 32, 'height': 32, 'no_background': True, 'seed': 3}
        if os.path.exists(star):
            args['style_image_base64'] = b64(star)
            args['style_copy'] = ['color_palette', 'outline', 'shading']
        _queue('cog', 'create_image_pro', args)
    if 'tiers' in families:
        for g in GLASSES:
            base = os.path.join(ITEMS, 'glass3d_%s.png' % g)
            if not os.path.exists(base):
                print('  no base for', g)
                continue
            im = Image.open(base)
            for t, what in TIERS.items():
                key = '%s_t%d' % (g, t)
                if only and key not in only:
                    continue
                _queue(key, 'edit_image', {
                    'images_base64': [b64(base)], 'description': what + TIER_TAIL,
                    'width': im.width, 'height': im.height, 'no_background': True,
                    'seed': 40 + t})


def fetch():
    s = load()
    for key, e in list(s.items()):
        if e.get('png') or not e.get('job_id'):
            continue
        text, images = call('get_image', {'job_id': e['job_id']})
        if images:
            _keep(key, images[0])
        else:
            print('  %-14s cooking: %s' % (key, (text.strip().splitlines() or [''])[0][:120]))


def sheet():
    s = load()
    ims = [(k, Image.open(os.path.join(HERE, e['png'])).convert('RGBA'))
           for k, e in sorted(s.items()) if e.get('png')]
    if not ims:
        print('nothing landed yet')
        return
    k = 3
    cols = 6
    cw = max(im.width for _, im in ims) * k + 12
    ch = max(im.height for _, im in ims) * k + 24
    rows = (len(ims) + cols - 1) // cols
    out = Image.new('RGBA', (cols * cw + 12, rows * ch + 12), (36, 24, 48, 255))
    for i, (key, im) in enumerate(ims):
        x = 12 + (i % cols) * cw
        y = 12 + (i // cols) * ch
        out.alpha_composite(im.resize((im.width * k, im.height * k), Image.NEAREST), (x, y))
    p = os.path.join(HERE, 'ninth_art_preview.png')
    out.save(p)
    print('sheet', p, [k for k, _ in ims])


def ship(families):
    s = load()
    if 'tiers' in families:
        for g in GLASSES:
            base_p = os.path.join(ITEMS, 'glass3d_%s.png' % g)
            base = Image.open(base_p).convert('RGBA')
            for t in TIERS:
                key = '%s_t%d' % (g, t)
                e = s.get(key) or {}
                if not e.get('png'):
                    continue
                im = Image.open(os.path.join(HERE, e['png'])).convert('RGBA')
                # THE BASE WAS RE-CENTRED ON ITS CAVITY AFTER THESE WERE EDITED (glass3d_ship.py,
                # 2026-09-07): the dress is cut with the same box before it is cut to the base.
                geo = json.load(io.open(os.path.join(HERE, 'glass3d_gen3d.json'), encoding='utf-8')).get(g) or {}
                crop = geo.get('crop')
                if crop and im.width >= crop[1]:
                    im = im.crop((crop[0], 0, crop[1], im.height))
                if im.size != base.size:
                    im = im.resize(base.size, Image.NEAREST)
                # CUT TO THE BASE'S SILHOUETTE: the dress may not move the cavity.
                r, gg, b, _ = im.split()
                out = Image.merge('RGBA', (r, gg, b, base.split()[3]))
                p = os.path.join(ITEMS, 'glass3d_%s_t%d.png' % (g, t))
                out.save(p)
                print('  shipped', p)
    if 'cog' in families and (s.get('cog') or {}).get('png'):
        im = Image.open(os.path.join(HERE, s['cog']['png'])).convert('RGBA')
        p = os.path.join(ITEMS, 'cog3d.png')
        im.save(p)
        print('  shipped', p)
    # The 16-row take (band16) is the one the band wears: 200x16 art px is the band's own
    # size at 3x, no crop and no resample. The 32-row take stays in staging as the study.
    band_key = 'band16' if (s.get('band16') or {}).get('png') else 'band'
    if 'band' in families and (s.get(band_key) or {}).get('png'):
        im = Image.open(os.path.join(HERE, s[band_key]['png'])).convert('RGBA')
        p = os.path.join(ITEMS, 'licence_band.png')
        im.save(p)
        print('  shipped', p)


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'take'
    rest = sys.argv[2:]
    only = None
    if '--only' in rest:
        i = rest.index('--only')
        only = rest[i + 1:]
        rest = rest[:i]
    fams = rest or ['band', 'cog', 'tiers']
    if cmd == 'take':
        take(fams, only)
    elif cmd == 'fetch':
        fetch()
    elif cmd == 'sheet':
        sheet()
    elif cmd == 'ship':
        ship(fams)
    else:
        raise SystemExit(__doc__)
