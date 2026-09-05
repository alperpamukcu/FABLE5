# -*- coding: utf-8 -*-
"""THE SHAKER, REDRAWN (2026-09-04, the author: "Kullandigimiz shaker gorsellerini
yenile ... tum shakerlar ayni boyutta gozukmeli ... shakerdan bardaga icecek
koydugumuz sahnede shakerin ucundaki kapak acik olarak uretilmeli").

What stood here was three sheets whose halves did not agree with each other: a flat
dark cup for the tin and a bright chrome dome for the lid, as if the bench had been
handed two shakers from two different bars. This takes ONE closed cobbler shaker and
cuts every plate the game needs out of THAT drawing, so the tin, the lid and the lid
with its cap off are the same object by construction — the house rule for open states
(memory open-states-derive), which has already been paid for three times.

The sheet is 116x208 and its layout is FROZEN, because the bench's numbers are
measured off it: `CapArtOffset` (0.245) is the lid block's centre, `CavityFloor` /
`CavityRim` (0.0913 / 0.6106) are the drink's floor and rim inside the tin, and the
fluid's 28-row profile is the tin's own silhouette. `split` re-measures all of them
off whatever art it is given and prints them, so a new shaker that stands anywhere
else on the sheet is caught here rather than in play.

Four plates come out:

  shaker          the whole thing, shut — the fallback and the shelf shot
  tin_open        the body alone, its mouth open, 73px of air above it
  shaker_cap      the strainer dome with its little cap ON  (the draggable lid)
  shaker_cap_pour the same dome with the cap OFF and the spout open — what the
                  serve bench meets, because a cobbler pours through its strainer

  py -3 -X utf8 Tools/shaker_gen.py take            # queue / collect the candidates
  py -3 -X utf8 Tools/shaker_gen.py report          # the 3x HTML contact sheet
  py -3 -X utf8 Tools/shaker_gen.py split <key>     # cut the four plates
  py -3 -X utf8 Tools/shaker_gen.py ship <key>      # ...and put them in Resources
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
STATE = os.path.join(HERE, 'shaker_state.json')
STAGING = os.path.join(HERE, 'AssetPipeline', 'staging', 'shaker')
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
PALETTE_PNG = os.path.join(HERE, 'v4_bottles', 'palette55.png')

# The sheet the bench is measured against. Never changes.
SHEET = (116, 208)

# The house palette, as flat numbers (v4_bottles/palette.py is the same 55).
RAMPS = [
    (0x0D0813, 0x1A1023, 0x241830, 0x362447, 0x4A3160),   # Night
    (0x5C1B45, 0x8F2464, 0xC23283, 0xE84DA6, 0xFF7DC6),   # Magenta
    (0x123B45, 0x1B5F66, 0x26918F, 0x3BC8BE, 0x7DF0E3),   # Cyan
    (0x4A2E14, 0x8F5A1E, 0xC9822B, 0xE8A33D, 0xF5C97B),   # Amber
    (0x3D1220, 0x6E1B32, 0xA62B44, 0xD9455C, 0xF27D8A),   # ViceRed
    (0x131B3D, 0x1F2E66, 0x2E4699, 0x4467CC, 0x6E93F0),   # ClubBlue
    (0x16331B, 0x2A5926, 0x479938, 0x6FCC4B, 0xA8F077),   # Lime
    (0x453E38, 0x6E6459, 0x9C8F80, 0xC9BCA8, 0xF2E8D5),   # Cream
    (0x3A2410, 0x6B4416, 0x9E6A1D, 0xC98F2B, 0xE6B959),   # Malt
    (0x14161A, 0x24272D, 0x383D45, 0x545A64, 0x808893),   # Graphite
    (0x38161A, 0x5C2226, 0x7E3130, 0x9C4740, 0xB96253),   # Brick
]
PALETTE = [((v >> 16) & 255, (v >> 8) & 255, v & 255) for r in RAMPS for v in r]
INK = (0x0D, 0x08, 0x13)

# ── the brief ────────────────────────────────────────────────────────────────
#
# Written to the recipe the bottles were (memory pixellab-mcp-constraints): the two
# things that must be there go FIRST and as physical objects, the proportion is a
# NUMBER rather than an adjective, and the viewpoint is spelled out or a flat
# cut-out comes back.

BODY = (
    'A SMALL ROUND STEEL CAP SEATED ON TOP OF A DOMED STRAINER LID, and under them a '
    'tall tapered stainless steel mixing tin: a three-piece cobbler cocktail shaker '
    'standing upright, closed. The tin is narrow at its base and widens all the way '
    'up to a rolled collar at its shoulder, the domed lid sits over that collar, and '
    'the little cap stands centred on the dome. The whole shaker is about 2.2 times '
    'as tall as it is wide. '
)
VIEW = (
    'Seen straight on from SLIGHTLY ABOVE, so the cap top and the collar read as '
    'shallow ellipses and the tin has visible roundness, never a flat front-on '
    'cut-out. '
)
TAIL = (
    'Brushed steel drawn in flat bands of grey from dark at the edges to pale down '
    'the middle, hard pixel edges, no anti-aliasing, one pixel dark outline all the '
    'way round, single object centred, filling the frame from top to bottom, '
    'transparent background, no text, no letters, no numbers, no logo.'
)

JOBS = {
    # engine, seed, extra prompt
    'pro_a':    ('pro',    11, ''),
    'pro_b':    ('pro',    47, 'The steel is warm and slightly worn, a working tin '
                               'rather than a showroom one. '),
    'flux_a':   ('flux',   11, ''),
    'flux_b':   ('flux',   29, 'Dark gunmetal tin with a polished steel lid. '),
    'pixen_a':  ('pixen',  11, ''),
    'pixen_b':  ('pixen',  53, 'Art deco cocktail shaker, a thin engraved band '
                               'around the collar. '),
}


def prompt_for(key):
    _, _, extra = JOBS[key]
    return BODY + VIEW + extra + TAIL


# ── plumbing ─────────────────────────────────────────────────────────────────

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
    W, H = SHEET
    assert W % 4 == 0 and H % 4 == 0, 'both edges must divide by 4'
    s = load()
    for key, (engine, seed, _) in JOBS.items():
        if only and key not in only:
            continue
        e = s.get(key) or {}
        if e.get('png'):
            print('  %-9s already landed' % key)
            continue
        desc = prompt_for(key)
        if len(desc) > 2000:
            raise SystemExit('%s: description is %d chars (max 2000)' % (key, len(desc)))
        if e.get('job_id'):
            text, images = call('get_image', {'job_id': e['job_id']})
            if images:
                _keep(key, images[0])
            else:
                print('  %-9s cooking: %s' % (key, (text.strip().splitlines() or [''])[0][:90]))
            continue
        if engine == 'pro':
            args = {'description': desc, 'width': W, 'height': H,
                    'no_background': True, 'seed': seed,
                    'style_image_base64': b64(os.path.join(OUT, 'shaker.png')),
                    'style_copy': ['color_palette', 'outline']}
            tool = 'create_image_pro'
        elif engine == 'flux':
            args = {'description': desc, 'width': W, 'height': H,
                    'no_background': True, 'seed': seed, 'view': 'side',
                    'outline': 'single color black outline', 'shading': 'medium shading',
                    'detail': 'medium detail',
                    'color_image_base64': b64(PALETTE_PNG)}
            tool = 'create_image_pixflux'
        else:
            args = {'description': desc, 'width': W, 'height': H,
                    'no_background': True, 'seed': seed, 'view': 'side',
                    'outline': 'single color black outline', 'detail': 'medium detail'}
            tool = 'create_image_pixen'
        text, images = call(tool, args)
        if images:
            _keep(key, images[0])
            continue
        m = re.search(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}', text, re.I)
        s = load()
        s.setdefault(key, {})['job_id'] = m.group(0) if m else None
        save(s)
        print('  %-9s queued %s' % (key, m.group(0) if m else text.strip()[:140]))


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


# ── the steel ladder ─────────────────────────────────────────────────────────
#
# Nearest-colour quantizing drags brushed steel into the CREAM ramp, because Cream
# is where this palette keeps its light neutrals and the generator's greys are a
# hair warm. What comes back is a bronze shaker. The fix is the one the top strip
# already paid for (memory art-direction-rules, 2026-08-19): map by LUMA onto a
# ladder chosen for the material, so the shading survives and the hue is decided
# once, here, rather than 4000 times by a distance metric.
STEEL = [(0x0D, 0x08, 0x13),    # Night 0   — the keyline and the deepest shadow
         (0x14, 0x16, 0x1A),    # Graphite 0
         (0x24, 0x27, 0x2D),    # Graphite 1
         (0x38, 0x3D, 0x45),    # Graphite 2
         (0x54, 0x5A, 0x64),    # Graphite 3
         (0x80, 0x88, 0x93),    # Graphite 4
         (0x9C, 0x8F, 0x80),    # Cream 2   — where the bar's warm light lands
         (0xC9, 0xBC, 0xA8),    # Cream 3
         (0xF2, 0xE8, 0xD5)]    # Cream 4


def luma(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def steel(im, keep=()):
    """Every opaque pixel onto STEEL by its place in the drawing's own luma range.

    The darkest 6% is held at the keyline so an outline stays an outline; the rest
    is spread across the ladder linearly, which is what keeps a highlight a
    highlight. `keep` is a list of (x0, y0, x1, y1) boxes left untouched — nothing
    uses it yet, but a coloured detail would go there rather than into a special case."""
    im = im.convert('RGBA')
    px = im.load()
    vals = []
    for y in range(im.height):
        for x in range(im.width):
            if px[x, y][3] >= 128:
                vals.append(luma(px[x, y]))
    if not vals:
        return im
    vals.sort()
    lo = vals[int(len(vals) * 0.02)]
    hi = vals[int(len(vals) * 0.985)]
    span = max(1.0, hi - lo)
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a < 128:
                px[x, y] = (0, 0, 0, 0)
                continue
            t = (luma((r, g, b)) - lo) / span
            i = 0 if t < 0.06 else 1 + int(min(0.999, max(0.0, (t - 0.06) / 0.94))
                                           * (len(STEEL) - 1))
            c = STEEL[min(i, len(STEEL) - 1)]
            px[x, y] = (c[0], c[1], c[2], 255)
    return im


# ── measuring the drawing ────────────────────────────────────────────────────

def rows(im):
    """(x0, x1, width) per row, or None where the row is empty."""
    px = im.load()
    out = []
    for y in range(im.height):
        xs = [x for x in range(im.width) if px[x, y][3] >= 128]
        out.append((min(xs), max(xs), len(xs)) if xs else None)
    return out


def seams(im):
    """Where the three pieces of a cobbler shaker meet, read off the silhouette.

    A cobbler is a cap, a strainer dome and a tin, and the silhouette says so
    without being told: the cap is a narrow column, the dome swells out of it to
    the COLLAR — the widest thing on the shaker — and the tin hangs below the
    collar, tapering away. So the collar is the widest row in the upper half, and
    the cap's seam is the narrowest row between the top and the collar."""
    r = rows(im)
    ys = [y for y, v in enumerate(r) if v]
    top, bot = ys[0], ys[-1]
    h = bot - top + 1
    upper = range(top, top + int(h * 0.62))
    widest = max(r[y][2] for y in upper)
    collar = max(y for y in upper if r[y][2] == widest)
    # ...and the waist between the cap and the dome. Skipped past the cap's own
    # first rows, where a domed cap top is still widening.
    # The LAST of the narrow rows, not the first: a cap is a column, so the whole
    # column ties for narrowest and the cut has to fall at its foot. Taking the
    # first put the seam through the top of the cap on two takes out of five.
    search = list(range(top + max(3, int(h * 0.05)), collar - int(h * 0.10)))
    narrow = min(r[y][2] for y in search)
    waist = max(y for y in search if r[y][2] <= narrow + 1)
    return top, bot, collar, waist


def ellipse_mouth(im, y0, x0, x1, deep):
    """Opens a vessel: the rim's far edge, the bore behind it, and the near lip.

    Drawn from the vessel's OWN ladder rather than from a fixed grey, so the tin
    that comes out of a dark take stays dark. `deep` is how many rows the bore
    runs before it meets the front wall."""
    px = im.load()
    w = x1 - x0 + 1
    cx = (x0 + x1) / 2.0
    rx = w / 2.0 - 1.0
    ry = max(3.0, w * 0.155)
    cy = y0 + ry + 1
    bore = STEEL[1]
    back = STEEL[3]
    lip = STEEL[6]
    lit = STEEL[8]
    for y in range(int(cy - ry) - 1, int(cy + ry + deep) + 2):
        for x in range(x0, x1 + 1):
            if not (0 <= y < im.height):
                continue
            if px[x, y][3] < 128:
                continue
            dx = (x - cx) / rx
            dy = (y - cy) / ry
            d = dx * dx + dy * dy
            if d <= 1.0:
                # inside the mouth: the bore, with the far wall catching light
                edge = d > 0.66
                if y < cy and edge:
                    px[x, y] = back + (255,)
                elif edge and y > cy:
                    px[x, y] = lip + (255,)
                else:
                    px[x, y] = bore + (255,)
            elif d <= 1.34 and y <= cy:
                px[x, y] = lit + (255,) if y < cy - ry * 0.4 else lip + (255,)


def split(key, out_dir=None):
    """Cuts the four plates out of one take, on one frozen sheet."""
    s = load()
    e = s.get(key) or {}
    if not e.get('png'):
        raise SystemExit('%s has not landed' % key)
    W, H = SHEET
    src = steel(Image.open(os.path.join(HERE, e['png'])))
    top, bot, collar, waist = seams(src)
    r = rows(src)

    shut = src.copy()

    cap = src.copy()                      # dome + cap: everything above the tin
    cpx = cap.load()
    for y in range(collar + 1, H):
        for x in range(W):
            cpx[x, y] = (0, 0, 0, 0)

    tin = src.copy()                      # the tin: the collar row down
    tpx = tin.load()
    for y in range(0, collar):
        for x in range(W):
            tpx[x, y] = (0, 0, 0, 0)
    ellipse_mouth(tin, collar, r[collar][0], r[collar][1], deep=6)

    pour = cap.copy()                     # the dome with its cap lifted off
    ppx = pour.load()
    for y in range(0, waist):
        for x in range(W):
            ppx[x, y] = (0, 0, 0, 0)
    ellipse_mouth(pour, waist, r[waist][0], r[waist][1], deep=3)

    plates = {'shaker': shut, 'tin_open': tin,
              'shaker_cap': cap, 'shaker_cap_pour': pour}
    d = out_dir or os.path.join(STAGING, 'plates_' + key)
    os.makedirs(d, exist_ok=True)
    for n, im in plates.items():
        im.save(os.path.join(d, n + '.png'))
    print('  %s: top %d collar %d waist %d bottom %d -> %s' % (key, top, collar, waist, bot, d))
    print(constants(tin, cap))
    return plates


def constants(tin, cap):
    """The bench's numbers, re-measured off the plates that were just cut.

    Every one of these is a fraction of the sheet, and the sheet is drawn to fill
    its rect exactly, so they are also fractions of the rect the bench draws into.
    Printed rather than written: they live in C# next to the reasons for them."""
    W, H = SHEET
    rt = rows(tin)
    ys = [y for y, v in enumerate(rt) if v]
    tin_top, tin_bot = ys[0], ys[-1]
    rc = rows(cap)
    yc = [y for y, v in enumerate(rc) if v]
    cap_mid = (yc[0] + yc[-1] + 1) / 2.0

    # the drink's floor sits above the tin's pinched base, and its rim inside the
    # mouth — five rows off each, which is what the old art was measured at
    floor_row = tin_bot - 5
    rim_row = tin_top + 8
    widest = max(rt[y][2] for y in range(tin_top, tin_bot + 1))
    cavity = widest - 18                       # two walls and their shading

    prof = []
    for i in range(28):
        y = int(round(rim_row + (floor_row - rim_row) * (1.0 - i / 27.0)))
        prof.append(rt[y][2] if rt[y] else 0)
    top_w = max(prof)
    prof = [round(p / float(top_w), 3) for p in prof]

    return ('\n  CapArtOffset = %.4ff   // the lid block\'s centre, above the sheet\'s\n'
            '  CavityFloor  = %.4ff\n'
            '  CavityRim    = %.4ff\n'
            '  cavity width = %.3f of the sprite  (the 0.50 in PushShakerPool)\n'
            '  profile      = %s'
            % (0.5 - cap_mid / H, (H - floor_row) / float(H), (H - rim_row) / float(H),
               cavity / float(W), ', '.join('%.3ff' % p for p in prof)))


def ship(key):
    plates = split(key)
    for n, im in plates.items():
        im.save(os.path.join(OUT, n + '.png'))
        print('  %s -> %s' % (n, os.path.join(OUT, n + '.png')))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'take'
    if cmd == 'take':
        take(sys.argv[2:] or None)
    elif cmd == 'split':
        split(sys.argv[2])
    elif cmd == 'ship':
        ship(sys.argv[2])
    else:
        print(__doc__)
