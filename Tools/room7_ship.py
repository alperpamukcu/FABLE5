# -*- coding: utf-8 -*-
"""SHIP ROUND SEVEN'S PICKS (2026-09-22, the author's eighth list: "C3, B2, F3 - Rightwall2da girişin
üstündeki görsel bozuk gözüküyor orayı sıkıştırma").

Four surfaces from Docs/reports/room7 go into the game as the top rungs of their ladders:

  C3  ceil7_dusk     the painted dusk sky               ceiling      3 stars
  B2  back7_wave     one great curling wave, a mural    back wall    2.5 stars (between the chevron and the harlequin)
  F3  floor7_marble  pink marble slabs                  floor        3 stars
  R2  rwall7_deco    pastel Art Deco fans               right wall   3 stars

Two things the picks page showed wrong are put right here rather than in round seven's builder, so the
page stays the record of what the author was shown:

  THE TILE'S OWN FRAME. PixelLab hands a tile back inside a grey frame that is 6-8px on the top and left
  and 3-4px on the bottom and right; the shared warp crops 5 from every side, so a pixel of frame
  survived on two sides - and because the ceiling is laid twice and MIRRORED, that sliver became a line
  down the middle of the room and along its top (C3 showed both). The frame is measured and cut exactly.

  THE STRIP OVER THE DOOR. The side wall is warped column by column, each column's texture run stretched
  over that column's own run of wall. Over the doorway a column's wall stops at the lintel, so the whole
  run of pattern was squeezed into the strip above it - the garbled band the author saw. Those columns now
  take the height the wall WOULD have there (its floor line carried on under the door), so the pattern
  keeps one scale across the wall and the door simply cuts it.

Prices and comfort follow each ladder's own steps; the star gates are this file's first draft.

  py -3 -X utf8 Tools/room7_ship.py
"""
import io
import json
import os
import sys
from collections import OrderedDict

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SRC = os.path.join(HERE, 'room_variants7')
FIXT = os.path.join(ROOT, 'Assets', 'Resources', 'Fixtures')
DATA = os.path.join(ROOT, 'Assets', 'Data', 'fixtures', 'fixtures.json')

sys.path.insert(0, HERE)
import room_variants3_gen as r3                    # noqa: E402  warp, ceiling_rows
import room_variants4_gen as r4                    # noqa: E402  floor_rows, project_floor, fit_back
import room_variants6_gen as r6                    # noqa: E402  right_wall_face, RIGHT_AT
sys.path.insert(0, os.path.join(HERE, 'upgrade_tree'))
import ship as tree                                # noqa: E402  busiest_window, FIELD_ORDER


def rgba(path):
    return Image.open(path).convert('RGBA')


def trim_frame(tex, tol=6):
    """Cut the uniform frame PixelLab leaves round a tile: the rows and columns, from each side in,
    that are one colour (the corner's) over nine tenths of their length."""
    a = np.array(tex.convert('RGBA')).astype(int)
    corner = a[0, 0, :3]
    same = np.abs(a[:, :, :3] - corner).max(axis=2) < tol
    h, w = same.shape
    top = 0
    while top < h // 4 and same[top].mean() > 0.9:
        top += 1
    bot = h
    while bot > h - h // 4 and same[bot - 1].mean() > 0.9:
        bot -= 1
    left = 0
    while left < w // 4 and same[:, left].mean() > 0.9:
        left += 1
    right = w
    while right > w - w // 4 and same[:, right - 1].mean() > 0.9:
        right -= 1
    return tex.crop((left, top, right, bot)), (left, top, w - right, h - bot)


def warp_side_whole(tex, face, drawn):
    """The side wall, each column given the wall's WHOLE height even where the doorway cuts it."""
    opaque = drawn[:, :, 3] > 200
    cols = {}
    for x in range(face.shape[1]):
        run = np.nonzero(face[:, x])[0]
        if run.size:
            cols[x] = (int(run.min()), int(run.max()))
    # the columns the door does not reach: their face runs down to the floor line
    door_x0 = 146
    clear = [x for x in cols if x < door_x0 - 2]
    m, c = np.polyfit(clear, [cols[x][1] for x in clear], 1)
    rows = {}
    for x, (top, bot) in cols.items():
        if x >= door_x0 - 2:
            bot = max(bot, int(round(m * x + c)))
        rows[x] = (top, bot)
    size = (face.shape[0], face.shape[1])           # transposed: (height, width)
    warped = r3.warp(tex.transpose(Image.TRANSPOSE), rows, size, near_at_bottom=True, repeat=3, inset=0)
    return warped.transpose(Image.TRANSPOSE)


def build():
    out = {}
    # C3 - the ceiling
    tex, cut = trim_frame(rgba(os.path.join(SRC, 'ceil_dusk.png')))
    print('  ceil_dusk frame cut', cut)
    csize, crows = r3.ceiling_rows()
    out['ceil7_dusk'] = (r3.warp(tex, crows, csize, near_at_bottom=False, inset=0), tex)
    # B2 - the back wall, laid on the plate of the rung below it (the chevron's own base)
    wave = rgba(os.path.join(SRC, 'back_wave.png'))
    plate = rgba(os.path.join(FIXT, 'fx_walls_3.png'))
    plate.alpha_composite(r4.fit_back(wave))
    out['back7_wave'] = (plate, wave)
    # F3 - the floor
    tex, cut = trim_frame(rgba(os.path.join(SRC, 'floor_marble.png')))
    print('  floor_marble frame cut', cut)
    fsize, frows = r4.floor_rows()
    out['floor7_marble'] = (r4.project_floor(tex, frows, fsize, inset=0), tex)
    # R2 - the right wall, the strip over the door at the wall's own scale
    tex, cut = trim_frame(rgba(os.path.join(SRC, 'rwall_deco.png')))
    print('  rwall_deco frame cut', cut)
    drawn, face = r6.right_wall_face()
    # THE THRESHOLD IS NOT WALL: the shared face test calls a pixel frame only when its grey is
    # brighter than 60, so the doorway's dark floor counted as wall and took a wedge of pattern (the
    # round-six walls carry it too). Any neutral grey, however dark, is the frame or the floor.
    rgb = drawn[:, :, :3].astype(int)
    neutral = (np.abs(rgb[:, :, 0] - rgb[:, :, 1]) < 10) & (np.abs(rgb[:, :, 1] - rgb[:, :, 2]) < 10)
    face = face & ~neutral
    surface = np.array(warp_side_whole(tex, face, drawn).convert('RGBA'))
    wall = drawn.copy()
    put = face & (surface[:, :, 3] > 0)
    wall[put, :3] = surface[put, :3]
    room = Image.new('RGBA', (640, 360), (0, 0, 0, 0))
    room.alpha_composite(Image.fromarray(wall), r6.RIGHT_AT)
    out['rwall7_deco'] = (room, tex)
    return out


RUNGS = [
    # id, slot, name, flavor, stars, price, comfort, group, after (the id it follows in the file)
    ('ceil7_dusk', 'ceiling', 'Dusk Ceiling',
     'A painted evening overhead, pink and lilac clouds on peach. The sun never quite goes down in here.',
     3.0, 210, 1.4, 'walls', 'ceil6_stars'),
    ('back7_wave', 'walls', 'Wave Mural',
     'One great wave curling across the whole wall under a sunset. Every seat at the bar faces the surf.',
     2.5, 185, 3.0, 'walls', 'back6_chevron'),
    ('floor7_marble', 'floor', 'Pink Marble',
     'Big pink slabs with white veins, polished matte. A floor that makes the drinks look cheaper than they are.',
     3.0, 215, 1.45, 'furniture', 'floor6_wave'),
    ('rwall7_deco', 'walls_right', 'Deco Fans',
     'Stacked fans in coral, mint and cream from the corner to the door. The side wall of a hotel on Ocean Drive.',
     3.0, 205, 2.5, 'walls', 'rwall6_sunray'),
]


def ship():
    art = build()
    for fid, (room, flat) in art.items():
        room.save(os.path.join(FIXT, 'fx_' + fid + '.png'))
        tree.busiest_window(flat).save(os.path.join(FIXT, 'fx_' + fid + '_swatch.png'))
        print('  art', fid)

    d = json.load(io.open(DATA, encoding='utf-8'), object_pairs_hook=OrderedDict)
    fx = d['fixtures']
    ids = [f['id'] for f in fx]
    for fid, slot, name, flavor, stars, price, comfort, group, after in RUNGS:
        entry = OrderedDict([('id', fid), ('name', name), ('slot', slot), ('price', price),
                             ('comfort', comfort), ('stars', stars), ('flavor', flavor),
                             ('sprite', 'fx_' + fid), ('swatch', 'fx_' + fid + '_swatch'),
                             ('level', 0), ('group', group)])
        if fid in ids:
            fx[ids.index(fid)] = entry
        else:
            fx.insert(ids.index(after) + 1, entry)
        ids = [f['id'] for f in fx]
    # every slot's ladder numbered again in the order the file gives it
    for slot in {r[1] for r in RUNGS}:
        n = 0
        for f in fx:
            if f['slot'] == slot:
                n += 1
                f['level'] = n
    text = json.dumps(d, indent=2, ensure_ascii=False)
    io.open(DATA, 'w', encoding='utf-8', newline='\n').write(text + '\n')
    print('  data', len(fx), 'fixtures')


if __name__ == '__main__':
    ship()
