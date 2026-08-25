# -*- coding: utf-8 -*-
"""The room's plants, replaced (2026-08-25, the author: "Mevcut yeni uretilen bitkileri
upgrade kismina koy eskilerini kaldir").

WHAT CHANGES. The room used to carry two plants and neither was an upgrade: a fern in the
left slot and a monstera in the right, one rung each, bought once and never improved. The
five new plants make both slots into LADDERS, which is how every other fitting in this bar
already works (the sink has two rungs, the wall lamps three).

    plant_left    palm  ->  fiddle-leaf fig  ->  trailing pothos
    plant_right   snake plant  ->  agave

WHY THE SPLIT IS THREE AND TWO. The left slot stands at x 20 and the right at x 616 - the
left is the deep corner beside the window and the right is the end of the bar by the till.
A tall plant reads in the corner and gets in the way at the till, so the three UPRIGHT
plants take the left slot and the two low, wide ones take the right.

WHICH CANDIDATE. Four came back for each plant and one of each is shipped; the index is
recorded below so a different one is a one-line change and a re-run. Chosen for the same
two things every time: the vessel has to read at this size (a pot that dissolves into its
plant is a plant standing on nothing), and the silhouette has to differ from its neighbours
on the ladder - a ladder whose rungs look alike is not visibly an upgrade.

THE OLD ART IS NOT DELETED, only unlisted. fx_fern.png and fx_monstera.png stay on disk:
fx_monstera is the COLOUR REFERENCE every one of these five was generated against
(vice_room_gen.PALETTE_OVERRIDE), so deleting it would break the tool that made their
replacements, and a plant nobody sells costs nothing but the bytes.

    py -3 Tools/plants_ship.py            # copies the art and rewrites the catalogue
    py -3 Tools/plants_ship.py --check    # says what it WOULD do, writes nothing
"""
import io
import json
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
STAGE = os.path.join(HERE, 'AssetPipeline', 'staging', 'vice_room')
FIXTURES = os.path.join(ROOT, 'Assets', 'Resources', 'Fixtures')
DATA = os.path.join(ROOT, 'Assets', 'Data', 'fixtures', 'fixtures.json')

#   staged take          ships as              slot          price stars level
PLANTS = [
    ('fx_palm_pot_3',    'fx_plant_palm',      'plant_left',   20, 0.0, 1,
     'Areca Palm', 'Thin fronds and a clay pot. The cheapest way to stop a corner '
     'looking like a corridor.'),
    ('fx_plant_fiddle_3', 'fx_plant_fiddle',   'plant_left',   55, 1.5, 2,
     'Fiddle-Leaf Fig', 'Broad leaves on a bare stem, in a cream vase. It fills the '
     'window wall the way a lamp fills a table.'),
    ('fx_plant_pothos_0', 'fx_plant_pothos',   'plant_left',   95, 3.0, 3,
     'Trailing Pothos', 'It grows down instead of up, off three brass legs. The only '
     'plant in here that reaches for the floor.'),
    ('fx_plant_snake_3',  'fx_plant_snake',    'plant_right',  25, 0.0, 1,
     'Snake Plant', 'Stiff blades in a plain pot. Survives a bar, which is more than '
     'most things manage.'),
    ('fx_plant_agave_1',  'fx_plant_agave',    'plant_right',  70, 1.5, 2,
     'Agave Bowl', 'Blue-green and low and wide, in a shallow dish. It sits under the '
     'neon rather than in front of it.'),
]

# The two the room used to carry. Unlisted, not deleted - see the module header.
RETIRED = ('fern_pot', 'monstera_pot')


def main():
    check = '--check' in sys.argv
    doc = json.load(io.open(DATA, encoding='utf-8'))

    # -- the art ---------------------------------------------------------------
    for take, name, _slot, _p, _s, _l, _n, _f in PLANTS:
        src = os.path.join(STAGE, take + '.png')
        if not os.path.exists(src):
            raise SystemExit('missing take: %s' % src)
        dst = os.path.join(FIXTURES, name + '.png')
        from PIL import Image
        im = Image.open(src)
        print('%-22s -> %-22s %dx%d%s'
              % (take, name + '.png', im.width, im.height,
                 '   (already there, overwritten)' if os.path.exists(dst) else ''))
        if not check:
            shutil.copyfile(src, dst)

    # -- the catalogue ---------------------------------------------------------
    kept = [f for f in doc['fixtures'] if f['id'] not in RETIRED]
    dropped = [f['id'] for f in doc['fixtures'] if f['id'] in RETIRED]
    if len(dropped) != len(RETIRED):
        raise SystemExit('expected to retire %s, found %s' % (list(RETIRED), dropped))

    made = []
    for _take, sprite, slot, price, stars, level, name, flavor in PLANTS:
        made.append({
            'id': sprite.replace('fx_', ''),
            'name': name,
            'slot': slot,
            'price': price,
            'stars': stars,
            'flavor': flavor,
            'sprite': sprite,
            'level': level,
        })

    # The new plants go in where the old ones were, so the shop's order does not jump.
    at = next((i for i, f in enumerate(doc['fixtures']) if f['id'] == RETIRED[0]), 0)
    doc['fixtures'] = kept[:at] + made + kept[at:]

    print('\nretired : %s' % ', '.join(dropped))
    print('listed  : %s' % ', '.join('%s (%s lv%d $%d)' % (m['id'], m['slot'], m['level'],
                                                           m['price']) for m in made))
    if check:
        print('\n--check: nothing written')
        return
    io.open(DATA, 'w', encoding='utf-8', newline='\n').write(
        json.dumps(doc, indent=2, ensure_ascii=False) + '\n')
    print('\nwrote %s' % os.path.relpath(DATA, ROOT))


if __name__ == '__main__':
    main()
