# -*- coding: utf-8 -*-
"""FOURTH ROUND (2026-09-10).

Approved out of round three: picR_pelican, picS_lips, picS_cherry, picS_flamingo,
fx_ceil_deco, fx_ceil_stucco, fx_ceil_palm, fx_ceil_beams.

Rejected, and rightly: every floor. "Üretilen zeminlerin hepsi hatalı, zemin tek yüzey
olacak." Round three warped the floor with the CEILING's mapping — the texture laid across
each row between that row's own edges, and mirrored at the centre so the tile's seam would
not show. On a ceiling read as one plate that passes. On a floor it does not: the pattern
splays outward like a fan and meets itself down the middle, so what you see is two half
surfaces, not one floor. The boards in the room's own art run straight back; mine did not.

So the floor is projected properly here, as a GROUND PLANE:

    the horizon is y = 148  (fitted independently off both side walls: the left wall's
    edges cross at (302.1, 147.0), the right wall's at (342.5, 148.3) — the two vanishing
    points differ, the art being hand drawn, but the HORIZON they share is what a floor
    needs)

    yd = y - 148,   V = s*f/yd,   U = s*(x - 320)/yd

which is the real projection of a plane under the camera: both coordinates go as 1/yd, so
the texture converges toward the horizon and tiles continuously — one surface, no seam, no
mirror, and lines that run back to the vanishing point instead of fanning out.

Also asked for, and queued here: alternatives for the BACK wall and for the LEFT windows,
music systems standing on the floor, and Malibu paper posters cut to both side walls.

  py -3 -X utf8 Tools/room_variants4_gen.py take     submit / collect
  py -3 -X utf8 Tools/room_variants4_gen.py build    map everything into the room
  py -3 -X utf8 Tools/room_variants4_gen.py sheet    the contact sheets
"""
import io
import json
import os
import re
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(HERE, 'room_variants4')
PREV = os.path.join(HERE, 'room_variants3')
STATE = os.path.join(OUT, 'state.json')
FIXT = os.path.join(ROOT, 'Assets', 'Resources', 'Fixtures')

sys.path.insert(0, HERE)
import pixellab                                   # noqa: E402

ROOM = ('pixel art, warm 1980s Miami bar interior palette, magenta and teal accents on '
        'dark wood and brass, flat colours with hard pixel edges, no dithering, no text, '
        'no watermark')

MATTE = ('completely matte and flat, no reflections, no mirror, no gloss, no shine, no '
         'specular highlights, no reflected light, ')

DECK = ('a flat floor texture seen straight from above, filling the whole frame edge to '
        'edge, tileable, even and repeating, no perspective, no vanishing point, ' + MATTE)

# The back wall is fronto-parallel — measured x 146..491, y 71..221 — so it is generated at
# its own size and needs no mapping at all.
#
# The first brief said "one interior wall of A BAR" and every take came back as a whole bar:
# a counter, stools, shelves of bottles, a neon sign. The word was doing the work. The room
# already HAS its counter and its furniture standing in front of this wall, so what is
# wanted is the surface and nothing else — and it has to be said as a refusal, repeatedly,
# because "a wall" alone still invites a scene.
BACK = ('a flat wall surface and NOTHING ELSE, filling the whole frame edge to edge, seen '
        'straight on with no perspective and no vanishing point, an EMPTY bare wall, no '
        'furniture, no counter, no bar, no stools, no shelves, no bottles, no lamps, no '
        'pictures, no objects of any kind in front of it, just the wall covering itself, '
        + MATTE)

# The window wall and the posters ARE mapped, so they are generated flat and square-on.
WINDOW = ('the flat front-on elevation of a wall of windows in a bar, filling the whole '
          'frame edge to edge, seen straight on with no perspective, ' + MATTE)

# A poster is a RECTANGLE OF PAPER. Cutting the background out of the first take cut the
# paper out with it and left the artwork floating on the wall with no sheet under it, so
# these are generated with their background kept and the whole canvas IS the sheet.
POSTER = ('a rectangular paper poster printed edge to edge, the printed image filling the '
          'whole rectangle right to the paper edge with a narrow plain paper margin, no '
          'frame, no glass, seen straight on, ' + MATTE)

HIFI = ('one piece of music equipment standing on the floor of a bar, seen straight on from '
        'a low eye level, transparent background, no floor, no shadow, ' + MATTE)

JOBS = {
    # ── the back wall (fronto-parallel, 345x150 — drawn at its own size) ───────
    'back_tropic':  (172, 76, 1901, BACK + 'tropical leaf wallpaper above a dark wood dado '
                     'rail, deep teal leaves on a dusty pink ground, ' + ROOM),
    'back_tile':    (172, 76, 1903, BACK + 'glossless cream ceramic tiles to waist height '
                     'with a magenta border course, painted plaster above, ' + ROOM),
    'back_panel':   (172, 76, 1905, BACK + 'full height vertical wood panelling in warm '
                     'walnut with a brass strip at the top, ' + ROOM),
    'back_stripe':  (172, 76, 1907, BACK + 'wide vertical stripes of coral and cream with a '
                     'narrow teal line between each pair, ' + ROOM),
    'back_brick':   (172, 76, 1909, BACK + 'painted brick, cream over old brick, with a low '
                     'plum coloured plinth along the bottom, ' + ROOM),

    # ── the left window wall (generated flat, mapped by build()) ──────────────
    'win_blinds':   (128, 128, 921, WINDOW + 'tall windows in slim dark frames with pale '
                     'venetian blinds half lowered behind the glass, ' + ROOM),
    'win_arch':     (128, 128, 923, WINDOW + 'tall arched windows in brass frames, a violet '
                     'evening sky behind the glass, ' + ROOM),
    'win_stained':  (128, 128, 925, WINDOW + 'tall windows of stained glass in lead came, '
                     'a sunburst of coral and teal panes in each, ' + ROOM),
    'win_shutter':  (128, 128, 927, WINDOW + 'tall windows with folded cream louvred '
                     'shutters at each side and palm leaves beyond the glass, ' + ROOM),

    # ── music systems standing on the floor ───────────────────────────────────
    'hifi_jukebox': (56, 92, 941, HIFI + 'a classic jukebox with an arched lit top and '
                     'chrome grille, ' + ROOM),
    'hifi_stack':   (44, 104, 943, HIFI + 'a tall speaker stack, two black speaker cabinets '
                     'one on top of the other with pale cones, ' + ROOM),
    'hifi_deck':    (72, 68, 945, HIFI + 'a low DJ console on a stand, two turntables and a '
                     'mixer between them, ' + ROOM),
    'hifi_radio':   (60, 56, 947, HIFI + 'a big boombox radio on a low wooden crate, two '
                     'round speakers and a cassette door, ' + ROOM),

    # ── Malibu paper posters, for both side walls ─────────────────────────────
    'post_malibu':  (72, 96, 1961, POSTER + 'a travel poster reading nothing, a Malibu beach '
                     'at sunset with one lifeguard tower and palm trees, ' + ROOM),
    'post_surf':    (72, 96, 1963, POSTER + 'a surf poster, one surfer riding a big teal wave '
                     'under an orange sky, ' + ROOM),
    'post_pier':    (72, 96, 1965, POSTER + 'a poster of the Malibu pier stretching out over '
                     'violet water at dusk, ' + ROOM),
    'post_coast':   (72, 96, 1967, POSTER + 'a poster of a coast road curving along a cliff '
                     'above the sea, palms on the verge, ' + ROOM),
}

BACKS = [k for k in JOBS if k.startswith('back_')]
WINS = [k for k in JOBS if k.startswith('win_')]
HIFIS = [k for k in JOBS if k.startswith('hifi_')]
POSTS = [k for k in JOBS if k.startswith('post_')]

# The floor tiles are round three's — the art was never the problem, the projection was.
FLOOR_TILES = ['floor_parquet', 'floor_oak', 'floor_check', 'floor_terrazzo',
               'floor_marble', 'floor_carpet']


def _state():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


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
        cut = key.startswith('hifi_')      # a poster keeps its paper; see POSTER
        text, images = _call('create_image_pro', {
            'description': desc, 'width': w, 'height': h,
            'no_background': cut, 'seed': seed})
        if images:
            io.open(png, 'wb').write(images[0])
            print('  ->', key)
            continue
        m = re.search(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}',
                      text, re.I)
        s.setdefault(key, {})['job'] = m.group(0) if m else None
        _save(s)
        print('  queued %s %s' % (key, m.group(0) if m else text.strip()[:90]))


# ── the room's geometry, all of it measured off fx_walls_2/3/4 ─────────────────

HORIZON = 148.0
ROOM_CX = 320.0

# right wall: the two edges cross at (342.5, 148.3)
R_X0, R_TOPM, R_TOPC, R_BOTM, R_BOTC = 493.0, -0.5000, 73.0, 0.4571, 217.0
# left wall: ceiling edge (4,0)->(146,70), floor edge (0,294)->(150,221); cross (302.1,147.0)
L_TOPM, L_TOPC = 0.4930, -1.97
L_BOTM, L_BOTC = -0.4867, 294.0
# the back wall, fronto-parallel
BACK_RECT = (146, 71, 491, 221)


def floor_rows():
    """Every board row of the plate. Measured: each column has a first board pixel and
    everything below it is board too, with no holes, so the deck is `y >= base[x]`."""
    im = Image.open(os.path.join(FIXT, 'fx_walls_3.png')).convert('RGBA')
    px = im.load()

    def board(c):
        r, g, b, a = c
        return a > 0 and r > b + 18 and r < 210 and b < 110 and g < r

    base = {}
    for x in range(im.width):
        col = [y for y in range(im.height) if board(px[x, y])]
        base[x] = min(col) if col else None
    rows = {}
    for y in range(im.height):
        run = [x for x in range(im.width) if base[x] is not None and base[x] <= y]
        if run:
            rows[y] = (min(run), max(run))
    return im.size, rows


def project_floor(tex, rows, size, tiles_across=3.0, focal=420.0, inset=5):
    """The floor as one plane under the camera, not a stack of independent rows.

    yd = y - HORIZON is how far below the horizon a row sits, and for a ground plane BOTH
    texture coordinates go as 1/yd — the depth as focal/yd and the sideways offset as
    (x - cx)/yd. That single fact is the whole difference from round three: it makes the
    boards run BACK toward the vanishing point and tile continuously across the whole
    width, instead of being stretched between each row's own two edges and mirrored at the
    middle, which is what made two half floors meet in the centre of the room."""
    w, h = size
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    op = out.load()
    tex = tex.crop((inset, inset, tex.width - inset, tex.height - inset))
    tp = tex.load()
    tw, th = tex.size
    ynear = max(rows)
    s = tiles_across * (ynear - HORIZON) / float(w)
    for y, (x0, x1) in rows.items():
        yd = y - HORIZON
        if yd <= 1.0:
            continue
        v = s * focal / yd
        sy = int((v % 1.0) * th) % th
        for x in range(x0, x1 + 1):
            u = s * (x - ROOM_CX) / yd
            sx = int((u % 1.0) * tw) % tw
            c = tp[sx, sy]
            op[x, y] = (c[0], c[1], c[2], 255)
    return out


def _hang(pic, xa, xb, topf, botf, f0, f1):
    """Map a flat picture onto a side wall between screen columns xa..xb.

    The horizontal parameter is hyperbolic in screen x, not linear: on a receding wall the
    distance along it goes as 1/d, d being the distance from the vanishing point, and
    shearing linearly instead is what makes a pasted picture read as a sticker."""
    out = Image.new('RGBA', (640, 360), (0, 0, 0, 0))
    op = out.load()
    pp = pic.convert('RGBA').load()
    pw, ph = pic.size
    lo, hi = int(min(xa, xb)), int(max(xa, xb))
    # The vanishing column is where the two edges meet. Found by bisection rather than
    # algebra so the same routine serves both walls, whichever way their edges lean.
    a, b = -4000.0, 4000.0
    for _ in range(60):
        m = (a + b) * 0.5
        if (topf(m) - botf(m)) * (topf(a) - botf(a)) > 0:
            a = m
        else:
            b = m
    xvp = (a + b) * 0.5
    da, db = xa - xvp, xb - xvp
    ka, kb = 1.0 / da, 1.0 / db
    for x in range(lo, hi + 1):
        u = (ka - 1.0 / (x - xvp)) / (ka - kb)
        sx = min(pw - 1, max(0, int(u * (pw - 1) + 0.5)))
        top, bot = topf(x), botf(x)
        hh = bot - top
        ya, yb = top + f0 * hh, top + f1 * hh
        for y in range(int(round(ya)), int(round(yb)) + 1):
            v = (y - ya) / max(1.0, (yb - ya))
            sy = min(ph - 1, max(0, int(v * (ph - 1) + 0.5)))
            c = pp[sx, sy]
            if c[3] > 40 and 0 <= x < 640 and 0 <= y < 360:
                op[x, y] = (c[0], c[1], c[2], 255)
    return out


def right_top(x):
    return R_TOPC + R_TOPM * (x - R_X0)


def right_bot(x):
    return R_BOTC + R_BOTM * (x - R_X0)


def left_top(x):
    return L_TOPC + L_TOPM * x


def left_bot(x):
    return L_BOTC + L_BOTM * x


def hang_right(pic, xa=506, xb=556, f0=0.20, f1=0.64):
    return _hang(pic, xa, xb, right_top, right_bot, f0, f1)


def hang_left(pic, xa=28, xb=104, f0=0.20, f1=0.64):
    return _hang(pic, xa, xb, left_top, left_bot, f0, f1)


def fit_back(pic):
    """The back wall takes its art straight: it faces the camera, so it is only resized."""
    x0, y0, x1, y1 = BACK_RECT
    out = Image.new('RGBA', (640, 360), (0, 0, 0, 0))
    out.paste(pic.convert('RGBA').resize((x1 - x0, y1 - y0), Image.NEAREST), (x0, y0))
    return out


def stand_on_floor(pic, foot_x, foot_y):
    """Put a floor-standing object down with its feet on the boards."""
    out = Image.new('RGBA', (640, 360), (0, 0, 0, 0))
    p = pic.convert('RGBA')
    out.alpha_composite(p, (int(foot_x - p.width * 0.5), int(foot_y - p.height)))
    return out


def build():
    size, rows = floor_rows()
    for key in FLOOR_TILES:
        p = os.path.join(PREV, key + '.png')
        if not os.path.exists(p):
            print('  !! no tile for', key)
            continue
        project_floor(Image.open(p).convert('RGBA'), rows, size).save(
            os.path.join(OUT, 'fx_' + key + '.png'))
        print('  floor  ', key)
    for key in BACKS:
        p = os.path.join(OUT, key + '.png')
        if os.path.exists(p):
            fit_back(Image.open(p)).save(os.path.join(OUT, 'fx_' + key + '.png'))
            print('  back   ', key)
    for key in WINS:
        p = os.path.join(OUT, key + '.png')
        if os.path.exists(p):
            hang_left(Image.open(p), 2, 148, 0.02, 0.98).save(
                os.path.join(OUT, 'fx_' + key + '.png'))
            print('  window ', key)
    for key in POSTS:
        p = os.path.join(OUT, key + '.png')
        if not os.path.exists(p):
            continue
        pic = Image.open(p)
        hang_right(pic).save(os.path.join(OUT, 'fx_R_' + key + '.png'))
        hang_left(pic).save(os.path.join(OUT, 'fx_L_' + key + '.png'))
        print('  poster ', key)
    for key, fx in zip(HIFIS, (196, 262, 400, 470)):
        p = os.path.join(OUT, key + '.png')
        if os.path.exists(p):
            stand_on_floor(Image.open(p), fx, 236).save(
                os.path.join(OUT, 'fx_' + key + '.png'))
            print('  hifi   ', key)


def sheet():
    wall = Image.open(os.path.join(FIXT, 'fx_walls_3.png')).convert('RGBA')

    def load(n):
        p = os.path.join(OUT, n + '.png')
        return Image.open(p).convert('RGBA') if os.path.exists(p) else None

    def stack(names, prefix, crop, path, z=2):
        strips = [('now', wall.crop(crop))]
        for n in names:
            im = load(prefix + n)
            if im is None:
                continue
            one = wall.copy()
            one.alpha_composite(im)
            strips.append((n, one.crop(crop)))
        if len(strips) < 2:
            print('  (nothing for %s yet)' % os.path.basename(path))
            return
        w, h = strips[0][1].size
        sh = Image.new('RGBA', (40 + w * z, 24 + len(strips) * (h * z + 16)),
                       (36, 22, 42, 255))
        y = 16
        for _, im in strips:
            sh.alpha_composite(im.resize((w * z, h * z), Image.NEAREST), (20, y))
            y += h * z + 16
        sh.save(path)
        print('%s  ->  %s' % (', '.join(n for n, _ in strips), path))

    stack(FLOOR_TILES, 'fx_', (0, 200, 640, 360),
          os.path.join(HERE, 'room_variants4_floors.png'))
    stack([k[5:] for k in BACKS], 'fx_back_', (130, 60, 510, 235),
          os.path.join(HERE, 'room_variants4_back.png'))
    stack([k[4:] for k in WINS], 'fx_win_', (0, 0, 200, 320),
          os.path.join(HERE, 'room_variants4_windows.png'))
    stack([k[5:] for k in POSTS], 'fx_R_post_', (470, 40, 640, 280),
          os.path.join(HERE, 'room_variants4_poster_right.png'), z=3)
    stack([k[5:] for k in POSTS], 'fx_L_post_', (0, 20, 170, 300),
          os.path.join(HERE, 'room_variants4_poster_left.png'), z=3)
    stack([k[5:] for k in HIFIS], 'fx_hifi_', (140, 110, 540, 250),
          os.path.join(HERE, 'room_variants4_hifi.png'))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'take'
    if cmd == 'sheet':
        sheet()
    elif cmd == 'build':
        build()
    else:
        take(sys.argv[2:] or None)
