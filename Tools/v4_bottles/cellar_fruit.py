# -*- coding: utf-8 -*-
"""THE FRUIT ON THE SHELF COPY (2026-09-09, the author: "meyve sularının raf için olan küçük
görsellerini tekrar üret, küçük hangi meyvenin meyve suyu olduğu anlaşılmalı ve büyüğü ile
benzer olmalı").

A cellar copy is the master area-averaged to 32x64 (cellar_box), and a printed brand name
does not survive that: five cartons on the shelf were five coloured boxes with a smudge on
them. The colour alone cannot say lemon from pineapple either — both are yellow.

So the copy keeps its shape and its colour and gets ONE readable mark: the card's own fruit,
the same emblem the master's label carries, drawn big enough to read at 32 px. Nothing is
regenerated — the emblem is one of the 32x32 candidates already picked for that card, taken
down to the copy's scale the way every other derivation in this pipeline works (area
average, palette snap, one keyline).

  py -3 -X utf8 Tools/v4_bottles/cellar_fruit.py            # every card in FRUIT
  py -3 -X utf8 Tools/v4_bottles/cellar_fruit.py orange_grove
"""
import io
import json
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
RAW = os.path.join(HERE, 'raw')
sys.path.insert(0, HERE)
import palette                                   # noqa: E402
import process                                   # noqa: E402

# card -> (emblem file, how wide the mark is drawn on the 32 px copy, its centre)
# The centres are measured off the shipped copies: the front face's middle, on the band the
# printed label already occupies, so the fruit lands where a label lands.
FRUIT = {
    'orange_grove':    ('marks', 13, (13, 28)),
    'lemon_fresh':     ('marks', 13, (15, 30)),
    'lime_fresh':      ('marks', 13, (15, 29)),
    'cranberry_north': ('marks', 13, (15, 28)),
    'pineapple_isla':  ('marks', 13, (18, 28)),
}


def mark(card_id, name, size):
    """The mark, at the size it was DRAWN at (Tools/v4_bottles/fruit_marks.py). Nothing is
    resampled: a 32 px emblem taken down to thirteen came back a dark blob with a colour in
    it — measured, first cut — which is the lesson the bottles paid for twice."""
    src = os.path.join(HERE, 'marks', card_id + '.png')
    if not os.path.exists(src):
        return None
    return Image.open(src).convert('RGBA')


def label_box(im):
    """Where the printed label sits on a cellar copy: the biggest patch of pale, low-chroma
    pixels in the middle third. Returns None for a card whose name is printed straight onto
    the board — then the caller's own centre is used."""
    px = im.load()
    w, h = im.size
    y0, y1 = int(h * 0.28), int(h * 0.68)
    seen = set()
    best = None
    for y in range(y0, y1):
        for x in range(w):
            if (x, y) in seen:
                continue
            c = px[x, y]
            if c[3] < 128 or sum(c[:3]) / 3 < 168 or max(c[:3]) - min(c[:3]) > 46:
                continue
            blob, stack = [], [(x, y)]
            seen.add((x, y))
            while stack:
                cx, cy = stack.pop()
                blob.append((cx, cy))
                for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1)):
                    if 0 <= nx < w and y0 <= ny < y1 and (nx, ny) not in seen:
                        d = px[nx, ny]
                        if d[3] >= 128 and sum(d[:3]) / 3 >= 168 and max(d[:3]) - min(d[:3]) <= 46:
                            seen.add((nx, ny))
                            stack.append((nx, ny))
            if best is None or len(blob) > len(best):
                best = blob
    if not best or len(best) < 18:
        return None
    xs = [p[0] for p in best]
    ys = [p[1] for p in best]
    return min(xs), min(ys), max(xs), max(ys)


def press(card_id):
    name, size, centre = FRUIT[card_id]
    dst = os.path.join(ITEMS, 'v4_%s_c.png' % card_id)
    if not os.path.exists(dst):
        print('  !! no cellar copy for', card_id)
        return 0
    im = Image.open(dst).convert('RGBA')
    box = label_box(im)
    if box is not None:
        # ON the label the card already carries, filling it: that patch is where the
        # eye already goes.
        centre = ((box[0] + box[2]) // 2, (box[1] + box[3]) // 2)
    stamp = mark(card_id, name, size)
    if stamp is None:
        print('  !! no emblem candidate for', card_id)
        return 0
    x = centre[0] - stamp.width // 2
    y = centre[1] - stamp.height // 2
    im.alpha_composite(stamp, (max(0, x), max(0, y)))
    im.save(dst)
    print('  %-18s %dx%d at %s%s' % (card_id, stamp.width, stamp.height, centre,
                                     '' if box is None else ' (on its label)'))
    return 1


if __name__ == '__main__':
    want = sys.argv[1:] or list(FRUIT)
    n = sum(press(c) for c in want if c in FRUIT)
    print('pressed %d fruit' % n)
