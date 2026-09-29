# -*- coding: utf-8 -*-
"""EIGHTH ROUND (2026-09-29, the author: "Tüm oda için çeşitli layoutlar üretmeni istiyorum, duvarlar ayrı, parkeler
ayrı, tavan ayrı, arkadaki dekorasyonlar ayrı. 5 adet layout üret şu anki gibi lvl1den lvl5'e hissiyatını vermeli.
Görseldeki ışık yerine başka ışık üret ve kullan.")

FIVE WHOLE-ROOM LAYOUTS, one per rung of the room's standing, each drawn as its own layers so a pick can be taken
whole or piece by piece: the ceiling, the back wall, the floor, the right wall and one piece of back-wall decoration
(the centre picture). The layers are drawn and mapped exactly as rounds six and seven mapped theirs (their warps, their
projections, their side-wall transpose), so a pick drops into the room the way the last rounds' picks did.

  L1  the corner dive        what a bar looks like the night it opens: cheap, tired, honest
  L2  the beach shack        cleaned up with salvage and sun
  L3  the tropical lounge    somebody spent money on it
  L4  Art Deco Miami         the Ocean Drive the hostess remembers
  L5  the club               the room the fifth star buys

And ONE new counter lamp for the pendant the author circled (counter_lamps_globe): a tiered brass Art Deco pendant.
The lamp is drawn unlit and flat - its light is the room's Light2D, never baked in.

Nothing here enters Assets: the pieces land in Tools/room_variants8 and a picks page, and only the author's choice
ships. One call per piece, all candidates a call returns are kept (a 128 tile returns four, the 56x72 lamp sixteen).

  py -3 -X utf8 Tools/room_variants8_gen.py take     submit / collect (re-run until done)
  py -3 -X utf8 Tools/room_variants8_gen.py build    map every candidate into the room
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
OUT = os.path.join(HERE, 'room_variants8')
STATE = os.path.join(OUT, 'state.json')

sys.path.insert(0, HERE)
import room_variants3_gen as r3                    # noqa: E402
import room_variants4_gen as r4                    # noqa: E402
import room_variants6_gen as r6                    # noqa: E402
import room7_ship as r7                            # noqa: E402  trim_frame

FIXT = os.path.join(ROOT, 'Assets', 'Resources', 'Fixtures')
BACK_PLATE = {'L1': 'fx_walls_1.png', 'L2': 'fx_walls_2.png', 'L3': 'fx_walls_3.png',
              'L4': 'fx_walls_4.png', 'L5': 'fx_walls_4.png'}

MATTE, TILE, WALL, PROP, PIC = r6.MATTE, r6.TILE, r6.WALL, r6.PROP, r6.PIC
PX = ('pixel art for a 640x360 game, flat colours with hard pixel edges and a 1px darker outline where shapes meet, '
      'no anti-aliasing, no text, no letters, no watermark')

LAYOUTS = {
    'L1': ('the corner dive, the night a tired old bar reopens: cheap, worn, honest, muted colours - faded mint, '
           'nicotine cream, brown, dull teal'),
    'L2': ('a Miami beach shack bar, cleaned up with salvaged wood and sun: sky blue, white, sand, sea green, '
           'a little coral'),
    'L3': ('a tropical lounge bar somebody spent money on: coral pink, palm green, terracotta, cream, turquoise'),
    'L4': ('an Art Deco Miami Ocean Drive bar of the 1980s: pastel peach, mint, flamingo pink, cream with thin gold '
           'lines'),
    'L5': ('a luxurious late-night club: deep emerald, dark plum, black walnut and brass gold, with small hot pink '
           'accents'),
}

SURF = {
    # layout: (ceiling, back wall, floor, right wall, centre picture)
    'L1': ('an old ceiling of dark stained wooden planks running side by side, knots and two faint water stains',
           'a painted cinder block wall in faded mint, the paint chipped in a few small patches showing grey block',
           'worn linoleum floor squares in brown and cream, scuffed, laid in a plain grid',
           'cheap brown faux wood panelling with vertical grooves, a little scuffed',
           'a small cheap print of a sunset over the sea, faded colours, in a plain thin black frame'),
    'L2': ('a ceiling of pale gold bamboo poles laid side by side, tied with dark cord every so often',
           'horizontal painted wooden planks, sky blue and white alternating, weathered, a thin sand stripe near '
           'the top',
           'pale sanded boardwalk planks in light grey-beige wood with thin dark gaps between the boards',
           'tropical palm leaf wallpaper, big green fronds on a cream ground',
           'a vintage beach poster of a surfboard standing against a palm tree at the shore, in a pale wood frame'),
    'L3': ('a sea-green painted beadboard ceiling with a white lattice of thin painted battens',
           'a coral pink wall with a row of painted palm fronds rising along its bottom edge',
           'terracotta hexagonal floor tiles with cream grout lines',
           'a turquoise wall behind a white painted bamboo lattice',
           'a painting of three flamingos standing in a pink lagoon at sunset, in a bamboo frame'),
    'L4': ('a pastel pink coffered ceiling, square recessed panels edged with thin gold lines, Art Deco',
           'a pastel peach wall with tall fluted Art Deco pilasters and thin gold horizontal lines',
           'cream terrazzo floor with thin brass inlay lines forming a repeating Art Deco fan pattern',
           'a mint green wall with a repeating gold Art Deco fan motif',
           'an Art Deco sunburst frame in gold around a pastel painting of Ocean Drive palms at dusk'),
    'L5': ('a deep plum ceiling of square coffers, each with a small gold star in its centre and gold edges',
           'emerald green tufted velvet wall panels set in thin brass frames, matte velvet, no shine',
           'dark green marble floor slabs with soft gold veins and thin brass borders, matte, not polished',
           'dark walnut wood wall panels with thin vertical brass inlay stripes',
           'a large painting in a heavy gold frame: one palm tree before a hot pink and cyan sunset, flat colours'),
}

JOBS = {}
for n, (L, mood) in enumerate(LAYOUTS.items()):
    ceil, back, floor, rwall, pic = SURF[L]
    s = 8100 + n * 10
    tail = ', ' + mood + ', ' + PX
    JOBS[L + '_ceil'] = (128, 128, s + 1, TILE + ceil + tail)
    JOBS[L + '_back'] = (172, 76, s + 3, WALL + back + tail)
    JOBS[L + '_floor'] = (128, 128, s + 5, TILE + floor + tail)
    JOBS[L + '_rwall'] = (128, 128, s + 7, TILE + rwall + tail)
    JOBS[L + '_pic'] = (160, 58, s + 9, PIC + pic + tail)
JOBS['lamp_deco'] = (56, 72, 8199, PROP + 'a hanging pendant bar lamp on a thin brass rod from the top edge: a tiered '
                     'Art Deco shade of three stepped brass rings over frosted fluted glass, narrow and tall, the lamp '
                     'switched OFF, no glow, no light rays, flat colours, ' + PX)


def _state():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def _save(s):
    os.makedirs(OUT, exist_ok=True)
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(s, indent=1))


def _keep(key, images):
    for i, data in enumerate(images):
        io.open(os.path.join(OUT, '%s_%d.png' % (key, i)), 'wb').write(data)
    print('  -> %s (%d)' % (key, len(images)))


def take(only=None):
    os.makedirs(OUT, exist_ok=True)
    s = _state()
    waiting = 0
    for key, (w, h, seed, desc) in JOBS.items():
        if only and not any(key.startswith(o) for o in only):
            continue
        if os.path.exists(os.path.join(OUT, key + '_0.png')):
            continue
        assert len(desc) <= 2000, (key, len(desc))
        e = s.get(key) or {}
        if e.get('job'):
            text, images = r4._call('get_image', {'job_id': e['job']}, timeout=180)
            if images:
                _keep(key, images)
            else:
                waiting += 1
            continue
        text, images = r4._call('create_image_pro', {'description': desc, 'width': w, 'height': h,
                                                     'no_background': key.endswith('_pic') or key.startswith('lamp'),
                                                     'seed': seed})
        if images:
            _keep(key, images)
            continue
        m = re.search(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}', text, re.I)
        s.setdefault(key, {})['job'] = m.group(0) if m else None
        _save(s)
        waiting += 1
        print('  queued %s %s' % (key, m.group(0) if m else text.strip()[:120]))
    have = sum(1 for k in JOBS if os.path.exists(os.path.join(OUT, k + '_0.png')))
    print('%d of %d drawn, %d still cooking' % (have, len(JOBS), waiting))


def build():
    fsize, frows = r4.floor_rows()
    csize, crows = r3.ceiling_rows()
    drawn, face = r6.right_wall_face()
    for name in sorted(os.listdir(OUT)):
        m = re.match(r'(L\d_(ceil|back|floor|rwall))_(\d+)\.png$', name)
        if not m:
            continue
        tex = Image.open(os.path.join(OUT, name)).convert('RGBA')
        kind = m.group(2)
        dst = os.path.join(OUT, 'fx_' + name)
        # A repeated tile keeps no frame (round seven's lesson): PixelLab leaves 1-5px of one colour round some of
        # them, and warped into the room that frame repeats as a seam every tile.
        if kind != 'back':
            tex, _cut = r7.trim_frame(tex)
        if kind == 'floor':
            r4.project_floor(tex, frows, fsize).save(dst)
        elif kind == 'ceil':
            r3.warp(tex, crows, csize, near_at_bottom=False).save(dst)
        elif kind == 'back':
            # The back wall is the plate's middle only, so it is laid on the whole plate of its rung - the one
            # the room would be wearing - the way round seven laid the wave on the chevron's base.
            plate = Image.open(os.path.join(FIXT, BACK_PLATE[name[:2]])).convert('RGBA')
            plate.alpha_composite(r4.fit_back(tex))
            plate.save(dst)
        else:
            surface = np.array(r6.warp_side(tex, face).convert('RGBA'))
            out = drawn.copy()
            put = face & (surface[:, :, 3] > 0)
            out[put, :3] = surface[put, :3]
            room = Image.new('RGBA', (640, 360), (0, 0, 0, 0))
            room.alpha_composite(Image.fromarray(out), r6.RIGHT_AT)
            room.save(dst)
        print('  built', name)
    print('build done ->', OUT)


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'take'
    if cmd == 'take':
        take(sys.argv[2:] or None)
    elif cmd == 'build':
        build()
    else:
        raise SystemExit(__doc__)
