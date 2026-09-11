# -*- coding: utf-8 -*-
"""SIXTH ROUND (2026-09-12, the author: "80ler miami ile alakalı desenler üret: halı, zemin,
duvar lambaları çift, sağ duvar neon, sağ duvar tablosu, tavan, arka duvar, sağ duvar.
Üretilen her nesnede perspektifi dikkate al. Fazladan üretim yapma, sadece 3er adet üret").

THREE OF EACH, EIGHT THINGS, AND EVERY ONE OF THEM LANDS IN PERSPECTIVE:

  rug          texture -> stamped into the room's own rug silhouette (its perspective)
  floor        texture -> projected as the ground plane (round 4's projection)
  wall lamps   one lamp drawn flat -> mounted at BOTH brackets, the right one mirrored,
               the way the pair slot mounts it in the game
  right neon   a sign, hung where the room hangs its neon
  right art    a picture -> warped onto the RIGHT WALL, which is at an angle (round 4's
               hang_right, the same mapping the pelican came through)
  ceiling      texture -> warped into the cream trapezoid (round 3's mapping)
  back wall    a wall surface, drawn at the back wall's own size: it is fronto-parallel
  right wall   texture -> warped onto the side wall's own quad, and laid into the author's
               own drawing so the door, its frame and the threshold stay exactly as drawn.
               The side wall recedes SIDEWAYS, so it is warped through a transpose: the same
               mapping a floor gets, turned on its side.

  py -3 -X utf8 Tools/room_variants6_gen.py take     submit / collect (re-run until done)
  py -3 -X utf8 Tools/room_variants6_gen.py build    map everything into the room
"""
import io
import json
import os
import re
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(HERE, 'room_variants6')
STATE = os.path.join(OUT, 'state.json')
FIXT = os.path.join(ROOT, 'Assets', 'Resources', 'Fixtures')
SRC = os.path.join(HERE, 'AssetPipeline', 'sources', 'konsept_art')
RIGHT_AT = (426, 0)        # where the right wall drawing sits in the room

sys.path.insert(0, HERE)
import pixellab                                    # noqa: E402
import room_variants3_gen as r3                    # noqa: E402
import room_variants4_gen as r4                    # noqa: E402
import room_variants5_gen as r5                    # noqa: E402  stamp(), framed()

MIAMI = ('pixel art, 1980s Miami palette — flamingo pink, turquoise, coral, cream and '
         'sunset orange on dark plum — flat colours with hard pixel edges, no dithering, '
         'no text, no watermark')
MATTE = ('completely matte and flat, no reflections, no mirror, no gloss, no shine, no '
         'specular highlights, ')
TILE = ('a flat surface pattern seen straight on, filling the whole frame edge to edge, '
        'tileable, even and repeating, no perspective, no vanishing point, ' + MATTE)
WALL = ('a flat wall surface and NOTHING ELSE, filling the whole frame edge to edge, seen '
        'straight on with no perspective, an EMPTY bare wall, no furniture, no counter, no '
        'stools, no shelves, no bottles, no lamps, no pictures, no objects in front of it, '
        + MATTE)
PROP = ('one object on a transparent background, seen straight on, nothing else in frame, '
        'no wall, no floor, no shadow, ' + MATTE)
PIC = ('one framed picture hanging flat on a wall, seen straight on, the frame filling the '
       'whole image, transparent background outside the frame, ' + MATTE)

JOBS = {
    # ── the rug (texture; stamped into the room's own rug) ────────────────────
    'rug_memphis':  (96, 96, 6101, TILE + 'a Memphis pattern rug: squiggles, triangles and '
                     'confetti dashes in flamingo pink, turquoise and black on cream, ' + MIAMI),
    'rug_sunset':   (96, 96, 6103, TILE + 'a rug woven in wide sunset bands, magenta into '
                     'orange into gold, with thin turquoise lines between, ' + MIAMI),
    'rug_palm':     (96, 96, 6105, TILE + 'a rug with repeating palm leaf silhouettes in '
                     'deep teal on a dusty pink ground, ' + MIAMI),

    # ── the floor (texture; projected as a ground plane) ──────────────────────
    'floor_grid':   (128, 128, 6111, TILE + 'a neon grid floor, thin magenta and cyan lines '
                     'ruled over near black, ' + MIAMI),
    'floor_chip':   (128, 128, 6113, TILE + 'terrazzo floor with big pink, turquoise and '
                     'cream chips in pale grey, ' + MIAMI),
    'floor_wave':   (128, 128, 6115, TILE + 'floor tiles printed with a repeating curling '
                     'wave motif in teal and coral on cream, ' + MIAMI),

    # ── the wall lamps (one lamp; mounted at both brackets) ───────────────────
    'lamp_scallop': (40, 40, 6121, PROP + 'a wall sconce with a scalloped cream shade on a '
                     'brass bracket, glowing warm, ' + MIAMI),
    'lamp_neonbar': (40, 40, 6123, PROP + 'a wall lamp made of two stacked neon tubes, pink '
                     'over cyan, in a slim chrome bracket, ' + MIAMI),
    'lamp_shell':   (40, 40, 6125, PROP + 'a wall lamp shaped like a scallop shell in coral '
                     'and cream, lit from inside, ' + MIAMI),

    # ── the neon sign on the right of the back wall ───────────────────────────
    'neon_sun':     (32, 36, 6131, PROP + 'a neon sign of a setting sun with three bars '
                     'under it, glowing magenta and orange tubing, ' + MIAMI),
    'neon_glass':   (32, 36, 6133, PROP + 'a neon sign of a tall cocktail glass with a '
                     'cherry, glowing cyan tubing, ' + MIAMI),
    'neon_bird':    (32, 36, 6135, PROP + 'a neon sign of a flying pelican, glowing pink '
                     'and white tubing, ' + MIAMI),

    # ── the picture for the right wall (warped onto the angled wall) ──────────
    'picR_sunset':  (64, 80, 6141, PIC + 'a painting of a sunset over the ocean with two '
                     'palms, thin brass frame, ' + MIAMI),
    'picR_conv':    (64, 80, 6143, PIC + 'a painting of a white convertible on a coast road, '
                     'pale wood frame, ' + MIAMI),
    'picR_shapes':  (64, 80, 6145, PIC + 'an abstract Memphis painting of triangles and '
                     'circles in pink, teal and yellow, black frame, ' + MIAMI),

    # ── the ceiling (texture; warped into the trapezoid) ──────────────────────
    'ceil_grid':    (128, 128, 6151, TILE + 'a ceiling of square panels divided by slim '
                     'turquoise beams, cream panels with a pink rosette in each, ' + MIAMI),
    'ceil_ray':     (128, 128, 6153, TILE + 'a ceiling of alternating coral and cream rays '
                     'meeting in thin brass lines, ' + MIAMI),
    'ceil_stars':   (128, 128, 6155, TILE + 'a deep plum ceiling with small cream stars and '
                     'thin gold pinstripes, ' + MIAMI),

    # ── the back wall (fronto-parallel; drawn at its own size) ────────────────
    'back_chevron': (172, 76, 6161, WALL + 'wide chevrons of coral, cream and turquoise '
                     'running up the wall, with a plum dado below, ' + MIAMI),
    'back_arch':    (172, 76, 6163, WALL + 'repeating tall arches outlined in pink neon '
                     'tubing on a deep plum wall, ' + MIAMI),
    'back_palmw':   (172, 76, 6165, WALL + 'palm leaf wallpaper, turquoise leaves on a warm '
                     'cream ground, with a coral picture rail, ' + MIAMI),

    # ── the right wall (texture; warped onto the side wall's quad) ────────────
    'rwall_stripe': (128, 128, 6171, TILE + 'wide vertical stripes of flamingo pink and '
                     'cream with a thin turquoise line between each pair, ' + MIAMI),
    'rwall_tile':   (128, 128, 6173, TILE + 'glossless square tiles in turquoise and cream '
                     'laid in a diamond grid, ' + MIAMI),
    'rwall_sunray': (128, 128, 6175, TILE + 'a sunburst pattern of coral and gold rays over '
                     'a cream ground, ' + MIAMI),
}

CUT = ('lamp_', 'neon_', 'picR_')
LAMP_MOUNTS = ((187, 86), (411, 86))     # measured off the room: the two brackets


def _state():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def _save(s):
    os.makedirs(OUT, exist_ok=True)
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(s, indent=1))


def take(only=None):
    os.makedirs(OUT, exist_ok=True)
    s = _state()
    waiting = 0
    for key, (w, h, seed, desc) in JOBS.items():
        if only and not any(key.startswith(o) for o in only):
            continue
        png = os.path.join(OUT, key + '.png')
        if os.path.exists(png):
            continue
        e = s.get(key) or {}
        if e.get('job'):
            text, images = r4._call('get_image', {'job_id': e['job']}, timeout=180)
            if images:
                io.open(png, 'wb').write(images[0])
                print('  ->', key)
            else:
                waiting += 1
            continue
        text, images = r4._call('create_image_pro', {
            'description': desc, 'width': w, 'height': h,
            'no_background': key.startswith(CUT), 'seed': seed})
        if images:
            io.open(png, 'wb').write(images[0])
            print('  ->', key)
            continue
        m = re.search(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}', text, re.I)
        s.setdefault(key, {})['job'] = m.group(0) if m else None
        _save(s)
        waiting += 1
        print('  queued %s %s' % (key, m.group(0) if m else text.strip()[:80]))
    have = sum(1 for k in JOBS if os.path.exists(os.path.join(OUT, k + '.png')))
    print('%d of %d drawn, %d still cooking' % (have, len(JOBS), waiting))


# ── the side wall: a plane that recedes sideways ──────────────────────────────

def right_wall_face():
    """The author's own right wall drawing, split into the WALL FACE and everything that is
    not it — the door, its frame and the threshold — which any new surface has to keep."""
    drawn = np.array(Image.open(os.path.join(SRC, 'right_wall_t1.png')).convert('RGBA'))
    plate = np.array(Image.open(os.path.join(FIXT, 'fx_walls_1.png')).convert('RGBA'))
    h, w = drawn.shape[:2]
    under = plate[RIGHT_AT[1]:RIGHT_AT[1] + h, RIGHT_AT[0]:RIGHT_AT[0] + w].astype(int)
    opaque = drawn[:, :, 3] > 200
    # the door, measured the way the shipping tool measures it
    differs = (np.abs(under[:, :, :3] - drawn[:, :, :3].astype(int)).max(axis=2) > 2) & opaque
    door = np.zeros_like(differs)
    door[55:, 146:] = True
    door &= differs
    rgb = drawn[:, :, :3].astype(int)
    grey = ((np.abs(rgb[:, :, 0] - rgb[:, :, 1]) < 10) & (np.abs(rgb[:, :, 1] - rgb[:, :, 2]) < 10)
            & (rgb[:, :, 0] > 60)) & opaque          # the frame lines and the threshold
    face = opaque & ~door & ~grey
    return drawn, face


def warp_side(tex, face):
    """A texture onto the side wall: transposed, the wall is a plane receding like a floor,
    so round three's warp does the work and the result is turned back."""
    rows = {}
    faceT = face.T
    for x in range(faceT.shape[0]):
        run = np.nonzero(faceT[x])[0]
        if run.size:
            rows[x] = (int(run.min()), int(run.max()))
    size = (face.shape[0], face.shape[1])           # transposed: (height, width)
    warped = r3.warp(tex.transpose(Image.TRANSPOSE), rows, size, near_at_bottom=True, repeat=3)
    return warped.transpose(Image.TRANSPOSE)


def build():
    os.makedirs(OUT, exist_ok=True)

    def have(key):
        p = os.path.join(OUT, key + '.png')
        return Image.open(p).convert('RGBA') if os.path.exists(p) else None

    # the rug: the room's own silhouette, a new weave
    for key in [k for k in JOBS if k.startswith('rug_')]:
        tex = have(key)
        if tex is not None:
            r5.stamp(tex, os.path.join(FIXT, 'fx_floor_rug.png'), 4.0).save(
                os.path.join(OUT, 'piece_' + key + '.png'))
            print('  rug', key)

    # the floor: the ground plane
    fsize, frows = r4.floor_rows()
    for key in [k for k in JOBS if k.startswith('floor_')]:
        tex = have(key)
        if tex is not None:
            r4.project_floor(tex, frows, fsize).save(os.path.join(OUT, 'fx_' + key + '.png'))
            print('  floor', key)

    # the ceiling: the cream trapezoid
    csize, crows = r3.ceiling_rows()
    for key in [k for k in JOBS if k.startswith('ceil_')]:
        tex = have(key)
        if tex is not None:
            r3.warp(tex, crows, csize, near_at_bottom=False).save(
                os.path.join(OUT, 'fx_' + key + '.png'))
            print('  ceiling', key)

    # the back wall: fronto-parallel, no mapping but its own rect
    for key in [k for k in JOBS if k.startswith('back_')]:
        pic = have(key)
        if pic is not None:
            r4.fit_back(pic).save(os.path.join(OUT, 'fx_' + key + '.png'))
            print('  back wall', key)

    # the right wall: the author's drawing with a new surface on its face
    drawn, face = right_wall_face()
    for key in [k for k in JOBS if k.startswith('rwall_')]:
        tex = have(key)
        if tex is None:
            continue
        surface = np.array(warp_side(tex, face).convert('RGBA'))
        out = drawn.copy()
        put = face & (surface[:, :, 3] > 0)
        out[put, :3] = surface[put, :3]
        room = Image.new('RGBA', (640, 360), (0, 0, 0, 0))
        room.alpha_composite(Image.fromarray(out), RIGHT_AT)
        room.save(os.path.join(OUT, 'fx_' + key + '.png'))
        print('  right wall', key)

    # the pair of wall lamps: one drawing, both brackets, the right one mirrored
    for key in [k for k in JOBS if k.startswith('lamp_')]:
        lamp = have(key)
        if lamp is None:
            continue
        lamp = lamp.crop(lamp.getbbox())
        room = Image.new('RGBA', (640, 360), (0, 0, 0, 0))
        for i, (x, y) in enumerate(LAMP_MOUNTS):
            piece = lamp if i == 0 else lamp.transpose(Image.FLIP_LEFT_RIGHT)
            room.alpha_composite(piece, (x, y))
        room.save(os.path.join(OUT, 'fx_' + key + '.png'))
        print('  lamps', key)

    # the picture for the right wall, warped onto it
    for key in [k for k in JOBS if k.startswith('picR_')]:
        pic = have(key)
        if pic is not None:
            r4.hang_right(pic, xa=506, xb=556, f0=0.18, f1=0.68).save(
                os.path.join(OUT, 'fx_' + key + '.png'))
            print('  right picture', key)

    print('build done ->', OUT)


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'take'
    if cmd == 'take':
        take(sys.argv[2:] or None)
    elif cmd == 'build':
        build()
    else:
        raise SystemExit(__doc__)
