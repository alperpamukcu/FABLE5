# -*- coding: utf-8 -*-
"""SHIP ROUND EIGHT'S PICKS (2026-09-29, the author: "L1 · zemin 0 / L1 · tavan 0 / L2 · tavan 0 / lamba 1 - Sadece
bunlar okay. Devam etme layout üretmeye.")

Four pieces from Tools/room_variants8 (the picks page: Docs/reports/room_layouts8) go into the game:

  L1 tavan 0   ceil8_dive      dark planks with the water stains      ceiling  the ladder's new FIRST rung
  L1 zemin 0   floor8_dive     worn cream-and-brown checker           floor    the ladder's new FIRST rung
  L2 tavan 0   ceil8_shack     pale driftwood slats on dark rafters   ceiling  between the Deco and the Palm ceilings
  lamba 1      the Deco lantern (copper rings, frosted fluted glass) on the GLOBE's rung: the author asked for it
               "görseldeki ışık yerine" - in place of the globe pendant - so the row keeps its id, price and stars (a
               saved bar that owns the globe owns the lantern) and only its drawing, name and words change.

The surfaces are round eight's builds as the page showed them: the tile's own frame cut (round seven's lesson), then
warped exactly as rounds six and seven were. The dive pieces are what a bar looks like the night it opens, so they go
under the rungs the room already had; each ladder is numbered again in file order, its prices, comfort and buff still
rising with it.

  py -3 -X utf8 Tools/room8_ship.py
"""
import io
import json
import os
import sys
from collections import OrderedDict

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SRC = os.path.join(HERE, 'room_variants8')
FIXT = os.path.join(ROOT, 'Assets', 'Resources', 'Fixtures')
DATA = os.path.join(ROOT, 'Assets', 'Data', 'fixtures', 'fixtures.json')

sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, 'upgrade_tree'))
import room7_ship as r7                            # noqa: E402  trim_frame
import ship as tree                                # noqa: E402  busiest_window

# id, slot, name, flavor, stars, price, comfort, buffPct, group, before (the id it goes in front of), built surface, flat tile
RUNGS = [
    ('ceil8_dive', 'ceiling', 'Plank Ceiling',
     'Dark boards overhead with the old water stains still on them. It keeps the rain out, most nights.',
     0, 20, 0.1, 1, 'walls', 'ceil_beams', 'fx_L1_ceil_0.png', 'L1_ceil_0.png'),
    ('ceil8_shack', 'ceiling', 'Driftwood Slats',
     'Pale slats laid over dark rafters, like the roof of a shack on the sand. The salt air comes free.',
     0.5, 52, 0.26, 5, 'walls', 'ceil_palm', 'fx_L2_ceil_0.png', 'L2_ceil_0.png'),
    ('floor8_dive', 'floor', 'Worn Checker',
     'Cream and brown squares, chipped at every corner. Somebody danced on this floor a long time ago.',
     0, 20, 0.1, 3, 'furniture', 'floor_terra', 'fx_L1_floor_0.png', 'L1_floor_0.png'),
]
LAMP_ID = 'counter_lamps_globe'
LAMP_SPRITE = 'fx_counter_lamps_deco'
LAMP_NAME = 'Deco Lantern Pendants'
LAMP_FLAVOR = ('Copper-ringed lanterns of frosted fluted glass, stepped like a hotel on Ocean Drive. '
               'The light comes down soft and even, and nobody at the bar looks tired under it.')


def ship():
    for fid, slot, name, flavor, stars, price, comfort, pct, group, before, built, flat in RUNGS:
        Image.open(os.path.join(SRC, built)).convert('RGBA').save(os.path.join(FIXT, 'fx_' + fid + '.png'))
        tex, _cut = r7.trim_frame(Image.open(os.path.join(SRC, flat)).convert('RGBA'))
        tree.busiest_window(tex).save(os.path.join(FIXT, 'fx_' + fid + '_swatch.png'))
        print('  art', fid)
    Image.open(os.path.join(SRC, 'lamp_deco_1.png')).convert('RGBA').save(os.path.join(FIXT, LAMP_SPRITE + '.png'))
    print('  art', LAMP_SPRITE)

    # THE FILE IS EDITED AS TEXT: a dump of the whole file would rewrite every "0.40" as "0.4" in rows this round
    # never touched. Each change is one exact, unique piece of text, counted before it is made.
    text = io.open(DATA, encoding='utf-8').read()
    d = json.loads(text, object_pairs_hook=OrderedDict)
    fx = d['fixtures']

    def span(t, fid):
        """The text of one fixture's object: from its id to the next id (or the end of the list)."""
        key = '"id": "%s"' % fid
        assert t.count(key) == 1, fid
        a = t.index(key)
        b = t.find('"id": "', a + len(key))
        return a, (b if b >= 0 else len(t))

    def swap(t, fid, old, new):
        a, b = span(t, fid)
        assert t[a:b].count(old) == 1, (fid, old)
        return t[:a] + t[a:b].replace(old, new) + t[b:]

    for fid, slot, name, flavor, stars, price, comfort, pct, group, before, built, flat in RUNGS:
        if any(f['id'] == fid for f in fx):
            continue
        buff = next(f['buff'] for f in fx if f['slot'] == slot and f.get('buff'))
        entry = OrderedDict([('id', fid), ('name', name), ('slot', slot), ('price', price),
                             ('comfort', comfort), ('stars', stars), ('flavor', flavor),
                             ('sprite', 'fx_' + fid), ('swatch', 'fx_' + fid + '_swatch'),
                             ('level', 0), ('group', group), ('buff', buff), ('buffPct', pct)])
        block = '\n'.join('    ' + line for line in json.dumps(entry, indent=2, ensure_ascii=False).split('\n'))
        a, _ = span(text, before)
        brace = text.rindex('    {', 0, a)
        text = text[:brace] + block + ',\n' + text[brace:]
        fx.insert([f['id'] for f in fx].index(before), entry)
    lamp = next(f for f in fx if f['id'] == LAMP_ID)
    for field, value in (('name', LAMP_NAME), ('flavor', LAMP_FLAVOR), ('sprite', LAMP_SPRITE)):
        if lamp[field] != value:
            text = swap(text, LAMP_ID, '"%s": %s' % (field, json.dumps(lamp[field], ensure_ascii=False)),
                        '"%s": %s' % (field, json.dumps(value, ensure_ascii=False)))
            lamp[field] = value
    # every slot's ladder numbered again in the order the file gives it - and it must still climb
    for slot in {r[1] for r in RUNGS}:
        rungs = [f for f in fx if f['slot'] == slot]
        for n, f in enumerate(rungs, 1):
            if f['level'] != n:
                text = swap(text, f['id'], '"level": %d' % f['level'], '"level": %d' % n)
                f['level'] = n
        for a, b in zip(rungs, rungs[1:]):
            assert a['price'] < b['price'] and a['comfort'] < b['comfort'] and a['stars'] <= b['stars'] \
                and a['buffPct'] < b['buffPct'], (a['id'], b['id'])
    assert json.loads(text) == json.loads(json.dumps(d)), 'the text and the data parted'
    io.open(DATA, 'w', encoding='utf-8', newline='\n').write(text)
    print('  data', len(fx), 'fixtures')


if __name__ == '__main__':
    ship()
