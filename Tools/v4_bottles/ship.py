# -*- coding: utf-8 -*-
"""picks.json -> Assets/Resources/Items. The ONLY step that touches the game.

  py -3 Tools/v4_bottles/ship.py               ship every card that has a pick
  py -3 Tools/v4_bottles/ship.py vodka_astra   one card

A pick is {"take": "s23", "emblem": 0}. The take's staging plates are copied as
v4_<id>_back / _mask / _front (96x192) and v4_<id>_back_c / _mask_c / _front_c (32x64);
sealed cards ship v4_<id> and v4_<id>_c. Old v3_*_flat / bot_* art for the card is left in
place until the runtime is measured against the new plates, then swept by name.
"""
import io
import json
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
STAGING = os.path.join(HERE, 'staging')
sys.path.insert(0, HERE)
import brief                                   # noqa: E402

GLASS_PLATES = ('back', 'mask', 'front', 'back_c', 'mask_c', 'front_c')
SEALED_PLATES = ('', '_c')


def ship(card_id, pick):
    take = pick['take']
    src = os.path.join(STAGING, card_id, take)
    if not os.path.isdir(src):
        print('  !! no staging for %s/%s' % (card_id, take)); return 0
    names = ['v4_%s_%s.png' % (card_id, p) for p in GLASS_PLATES] if brief.family(card_id) not in brief.SEALED \
        else ['v4_%s%s.png' % (card_id, p) for p in SEALED_PLATES]
    n = 0
    for name in names:
        s = os.path.join(src, name)
        if not os.path.exists(s):
            print('  !! missing plate', name); continue
        shutil.copyfile(s, os.path.join(ITEMS, name)); n += 1
    print('  %-18s %s -> %d plates' % (card_id, take, n))
    return n


if __name__ == '__main__':
    picks = json.load(io.open(os.path.join(HERE, 'picks.json'), encoding='utf-8'))
    want = sys.argv[1:] or list(picks)
    total = sum(ship(c, picks[c]) for c in want if c in picks)
    print('shipped %d plates' % total)
