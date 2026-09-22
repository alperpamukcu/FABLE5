# -*- coding: utf-8 -*-
"""SEVENTH ROUND (2026-09-22, the author's seventh list: "Yeni tavan alternatifleri, orta duvar, zemin, yan duvar
alternatifleri üret").

Four of each, four surfaces, drawn and mapped exactly as round six mapped them (its warps, its projections, its
side-wall transpose), so a pick from here drops into the room the way the last rounds' picks did. Nothing here
enters Assets: the pieces land in Tools/room_variants7 and a picks page, and only the author's choice ships.

The briefs keep to the club the game is now called - the MALIBU CLUB - and stay away from what rounds three to six
already drew (grids, rays, stars, chevrons, arches, palm paper, stripes, diamond tiles, sunbursts):

  ceiling     rattan weave, pressed tin in pastel, a painted sky at dusk, a lacquered teal with a gold fret
  back wall   a surf-shack plank wall, a neon wave mural, a pastel terrazzo wainscot, a flamingo toile
  floor       black and white checker at a slant, sun-bleached boardwalk planks, pink marble slabs, a sea-glass mosaic
  right wall  a coral reef mural, a pastel Art Deco fan, a palm-shadow stucco, a vintage beach-ad collage

  py -3 -X utf8 Tools/room_variants7_gen.py take     submit / collect (re-run until done)
  py -3 -X utf8 Tools/room_variants7_gen.py build    map everything into the room
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
OUT = os.path.join(HERE, 'room_variants7')
STATE = os.path.join(OUT, 'state.json')
FIXT = os.path.join(ROOT, 'Assets', 'Resources', 'Fixtures')

sys.path.insert(0, HERE)
import room_variants3_gen as r3                    # noqa: E402
import room_variants4_gen as r4                    # noqa: E402
import room_variants6_gen as r6                    # noqa: E402  right_wall_face, warp_side, the briefs

MIAMI, TILE, WALL = r6.MIAMI, r6.TILE, r6.WALL

JOBS = {
    # ── the ceiling (texture; warped into the trapezoid) ──────────────────────
    'ceil_rattan':  (128, 128, 7151, TILE + 'a ceiling of woven rattan cane panels, honey and cream '
                     'weave between thin dark bamboo battens, ' + MIAMI),
    'ceil_tin':     (128, 128, 7153, TILE + 'a pressed tin ceiling in pastel mint and pink, square '
                     'embossed medallions in a grid, ' + MIAMI),
    'ceil_dusk':    (128, 128, 7155, TILE + 'a painted ceiling of a dusk sky, soft pink and lilac '
                     'clouds on pale peach, like a mural, ' + MIAMI),
    'ceil_fret':    (128, 128, 7157, TILE + 'a lacquered deep teal ceiling with a thin gold Greek '
                     'fret border pattern repeating, ' + MIAMI),

    # ── the back wall (fronto-parallel; drawn at its own size) ────────────────
    'back_planks':  (172, 76, 7161, WALL + 'a surf shack wall of horizontal painted planks, faded '
                     'turquoise and white, a coral stripe near the top, ' + MIAMI),
    'back_wave':    (172, 76, 7163, WALL + 'a mural of one big curling ocean wave in pink and '
                     'turquoise under a sunset, painted flat on the wall, ' + MIAMI),
    'back_terrazzo':(172, 76, 7165, WALL + 'a pastel pink wall with a terrazzo wainscot in the '
                     'lower third and a thin brass rail, ' + MIAMI),
    'back_toile':   (172, 76, 7167, WALL + 'toile wallpaper of little flamingos and palm fronds '
                     'printed in coral on cream, ' + MIAMI),

    # ── the floor (texture; projected as a ground plane) ──────────────────────
    'floor_check':  (128, 128, 7111, TILE + 'a black and white checkerboard floor of glossless '
                     'square tiles set on the diagonal, ' + MIAMI),
    'floor_deck':   (128, 128, 7113, TILE + 'sun-bleached boardwalk planks, pale grey-pink wood '
                     'with dark gaps between the boards, ' + MIAMI),
    'floor_marble': (128, 128, 7115, TILE + 'large pink marble floor slabs with soft white veins '
                     'and thin dark joints, matte, ' + MIAMI),
    'floor_glass':  (128, 128, 7117, TILE + 'a sea glass mosaic floor, small irregular tiles in '
                     'aqua, mint and white with dark grout, ' + MIAMI),

    # ── the right wall (texture; warped onto the side wall's quad) ────────────
    'rwall_reef':   (128, 128, 7171, TILE + 'a coral reef mural, pink and orange corals and small '
                     'fish on a deep turquoise ground, ' + MIAMI),
    'rwall_deco':   (128, 128, 7173, TILE + 'a pastel Art Deco pattern of stacked fans in peach, '
                     'mint and cream with thin gold outlines, ' + MIAMI),
    'rwall_stucco': (128, 128, 7175, TILE + 'warm pink stucco wall with soft shadows of palm '
                     'leaves falling across it, ' + MIAMI),
    'rwall_ads':    (128, 128, 7177, TILE + 'a collage of vintage beach and cocktail posters in '
                     'faded pastel colours, pasted edge to edge, no readable text, ' + MIAMI),
}


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
        text, images = r4._call('create_image_pro', {'description': desc, 'width': w, 'height': h,
                                                     'no_background': False, 'seed': seed})
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


def build():
    def have(key):
        p = os.path.join(OUT, key + '.png')
        return Image.open(p).convert('RGBA') if os.path.exists(p) else None

    fsize, frows = r4.floor_rows()
    csize, crows = r3.ceiling_rows()
    drawn, face = r6.right_wall_face()
    for key in JOBS:
        tex = have(key)
        if tex is None:
            continue
        dst = os.path.join(OUT, 'fx_' + key + '.png')
        if key.startswith('floor_'):
            r4.project_floor(tex, frows, fsize).save(dst)
        elif key.startswith('ceil_'):
            r3.warp(tex, crows, csize, near_at_bottom=False).save(dst)
        elif key.startswith('back_'):
            r4.fit_back(tex).save(dst)
        elif key.startswith('rwall_'):
            surface = np.array(r6.warp_side(tex, face).convert('RGBA'))
            out = drawn.copy()
            put = face & (surface[:, :, 3] > 0)
            out[put, :3] = surface[put, :3]
            room = Image.new('RGBA', (640, 360), (0, 0, 0, 0))
            room.alpha_composite(Image.fromarray(out), r6.RIGHT_AT)
            room.save(dst)
        print('  built', key)
    print('build done ->', OUT)


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'take'
    if cmd == 'take':
        take(sys.argv[2:] or None)
    elif cmd == 'build':
        build()
    else:
        raise SystemExit(__doc__)
