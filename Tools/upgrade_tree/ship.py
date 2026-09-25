# -*- coding: utf-8 -*-
"""SHIP THE TREE (2026-09-13, the author: "Mevcut oyun açılış v2 gerçekleştirilsin. Mekan
geliştirme sistemine güncelleme ... Her bölümün ayrı kısmı olmalı markette. Örneğin duvar
dendiğinde tüm seçenekler desen png si ile gözükmeli").

NOTE (2026-09-23): fixtures.json is HAND-OWNED now: its prices were re-set by hand and every row
carries a "buff"/"buffPct" pair (the fitting buffs) that this script does not write — re-running
it would drop both, so carry any new rung into fixtures.json by hand.

The upgrade tree is where the author sets each ladder's tiers and leaves out what they do not
want (Tools/upgrade_tree/decisions.json, read back from the tree page's own database). This
turns those choices into the game: every rung that stayed in, in the order it was given, as
art in Assets/Resources/Fixtures and as data in Assets/Data/fixtures/fixtures.json.

What each kind of rung becomes
  overlay layer   a whole 640x360 canvas laid over the plate at its slot's order — the right
                  wall (12), the ceiling and the floor (13)
  plate           the back wall is the plate itself, so a rung drawn as a patch of it is laid
                  onto the plate of the rung below it and saved whole
  sprite          one drawing stood at its slot's hook, cropped to what is drawn; a piece the
                  author saw in the tree centred where the old one hung keeps that spot
  re-hung         the right wall's picture and posters are warped onto the wall again: the
                  posters share the TELEVISION'S spot and size (the author: "posterlerle
                  televizyon aynı klasmanda olacak"), and the picture hangs above it ("diğer sağ
                  duvar tablosu televizyonun altında üstünde gözükecek, duvar kalabalıklaşacak")
  swatch          the pattern the market shows (64x48, the busiest window of the flat tile the
                  rung was mapped from) or the flat picture itself for a poster, a painting or
                  a rug

The room opens as "Oyun açılış v2": the cracked back wall, the peeling right wall, the bistro
tables, the steel sink and both mats — and the tap and the tin, which are tools and are only
ever upgraded; each tool's next rung WORKS faster (the author: "upgrade etmenin üretime buffu
olacak — musluk hızlı temizlerken shaker hızlı çalkalayacak").

Prices, comfort and star gates here are the first draft for the sim to move (GDD 27 §7).
Re-running is safe: every picture is made again from its source, and the data from this file.

  py -3 -X utf8 Tools/upgrade_tree/ship.py
"""
import json
import os
from collections import OrderedDict

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
ROOT = os.path.dirname(TOOLS)
FIXT = os.path.join(ROOT, 'Assets', 'Resources', 'Fixtures')
DATA = os.path.join(ROOT, 'Assets', 'Data', 'fixtures', 'fixtures.json')
LAYERS = os.path.join(HERE, 'layers')
W, H = 640, 360


def tool(*p):
    return os.path.join(TOOLS, *p)


# ── the right wall's receding face (Tools/room_variants4_gen.py, measured off the plate) ──
R_X0, R_TOPM, R_TOPC, R_BOTM, R_BOTC = 493.0, -0.5000, 73.0, 0.4571, 217.0


def right_top(x):
    return R_TOPC + R_TOPM * (x - R_X0)


def right_bot(x):
    return R_BOTC + R_BOTM * (x - R_X0)


def hang_right(pic, xa, xb, f0, f1):
    """A flat picture onto the right wall between screen columns xa..xb and heights f0..f1
    of the wall. The same mapping room_variants4_gen._hang uses: hyperbolic across the wall,
    because distance along a receding wall goes as 1/d from the vanishing column."""
    out = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    op = out.load()
    pp = pic.convert('RGBA').load()
    pw, ph = pic.size
    a, b = -4000.0, 4000.0
    for _ in range(60):
        m = (a + b) * 0.5
        if (right_top(m) - right_bot(m)) * (right_top(a) - right_bot(a)) > 0:
            a = m
        else:
            b = m
    xvp = (a + b) * 0.5
    ka, kb = 1.0 / (xa - xvp), 1.0 / (xb - xvp)
    for x in range(int(min(xa, xb)), int(max(xa, xb)) + 1):
        u = (ka - 1.0 / (x - xvp)) / (ka - kb)
        sx = min(pw - 1, max(0, int(u * (pw - 1) + 0.5)))
        top, bot = right_top(x), right_bot(x)
        ya, yb = top + f0 * (bot - top), top + f1 * (bot - top)
        for y in range(int(round(ya)), int(round(yb)) + 1):
            v = (y - ya) / max(1.0, (yb - ya))
            sy = min(ph - 1, max(0, int(v * (ph - 1) + 0.5)))
            c = pp[sx, sy]
            if c[3] > 40 and 0 <= x < W and 0 <= y < H:
                op[x, y] = (c[0], c[1], c[2], 255)
    return out


# The television's footprint is (507,121)-(556,170). A poster takes the same stretch of wall,
# a little taller because it is portrait; the painting hangs over both with air between.
POSTER_HANG = dict(xa=512, xb=551, f0=0.355, f1=0.66)
PICTURE_HANG = dict(xa=513, xb=549, f0=0.055, f1=0.31)


# ── the art each shipped rung is made from ──────────────────────────────────────────────
#   ('overlay', layer)            ('plate', layer, base plate)      ('sprite', drawing)
#   ('whole', drawing)            ('poster'|'picture', flat source)
# and the swatch: ('pattern', tile) | ('flat', picture) | None
ART = {
    # the back wall
    'back6_chevron': (('plate', tool('room_variants6', 'fx_back_chevron.png'), 'fx_walls_3'),
                      ('pattern', tool('room_variants6', 'back_chevron.png'))),
    # the right wall
    'rwall6_stripe': (('overlay', tool('room_variants6', 'fx_rwall_stripe.png')),
                      ('pattern', tool('room_variants6', 'rwall_stripe.png'))),
    'rwall6_tile': (('overlay', tool('room_variants6', 'fx_rwall_tile.png')),
                    ('pattern', tool('room_variants6', 'rwall_tile.png'))),
    'rwall6_sunray': (('overlay', tool('room_variants6', 'fx_rwall_sunray.png')),
                      ('pattern', tool('room_variants6', 'rwall_sunray.png'))),
    # the ceiling
    'ceil_beams': (('overlay', tool('room_variants3', 'fx_ceil_beams.png')),
                   ('pattern', tool('room_variants3', 'ceil_beams.png'))),
    'ceil_deco': (('overlay', tool('room_variants3', 'fx_ceil_deco.png')),
                  ('pattern', tool('room_variants3', 'ceil_deco.png'))),
    'ceil_palm': (('overlay', tool('room_variants3', 'fx_ceil_palm.png')),
                  ('pattern', tool('room_variants3', 'ceil_palm.png'))),
    'ceil_stucco': (('overlay', tool('room_variants3', 'fx_ceil_stucco.png')),
                    ('pattern', tool('room_variants3', 'ceil_stucco.png'))),
    'ceil6_grid': (('overlay', tool('room_variants6', 'fx_ceil_grid.png')),
                   ('pattern', tool('room_variants6', 'ceil_grid.png'))),
    'ceil6_ray': (('overlay', tool('room_variants6', 'fx_ceil_ray.png')),
                  ('pattern', tool('room_variants6', 'ceil_ray.png'))),
    'ceil6_stars': (('overlay', tool('room_variants6', 'fx_ceil_stars.png')),
                    ('pattern', tool('room_variants6', 'ceil_stars.png'))),
    # the floor
    'floor_terra': (('overlay', tool('room_variants5', 'fx_floor_terra.png')),
                    ('pattern', tool('room_variants5', 'floor_terra.png'))),
    'floor_carpet': (('overlay', tool('room_variants4', 'fx_floor_carpet.png')),
                     ('pattern', tool('room_variants3', 'floor_carpet.png'))),
    'floor_marble': (('overlay', tool('room_variants4', 'fx_floor_marble.png')),
                     ('pattern', tool('room_variants3', 'floor_marble.png'))),
    'floor_check': (('overlay', tool('room_variants4', 'fx_floor_check.png')),
                    ('pattern', tool('room_variants3', 'floor_check.png'))),
    'floor6_grid': (('overlay', tool('room_variants6', 'fx_floor_grid.png')),
                    ('pattern', tool('room_variants6', 'floor_grid.png'))),
    'floor6_chip': (('overlay', tool('room_variants6', 'fx_floor_chip.png')),
                    ('pattern', tool('room_variants6', 'floor_chip.png'))),
    'floor6_wave': (('overlay', tool('room_variants6', 'fx_floor_wave.png')),
                    ('pattern', tool('room_variants6', 'floor_wave.png'))),
    # the rug
    'rug_leopard': (('whole', tool('room_variants5', 'piece_rug_leopard.png')),
                    ('flat', tool('room_variants5', 'rug_leopard.png'))),
    'rug_wave': (('whole', tool('room_variants5', 'piece_rug_wave.png')),
                 ('flat', tool('room_variants5', 'rug_wave.png'))),
    'rug6_memphis': (('whole', tool('room_variants6', 'piece_rug_memphis.png')),
                     ('flat', tool('room_variants6', 'rug_memphis.png'))),
    'rug6_sunset': (('whole', tool('room_variants6', 'piece_rug_sunset.png')),
                    ('flat', tool('room_variants6', 'rug_sunset.png'))),
    'rug6_palm': (('whole', tool('room_variants6', 'piece_rug_palm.png')),
                  ('flat', tool('room_variants6', 'rug_palm.png'))),
    # the tables
    'table5_bistro_L': (('sprite', tool('room_variants5', 'table5_bistro.png')), None),
    'table5_bistro_R': (('sprite', tool('room_variants5', 'table5_bistro.png')), None),
    'table_v1_L': (('sprite', tool('room_variants', 'table_v1.png')), None),
    'table_v1_R': (('sprite', tool('room_variants', 'table_v1.png')), None),
    'table_v2_L': (('sprite', tool('room_variants', 'table_v2.png')), None),
    'table_v2_R': (('sprite', tool('room_variants', 'table_v2.png')), None),
    # the plants
    'plant5_yucca_L': (('sprite', tool('room_variants5', 'plant5_yucca.png')), None),
    # the picture in the middle of the back wall
    'art_city': (('hung', tool('room_variants', 'art_city.png')), None),
    'picS_trio': (('hung', tool('room_variants3', 'picS_trio.png')), None),
    'picS_trioA': (('hung', tool('room_variants5', 'picS_trioA.png')), None),
    'picS_trioB': (('hung', tool('room_variants5', 'picS_trioB.png')), None),
    # the lamps (one drawing, both brackets — the 40x40 canvas IS the bracket's)
    'lamp6_scallop': (('whole', tool('room_variants6', 'lamp_scallop.png')), None),
    'lamp6_neonbar': (('whole', tool('room_variants6', 'lamp_neonbar.png')), None),
    'lamp6_shell': (('whole', tool('room_variants6', 'lamp_shell.png')), None),
    # the neon sign
    'neon_flamingo': (('sprite', tool('room_variants5', 'neon_flamingo.png')), None),
    'neon6_sun': (('sprite', tool('room_variants6', 'neon_sun.png')), None),
    'neon6_glass': (('sprite', tool('room_variants6', 'neon_glass.png')), None),
    'neon6_bird': (('sprite', tool('room_variants6', 'neon_bird.png')), None),
    # the television's spot: the posters
    'post_malibu_R': (('poster', tool('room_variants4', 'post_malibu.png')), ('flat', tool('room_variants4', 'post_malibu.png'))),
    'post_surf_R': (('poster', tool('room_variants4', 'post_surf.png')), ('flat', tool('room_variants4', 'post_surf.png'))),
    'post_pier_R': (('poster', tool('room_variants4', 'post_pier.png')), ('flat', tool('room_variants4', 'post_pier.png'))),
    'post_coast_R': (('poster', tool('room_variants4', 'post_coast.png')), ('flat', tool('room_variants4', 'post_coast.png'))),
    'post5_neon': (('poster', tool('room_variants5', 'post5_neon.png')), ('flat', tool('room_variants5', 'post5_neon.png'))),
    'post5_car': (('poster', tool('room_variants5', 'post5_car.png')), ('flat', tool('room_variants5', 'post5_car.png'))),
    'post5_diver': (('poster', tool('room_variants5', 'post5_diver.png')), ('flat', tool('room_variants5', 'post5_diver.png'))),
    # over the television: the painting
    'pic_pelican': (('picture', tool('room_variants3', 'picR_pelican.png')), ('flat', tool('room_variants3', 'picR_pelican.png'))),
    'picR6_sunset': (('picture', tool('room_variants6', 'picR_sunset.png')), ('flat', tool('room_variants6', 'picR_sunset.png'))),
    'picR6_conv': (('picture', tool('room_variants6', 'picR_conv.png')), ('flat', tool('room_variants6', 'picR_conv.png'))),
    'picR6_shapes': (('picture', tool('room_variants6', 'picR_shapes.png')), ('flat', tool('room_variants6', 'picR_shapes.png'))),
}

# Swatches for rungs already in the game whose market picture was the sprite itself.
EXTRA_SWATCH = {
    'floor_rug': ('flat', os.path.join(TOOLS, 'AssetPipeline', 'sources', 'konsept_art', 'floor_rug.png')),
    'wall_tv': ('cell', os.path.join(FIXT, 'fx_tv.png'), 49),
}

# ── the words and the numbers ───────────────────────────────────────────────────────────
# name, flavor, price, comfort, stars (+ light, workSpeed). A rung already in the game keeps
# its own words unless a field is named here.
SPEC = {
    # THE BACK WALL — the biggest rungs in the shop, as the author asked
    'back6_chevron': dict(name='Chevron Wall', price=165, comfort=2.75, stars=1.5,
                          flavor='Coral, cream and teal zigzags from corner to corner over the panelling. The room stops standing still.'),
    'walls_4': dict(price=200, comfort=3.25, stars=2.0),
    # THE RIGHT WALL
    'rwall6_stripe': dict(name='Flamingo Stripes', price=140, comfort=1.75, stars=2.5,
                          flavor='Pink and cream bands running back to the door. The side wall finally looks like it belongs to a bar on the coast.'),
    'rwall6_tile': dict(name='Turquoise Diamonds', price=160, comfort=2.0, stars=3.0,
                        flavor='Teal diamonds on cream, set square to the wall as it runs away from you. A pool you can lean on.'),
    'rwall6_sunray': dict(name='Sunray Wall', price=180, comfort=2.25, stars=3.5,
                          flavor='A sunset breaking out of the corner in coral and magenta rays. Nobody leaves by that door without looking at it.'),
    # THE CEILING
    'ceil_beams': dict(name='Beamed Ceiling', price=40, comfort=0.25, stars=0,
                       flavor='Dark beams across the plaster. The room gets a lid, and the lamps something to throw light at.'),
    'ceil_deco': dict(name='Deco Ceiling', price=60, comfort=0.4, stars=0.5,
                      flavor='Stepped cream mouldings in long panels. It looks like the ceiling of somewhere that charges more.'),
    'ceil_palm': dict(name='Palm Ceiling', price=85, comfort=0.55, stars=1.0,
                      flavor='Fronds painted overhead, as if the bar were under a tree. The smoke drifts through them.'),
    'ceil_stucco': dict(name='Stucco Ceiling', price=110, comfort=0.7, stars=1.5,
                        flavor='Hand-swirled plaster in warm white. Every lamp in the room looks softer under it.'),
    'ceil6_grid': dict(name='Rosette Panels', price=135, comfort=0.85, stars=2.0,
                       flavor='Square coffers, each with a pink rosette at its heart. A ceiling you notice on your second drink.'),
    'ceil6_ray': dict(name='Sunray Ceiling', price=160, comfort=1.0, stars=2.5,
                      flavor='Coral and magenta rays from the middle of the room outward. The whole place leans towards the bar.'),
    'ceil6_stars': dict(name='Star Ceiling', price=185, comfort=1.2, stars=3.0,
                        flavor='Night blue with gold stars pricked through it. Closing time outside, happy hour in here.'),
    # THE FLOOR
    'floor_terra': dict(name='Terracotta', price=45, comfort=0.3, stars=0,
                        flavor='Warm clay tiles over the boards. It hides a spilled drink better than wood ever did.'),
    'floor_carpet': dict(name='Carpet', price=65, comfort=0.45, stars=0.5,
                         flavor='Plum carpet with a busy pattern, the kind that forgives everything. The room goes quiet underfoot.'),
    'floor_marble': dict(name='Marble Floor', price=90, comfort=0.6, stars=1.0,
                         flavor='Cold white stone with grey veins. Heels sound expensive on it.'),
    'floor_check': dict(name='Checker Floor', price=115, comfort=0.75, stars=1.5,
                        flavor='Black and white squares running to the back wall. Every diner in every film had one.'),
    'floor6_grid': dict(name='Neon Grid', price=140, comfort=0.9, stars=2.0,
                        flavor='Dark tiles lined in pink and cyan, straight out of a video game. Walking to the bar feels like level one.'),
    'floor6_chip': dict(name='Miami Terrazzo', price=165, comfort=1.05, stars=2.5,
                        flavor='Chips of coral, teal and cream set in polished cream. A hotel lobby floor for a room that is not one.'),
    'floor6_wave': dict(name='Wave Tiles', price=190, comfort=1.25, stars=3.0,
                        flavor='Teal waves rolling across the whole floor. The tide came in and stayed for a drink.'),
    # THE RUG
    'floor_rug': dict(price=35, comfort=0.2, stars=0),
    'rug_leopard': dict(name='Leopard Rug', price=55, comfort=0.3, stars=0.5,
                        flavor='Spots from wall to wall, with a teal border to keep them in. Loud, and proud of it.'),
    'rug_wave': dict(name='Wave Rug', price=75, comfort=0.4, stars=1.0,
                     flavor='A breaking wave woven across the boards. The tables look like they are standing on the shore.'),
    'rug6_memphis': dict(name='Memphis Rug', price=95, comfort=0.5, stars=1.5,
                         flavor='Squiggles, dots and triangles in every colour the eighties sold. It argues with the walls and wins.'),
    'rug6_sunset': dict(name='Sunset Bands', price=115, comfort=0.6, stars=2.0,
                        flavor='Stripes from gold to magenta to night, the evening laid flat on the floor.'),
    'rug6_palm': dict(name='Palm Leaf Rug', price=135, comfort=0.7, stars=2.5,
                      flavor='Big green fronds on cream. It makes the whole floor feel like somebody\'s verandah.'),
    # THE TABLES — the bar opens with the bistro set, and it is worth nothing yet
    'table5_bistro_L': dict(name='Bistro Table', price=30, comfort=0, stars=0,
                            flavor='A square top and two bentwood chairs that came with the lease. Nobody sits there long.'),
    'table5_bistro_R': dict(name='Bistro Table', price=30, comfort=0, stars=0,
                            flavor='A square top and two bentwood chairs that came with the lease. Nobody sits there long.'),
    'table_v1_L': dict(name='Cocktail High-Top', price=60, comfort=0.25, stars=0.5,
                       flavor='A tall round top on a brass stem, two red stools and a glass waiting on it. People stay for a second round.'),
    'table_v1_R': dict(name='Cocktail High-Top', price=60, comfort=0.25, stars=0.5,
                       flavor='A tall round top on a brass stem, two red stools and a glass waiting on it. People stay for a second round.'),
    'table_v2_L': dict(name='Teal Pedestal Set', price=90, comfort=0.45, stars=1.5,
                       flavor='A teal top on a chrome pedestal, stools to match and the bottles already on it. The seat people ask for.'),
    'table_v2_R': dict(name='Teal Pedestal Set', price=90, comfort=0.45, stars=1.5,
                       flavor='A teal top on a chrome pedestal, stools to match and the bottles already on it. The seat people ask for.'),
    'table_left_3': dict(price=120, comfort=0.6, stars=3.0),
    'table_right_3': dict(price=120, comfort=0.6, stars=3.0),
    # THE PLANTS
    'plant5_yucca_L': dict(name='Yucca', price=55, comfort=0.2, stars=1.5,
                           flavor='A spiky crown on a bare cane, in a terracotta pot. It stands guard by the window.'),
    'plant_agave': dict(price=45, comfort=0.2, stars=0),
    # THE PICTURE IN THE MIDDLE
    'art_city': dict(name='City Triptych', price=45, comfort=0.2, stars=0,
                     flavor='The skyline at night in three frames. The only view in the room that is not out of the window.'),
    'picS_trio': dict(name='Pop Trio', price=70, comfort=0.35, stars=1.0,
                      flavor='A flamingo, two cherries and a pair of lips, one small frame each. Somebody always asks where they are from.'),
    'picS_trioA': dict(name='Cocktail Trio', price=95, comfort=0.5, stars=1.5,
                       flavor='A cocktail, a palm and a sun in gold frames. The house style, hung where everybody can read it.'),
    'picS_trioB': dict(name='Vinyl Trio', price=120, comfort=0.7, stars=2.5,
                       flavor='A record, a parrot and a wave, framed in teal and red. The loudest wall in the room without a speaker on it.'),
    'flamingo_triptych': dict(price=150, comfort=0.9, stars=3.0),
    # THE LAMPS
    'lamp6_scallop': dict(name='Scallop Sconces', price=110, comfort=0.8, stars=3.0,
                          flavor='Cream shades on brass arms, washing the wall in warm light. The kind of lamp a hotel bar has.',
                          light=(1.0, 0.86, 0.66, 1.05, 136)),
    'lamp6_neonbar': dict(name='Neon Tube Sconces', price=130, comfort=0.9, stars=3.5,
                          flavor='A pink tube and a cyan tube side by side on each bracket. The wall hums, and so does the room.',
                          light=(0.95, 0.55, 0.95, 1.0, 140)),
    'lamp6_shell': dict(name='Shell Lamps', price=150, comfort=1.0, stars=4.0,
                        flavor='Coral seashells glowing from inside. Everybody under them looks like they have been on holiday.',
                        light=(1.0, 0.62, 0.52, 1.15, 148)),
    # THE NEON SIGN
    'neon_martini': dict(price=50, comfort=0.2, stars=1.0),
    'neon_flamingo': dict(name='Neon Flamingo', price=75, comfort=0.3, stars=1.5,
                          flavor='A pink flamingo on one leg, buzzing faintly. It never gets tired of standing there.',
                          light=(1.0, 0.35, 0.8, 0.8, 55)),
    'neon6_sun': dict(name='Neon Sunset', price=100, comfort=0.4, stars=2.0,
                      flavor='A half sun sinking into pink lines. It sets every night at the same time the bar opens.',
                      light=(1.0, 0.4, 0.72, 0.8, 55)),
    'neon6_glass': dict(name='Neon Glass', price=125, comfort=0.5, stars=2.5,
                        flavor='A tall glass in cyan with pink inside and a cherry on the rim. The sign says what the bar is for.',
                        light=(0.85, 0.6, 1.0, 0.8, 55)),
    'neon6_bird': dict(name='Neon Pelican', price=150, comfort=0.6, stars=3.0,
                       flavor='A pelican in white and pink tube, beak full. The locals call the bar after it.',
                       light=(1.0, 0.55, 0.85, 0.8, 55)),
    # THE SCREEN'S SPOT — the set first, then the posters that can hang in its place
    'wall_tv': dict(price=70, comfort=0.2, stars=0),
    'post_malibu_R': dict(name='Malibu Poster', price=80, comfort=0.25, stars=0.5,
                          flavor='A lifeguard hut under a burning sky, on a poster curling at one corner.'),
    'post_surf_R': dict(name='Surf Poster', price=90, comfort=0.3, stars=1.0,
                        flavor='A surfer on a teal wave, printed so big the grain shows. Nobody here has surfed.'),
    'post_pier_R': dict(name='Pier Poster', price=100, comfort=0.35, stars=1.5,
                        flavor='The pier at dusk with its lights coming on. The one postcard every tourist sends.'),
    'post_coast_R': dict(name='Coast Poster', price=110, comfort=0.4, stars=2.0,
                         flavor='The coast road from above, palms and sea and a thin white car. The drive everybody means to take.'),
    'post5_neon': dict(name='Neon Avenue Poster', price=120, comfort=0.45, stars=2.5,
                       flavor='A street of pink and blue signs in the rain. It makes the room feel like part of a city.'),
    'post5_car': dict(name='Convertible Poster', price=130, comfort=0.5, stars=3.0,
                      flavor='A teal convertible parked facing the sunset, top down. The car the owner says he nearly bought.'),
    'post5_diver': dict(name='Diver Poster', price=140, comfort=0.55, stars=3.5,
                        flavor='A diver in mid-air over a turquoise pool. The whole bar holds its breath with them.'),
    # OVER THE SCREEN — the painting
    'pic_pelican': dict(name='Pelican', price=40, comfort=0.15, stars=0,
                        flavor='A pelican on a post in a dark wood frame. It watches the door so the bartender does not have to.'),
    'picR6_sunset': dict(name='Ocean Sunset', price=60, comfort=0.25, stars=1.0,
                         flavor='Two palms, a pink sun and a turquoise sea, in a thin gold frame.'),
    'picR6_conv': dict(name='Coast Road', price=85, comfort=0.35, stars=2.0,
                       flavor='A white convertible on a road along the sea. Somebody painted it from memory.'),
    'picR6_shapes': dict(name='Memphis Shapes', price=110, comfort=0.45, stars=3.0,
                         flavor='Triangles, circles and squiggles in the colours of the room. Nobody agrees what it is.'),
    # THE TOOLS — they are only ever upgraded, and the upgrade is WORK: the tap pours faster,
    # the basin washes faster (washSeconds, 2026-09-06), the tin shakes faster
    'taps_two': dict(workSpeed=1.2),
    'taps_three': dict(workSpeed=1.4),
    'shaker_gold': dict(workSpeed=1.5),
}

GROUP_OF = {
    'walls': 'walls', 'walls_right': 'walls', 'ceiling': 'walls',
    'wall_center': 'wall_art', 'wall_tv': 'wall_art', 'wall_right_art': 'wall_art',
    'wall_lamps': 'light', 'wall_right': 'light',
    'floor': 'furniture', 'floor_rug': 'furniture', 'table_left': 'furniture', 'table_right': 'furniture',
    'plant_left': 'greenery', 'plant_right': 'greenery',
    'taps': 'counter', 'sink': 'counter', 'shaker': 'counter', 'beer_mat': 'counter', 'prep_mat': 'counter',
}

# What each slot is called on the market's shelf, and where its rung goes.
TITLES = {
    'walls': ('Back wall', 'The back wall'), 'walls_right': ('Right wall', 'The right wall'),
    'ceiling': ('Ceiling', 'The ceiling'), 'wall_center': ('Centre picture', 'The back wall'),
    'wall_tv': ('Screen & posters', 'The right wall'), 'wall_right_art': ('Picture over the screen', 'The right wall'),
    'wall_lamps': ('Wall lamps', 'The back wall'), 'wall_right': ('Neon sign', 'The back wall'),
    'floor': ('Floor', 'The floor'), 'floor_rug': ('Rug', 'The floor'),
    'table_left': ('Left table', 'The floor'), 'table_right': ('Right table', 'The floor'),
    'plant_left': ('Left plant', 'By the window'), 'plant_right': ('Right plant', 'The counter'),
    'taps': ('Beer tower', 'The counter'), 'sink': ('Sink', 'The counter'), 'shaker': ('Shaker', 'In your hand'),
    'beer_mat': ('Drip mat', 'The counter'), 'prep_mat': ('Snack mat', 'The counter'),
}

# Slots the tree carried that the game did not have. The ceiling and the floor are layers of
# the room's picture drawn over the plate and the right wall (13); the painting is a hook.
NEW_SLOTS = [
    OrderedDict([('id', 'ceiling'), ('x', 320), ('y', 324), ('backdrop', True), ('overlay', True), ('order', 13)]),
    OrderedDict([('id', 'floor'), ('x', 320), ('y', 60), ('backdrop', True), ('overlay', True), ('order', 13)]),
    OrderedDict([('id', 'wall_right_art'), ('x', 531), ('y', 250), ('hangs', True)]),
]

# The order a shelf lists its ladders in: the surfaces, then what hangs, stands or sits on them.
SHELF_ORDER = ['walls', 'walls_right', 'ceiling',
               'wall_center', 'wall_tv', 'wall_right_art',
               'wall_lamps', 'wall_right',
               'floor', 'floor_rug', 'table_left', 'table_right',
               'plant_left', 'plant_right',
               'taps', 'sink', 'shaker', 'beer_mat', 'prep_mat']

# Where the tree's slots land in the game: the posters share the screen's spot.
SLOT_INTO = {'poster_right': 'wall_tv'}
NOT_SHIPPED_SLOTS = {'bar_top', 'poster_left'}     # everything in them was left out

# A candidate in the tree stood where the piece named here stands; once a piece leaves the
# game, whatever was stood like it in the tree is stood like its replacement instead.
LIKE_AFTER = {'table_left_1': 'table_left_3', 'table_left_2': 'table_left_3',
              'table_right_1': 'table_right_3', 'table_right_2': 'table_right_3',
              'plant_snake': 'plant_agave', 'plant_fiddle': 'plant_palm'}


# ── pictures ────────────────────────────────────────────────────────────────────────────

def rgba(path):
    return Image.open(path).convert('RGBA')


def save(im, name):
    im.save(os.path.join(FIXT, name + '.png'))


def busiest_window(tile, size=(64, 48)):
    """The 64x48 window of a flat tile with the most going on in it — the part of a pattern a
    swatch should show. Every window is scanned, wrapping, because a tile repeats."""
    a = np.array(tile.convert('RGBA'))
    h, w = a.shape[:2]
    sw, sh = size
    wrap = np.concatenate([a, a], axis=1)
    wrap = np.concatenate([wrap, wrap], axis=0)
    best, at = -1.0, (0, 0)
    for y in range(0, h, 2):
        for x in range(0, w, 2):
            win = wrap[y:y + sh, x:x + sw]
            if (win[:, :, 3] < 250).any():
                continue
            score = float(win[:, :, :3].astype(float).std(axis=(0, 1)).sum())
            if score > best:
                best, at = score, (x, y)
    x, y = at
    return Image.fromarray(wrap[y:y + sh, x:x + sw].copy())


def cropped(im):
    return im.crop(im.getbbox())


def tree_centre_of(like_id):
    """The middle of where the piece named stands in the exported room."""
    x0, y0, x1, y1 = rgba(os.path.join(LAYERS, 'fx_' + like_id + '.png')).getbbox()
    return (x0 + x1) / 2.0, (y0 + y1) / 2.0


def hook_for(x0, y0, w, h):
    """A fixture's own x/y: the bottom-centre of its drawing in stage units (y up), with
    the left and bottom edges on whole pixels."""
    return x0 + w / 2.0, float(H - (y0 + h))


def make_art(fid):
    """Draws a rung's picture into Resources and says where it stands, if not at its hook."""
    kind = ART[fid][0]
    where = None
    if kind[0] == 'overlay':
        layer = rgba(kind[1])
        assert layer.size == (W, H), fid
        save(layer, 'fx_' + fid)
    elif kind[0] == 'plate':
        plate = rgba(os.path.join(FIXT, kind[2] + '.png'))
        plate.alpha_composite(rgba(kind[1]))
        save(plate, 'fx_' + fid)
    elif kind[0] == 'whole':
        save(rgba(kind[1]), 'fx_' + fid)
    elif kind[0] == 'sprite':
        save(cropped(rgba(kind[1])), 'fx_' + fid)
    elif kind[0] == 'hung':
        # The author saw these in the tree hung by their MIDDLE where the triptych hangs.
        spr = cropped(rgba(kind[1]))
        cx, cy = tree_centre_of('flamingo_triptych')
        x0, y0 = int(round(cx - spr.width / 2.0)), int(round(cy - spr.height / 2.0))
        save(spr, 'fx_' + fid)
        where = hook_for(x0, y0, spr.width, spr.height)
    elif kind[0] in ('poster', 'picture'):
        hang = POSTER_HANG if kind[0] == 'poster' else PICTURE_HANG
        room = hang_right(rgba(kind[1]), **hang)
        box = room.getbbox()
        save(room.crop(box), 'fx_' + fid)
        where = hook_for(box[0], box[1], box[2] - box[0], box[3] - box[1])
    else:
        raise SystemExit('unknown art kind for ' + fid)
    return where


def make_swatch(fid, how):
    if how is None:
        return None
    if how[0] == 'pattern':
        im = busiest_window(rgba(how[1]))
    elif how[0] == 'flat':
        im = cropped(rgba(how[1]))
    elif how[0] == 'cell':
        im = rgba(how[1]).crop((0, 0, how[2], how[2]))
    else:
        raise SystemExit('unknown swatch kind for ' + fid)
    name = 'fx_' + fid + '_swatch'
    save(im, name)
    return name


# ── data ────────────────────────────────────────────────────────────────────────────────

FIELD_ORDER = ['id', 'name', 'slot', 'price', 'comfort', 'stars', 'flavor', 'sprite', 'swatch',
               'water', 'cellW', 'cellH', 'screen', 'level', 'tapLevel', 'startsInTheRoom',
               'drain', 'drainsFree', 'washSeconds', 'workSpeed', 'group', 'x', 'y', 'hasX', 'hasY',
               'order', 'lightR', 'lightG', 'lightB', 'lightIntensity', 'lightRadius']


def ordered(entry):
    out = OrderedDict()
    for k in FIELD_ORDER:
        if k in entry:
            out[k] = entry[k]
    for k in entry:
        if k not in out:
            out[k] = entry[k]
    return out


def main():
    dec = json.load(open(os.path.join(HERE, 'decisions.json'), encoding='utf-8'))
    data = json.load(open(DATA, encoding='utf-8'), object_pairs_hook=OrderedDict)
    cand = json.load(open(os.path.join(HERE, 'candidates.json'), encoding='utf-8'), object_pairs_hook=OrderedDict)
    had = OrderedDict((f['id'], f) for f in data['fixtures'])

    # Every ladder the tree ordered, minus what was left out, in the game's slots.
    ladders = OrderedDict()
    # A slot folded into another comes after that slot's own rungs (the set, then the posters).
    for slot, ids in sorted(dec['order'].items(), key=lambda kv: kv[0] in SLOT_INTO):
        if slot in NOT_SHIPPED_SLOTS:
            continue
        keep = [i for i in ids if i not in dec['excluded'].get(slot, [])]
        ladders.setdefault(SLOT_INTO.get(slot, slot), []).extend(keep)

    opening = set(dec['opening'].values()) | {'taps_one', 'shaker_steel'}
    shipped, removed = OrderedDict(), []
    for slot, ids in ladders.items():
        for level, fid in enumerate(ids, 1):
            entry = OrderedDict(had[fid]) if fid in had else OrderedDict([('id', fid)])
            spec = SPEC.get(fid, {})
            for k in ('name', 'price', 'comfort', 'stars', 'flavor', 'workSpeed'):
                if k in spec:
                    entry[k] = spec[k]
            entry['slot'] = slot
            entry['group'] = GROUP_OF[slot]
            if fid not in had or 'sprite' not in entry:
                entry['sprite'] = 'fx_' + fid
            # A ladder's rung. The mats are single pieces and keep none; the tower keeps its
            # tapLevel, which IS its rung.
            if len(ids) > 1 or 'level' in entry or 'tapLevel' in entry:
                if 'tapLevel' in entry:
                    entry['tapLevel'] = level
                else:
                    entry['level'] = level
            if fid in opening:
                entry['startsInTheRoom'] = True
            else:
                entry.pop('startsInTheRoom', None)
            if 'light' in spec:
                r, g, b, inten, rad = spec['light']
                entry.update(lightR=r, lightG=g, lightB=b, lightIntensity=inten, lightRadius=rad)
            if fid in ART:
                where = make_art(fid)
                sw = make_swatch(fid, ART[fid][1])
                if sw:
                    entry['swatch'] = sw
                if where is not None:
                    entry['x'], entry['y'] = where
                    entry['hasX'] = entry['hasY'] = True
            if fid in EXTRA_SWATCH:
                entry['swatch'] = make_swatch(fid, EXTRA_SWATCH[fid])
            for k in ('name', 'price', 'flavor'):
                if k not in entry:
                    raise SystemExit('%s has no %s — write it into SPEC' % (fid, k))
            # A piece the game already had keeps its fields where they were in the file.
            shipped[fid] = entry if fid in had else ordered(entry)
    for fid, f in had.items():
        if fid not in shipped:
            if f['slot'] in ladders:
                removed.append(fid)
            else:
                shipped[fid] = f            # a slot the tree never ordered stays as it is

    # The slots: the ones the game had, the new ones, and every shelf's words.
    slots = OrderedDict((s['id'], s) for s in data['slots'])
    for s in NEW_SLOTS:
        slots[s['id']] = OrderedDict(slots.get(s['id'], {}), **s)
    for sid, s in slots.items():
        if sid in TITLES:
            s['title'], s['place'] = TITLES[sid]

    # The file stands in the shelves' order, each ladder climbing — and inside a shelf the big
    # surfaces first, because the market lists a shelf's ladders in the file's order.
    shelf = ['walls', 'wall_art', 'light', 'furniture', 'greenery', 'counter']
    slot_rank = {sid: i for i, sid in enumerate(SHELF_ORDER)}
    fixtures = sorted(shipped.values(), key=lambda f: (
        shelf.index(f['group']) if f.get('group') in shelf else 99,
        slot_rank.get(f['slot'], 99), f.get('level') or f.get('tapLevel') or 0))
    data['slots'] = list(slots.values())
    data['fixtures'] = fixtures
    with open(DATA, 'w', encoding='utf-8', newline='\n') as fh:
        json.dump(data, fh, ensure_ascii=False, indent=2)
        fh.write('\n')

    # The tree keeps showing what was left out: a piece that left the game goes back to being
    # a candidate, and everything that shipped stops being one.
    items = [c for c in cand['items'] if c['id'] not in shipped]
    known = {c['id'] for c in items}
    for fid in removed:
        f = had[fid]
        if fid in known:
            continue
        items.append(OrderedDict([('id', fid), ('name', f['name']), ('slot', f['slot']),
                                  ('sprite', 'Assets/Resources/Fixtures/' + f['sprite'] + '.png'),
                                  ('like', LIKE_AFTER.get(fid, fid)), ('round', 0)]))
    for c in items:
        if c.get('like') in LIKE_AFTER:
            c['like'] = LIKE_AFTER[c['like']]
    cand['items'] = items
    cand['slots'] = [s for s in cand['slots'] if s['id'] not in slots and s['id'] not in SLOT_INTO]
    with open(os.path.join(HERE, 'candidates.json'), 'w', encoding='utf-8', newline='\n') as fh:
        json.dump(cand, fh, ensure_ascii=False, indent=2)
        fh.write('\n')

    n_new = sum(1 for f in shipped if f not in had)
    print('%d fixtures (%d new), %d slots, removed: %s' % (len(fixtures), n_new, len(slots), ', '.join(removed)))
    for slot, ids in ladders.items():
        print('  %-15s %s' % (slot, ' > '.join(ids)))


if __name__ == '__main__':
    main()
