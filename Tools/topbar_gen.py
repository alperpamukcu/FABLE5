# -*- coding: utf-8 -*-
"""The top strip's two generated pieces (2026-08-19, the author):

  - the weekly calendar's BACKPLATE ("Haftalık takvim için pixellabden arkaplan
    oluştur") — the one written exception to "UI chrome is never generated",
    licensed by that sentence and recorded in 16 §0 when it ships;
  - the 3D STAR icon for the standing row ("Yıldızlarda 3 boyutlu yıldız
    iconlarından olsun") — illustrative content, PixelLab's default lane.

Both are generated AT the size they draw at (backplate 2x, star 1x on the 1280
canvas), with the palette swatch and a shipped sprite as style references, and
quantized to the palette in post. Day names and the week number are NOT in the
art - PixelLab cannot write text and the code draws them on top.

    python Tools/topbar_gen.py queue    # start the jobs
    python Tools/topbar_gen.py fetch    # collect finished takes into Tools/topbar_raw
    python Tools/topbar_gen.py post     # quantize + trim -> candidates + contact sheet
"""
import base64, io, json, os, re, sys, time

from PIL import Image

import pixellab

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
RAW = os.path.join(HERE, 'topbar_raw')
STATE = os.path.join(HERE, 'topbar_state.json')
UUID = re.compile(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}')

PALETTE_SWATCH = os.path.join(HERE, 'palette_miami.png')
STYLE_REF = os.path.join(ROOT, 'Assets', 'Resources', 'Items', 'v3_bourbon_redline_flat.png')


def b64file(path):
    with io.open(path, 'rb') as f:
        return base64.b64encode(f.read()).decode('ascii')


def refs():
    return json.dumps([
        {'base64': b64file(PALETTE_SWATCH),
         'usage': 'colour palette: use ONLY these colours'},
        {'base64': b64file(STYLE_REF),
         'usage': 'pixel art rendering style: hard 1px edges, flat colour runs, '
                  'crisp bevels, no blur, no anti-aliasing'},
    ])


# The backplate is asked for PLAIN - no slots, no dividers, no lamps - because the
# code stands the lamps, the star fitting, the shutter and the letters on top at
# exact positions; art that guessed at seven cells would fight the grid drawn over
# it. What the art owns is the BODY: material, edge, ends.
# Take 2 (2026-08-19): take 1 asked for a panel on a transparent background at 224x32
# and the model answered with a thin brass LINE in an empty frame - at that aspect,
# "panel" plus alpha reads as "draw me an object", and the object it chose was a rule.
# The fix is to stop asking for an object at all: no_background=False and "the entire
# canvas IS the plate surface" - texture generation, not object generation.
PLATE = ('the entire canvas is the flat face of a dark metal signboard plate, filling '
         '100 percent of the image edge to edge and top to bottom: dark navy blue '
         'brushed metal surface with faint horizontal brushing, a 2 pixel brass border '
         'frame running along all four edges of the image, one small round rivet in '
         'each corner, plain empty centre, subtle darker vignette near the edges, flat '
         'matte pixel art, no text, no letters, no symbols, no slots, no dividers, no '
         'glare, seen straight on')

STAR = ('one single five-pointed star icon, glossy polished gold, chunky 3D bevel: '
        'each point is a raised facet with a bright top-left face and a dark amber '
        'bottom-right face, one small white specular glint near the top left point, '
        'dark bronze 1px outline, centered, plain transparent background, video game '
        'rating star, pixel art')

ASSETS = {
    'plate2_a': dict(tool='create_image_pro', seed=52121,
        args=dict(width=224, height=32, no_background=False, description=PLATE)),
    'plate2_b': dict(tool='create_image_pro', seed=52122,
        args=dict(width=224, height=32, no_background=False,
                  description=PLATE.replace('dark navy blue brushed metal surface',
                                            'deep aubergine-charcoal lacquered panel'))),
    'plate2_c': dict(tool='create_image_pro', seed=52123,
        args=dict(width=224, height=32, no_background=False,
                  description=PLATE.replace('brass border frame', 'teal neon tube border frame'))),
    'star_a': dict(tool='create_image_pro', seed=52111,
        args=dict(width=32, height=32, no_background=True, description=STAR)),
    'star_b': dict(tool='create_image_pro', seed=52112,
        args=dict(width=32, height=32, no_background=True, description=STAR)),
    'star_c': dict(tool='create_image_pro', seed=52113,
        args=dict(width=32, height=32, no_background=True, description=STAR)),
    'star_d': dict(tool='create_image_pro', seed=52114,
        args=dict(width=32, height=32, no_background=True, description=STAR)),
}


def load():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def save(st):
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(st, indent=1))


def texts(msgs):
    out = []
    for m in msgs:
        for c in ((m.get('result') or {}).get('content') or []):
            if c.get('type') == 'text':
                out.append(c['text'])
    return '\n'.join(out)


def images(msgs):
    out = []
    for m in msgs:
        for c in ((m.get('result') or {}).get('content') or []):
            if c.get('type') == 'image':
                out.append(Image.open(io.BytesIO(base64.b64decode(c['data']))).convert('RGBA'))
    return out


def queue(only=None):
    st = load()
    for key, a in ASSETS.items():
        if only and key not in only:
            continue
        if st.get(key, {}).get('id'):
            print('%-8s already queued -> %s' % (key, st[key]['id'][:8]))
            continue
        args = dict(a['args'], seed=a['seed'], reference_images=refs())
        msgs = pixellab.call(a['tool'], args, timeout=900)
        body = texts(msgs)
        m = UUID.search(body)
        st[key] = {'id': m.group(0) if m else None}
        save(st)
        print('%-8s -> %s' % (key, st[key]['id'] or body[:140].replace('\n', ' ')))
        time.sleep(0.6)


def fetch():
    os.makedirs(RAW, exist_ok=True)
    st = load()

    def pending():
        return {k: v for k, v in st.items() if v.get('id')
                and not os.path.exists(os.path.join(RAW, k + '.png'))}

    for _ in range(60):
        if not pending():
            break
        moved = False
        for key, rec in sorted(pending().items()):
            msgs = pixellab.call('get_image', {'job_id': rec['id']}, timeout=300)
            ims, body = images(msgs), texts(msgs)
            if ims:
                ims[0].save(os.path.join(RAW, key + '.png'))
                print('fetched %-8s %dx%d' % (key, ims[0].width, ims[0].height))
                moved = True
            elif 'failed' in body.lower():
                print('FAILED', key, body[:200].replace('\n', ' '))
                rec['id'] = None
                save(st)
                moved = True
        if pending() and not moved:
            print(' %d pending...' % len(pending()))
            time.sleep(20)
    print('missing:', sorted(pending()) if pending() else 'none')


# ── post: quantize to the palette, sheet for the eye ─────────────────────────

RAMPS = {   # UITheme's ramps, the ones these two pieces may use
    'Night': ('0D0813', '1A1023', '241830', '362447', '4A3160'),
    'Amber': ('4A2E14', '8F5A1E', 'C9822B', 'E8A33D', 'F5C97B'),
    'ClubBlue': ('131B3D', '1F2E66', '2E4699', '4467CC', '6E93F0'),
    'Cream': ('453E38', '6E6459', '9C8F80', 'C9BCA8', 'F2E8D5'),
    'Graphite': ('14161A', '24272D', '383D45', '545A64', '808893'),
    'Malt': ('3A2410', '6B4416', '9E6A1D', 'C98F2B', 'E6B959'),
}


def palette():
    out = []
    for ramp in RAMPS.values():
        for hx in ramp:
            out.append(tuple(int(hx[i:i + 2], 16) for i in (0, 2, 4)))
    return out


def quantize(im):
    pal = palette()
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a < 40:
                px[x, y] = (0, 0, 0, 0)
                continue
            best = min(pal, key=lambda c: (c[0] - r) ** 2 + (c[1] - g) ** 2 + (c[2] - b) ** 2)
            px[x, y] = best + (255,)
    return im


def post():
    sheet_cells = []
    for key in sorted(ASSETS):
        p = os.path.join(RAW, key + '.png')
        if not os.path.exists(p):
            continue
        im = quantize(Image.open(p).convert('RGBA'))
        out = os.path.join(RAW, key + '_q.png')
        im.save(out)
        colours = len(set(c for c in im.getdata() if c[3] > 0))
        print('%-8s %dx%d  %d colours -> %s' % (key, im.width, im.height, colours, out))
        sheet_cells.append((key, im))
    # Contact sheet at 3x on the fascia's own dark, so the takes are judged on the
    # ground they will stand on.
    if sheet_cells:
        k = 3
        W = max(im.width for _, im in sheet_cells) * k + 20
        H = sum(im.height * k + 26 for _, im in sheet_cells) + 10
        sheet = Image.new('RGBA', (W, H), (26, 16, 35, 255))
        y = 10
        for key, im in sheet_cells:
            big = im.resize((im.width * k, im.height * k), Image.NEAREST)
            sheet.alpha_composite(big, (10, y))
            y += big.height + 26
        sheet.save(os.path.join(RAW, 'sheet.png'))
        print('sheet ->', os.path.join(RAW, 'sheet.png'))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'queue'
    if cmd == 'queue':
        queue(sys.argv[2:] or None)
    elif cmd == 'fetch':
        fetch()
    elif cmd == 'post':
        post()
