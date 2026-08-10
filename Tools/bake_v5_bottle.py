# -*- coding: utf-8 -*-
"""Bake a v5 pilot bottle into the game's sprite slots.

The runtime draws ONE Image per bottle, so the two layers that must sit in front of
the drink — the thinned glass and the label — are composited into one sprite here.
The drink is drawn behind it by BottleFluid; the glass is see-through so the level
reads, and the label is fully opaque so it never is.

Only the glass THINNING is baked. The level, its surface and the chase stay at
runtime, because they depend on what the rules say is left in the bottle.

    python Tools/bake_v5_bottle.py

Sprites land on the names ItemArt already looks for (v3_{id}_flat / _flat_open), so
nothing in the UI has to learn a new path.
"""
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
DEST = os.path.join(ROOT, 'Assets', 'Resources', 'Items')

# the pilot's working directory — the scratchpad chain that produced the takes
PILOT = os.environ.get('V5_PILOT') or os.path.join(
    os.path.expanduser('~'), 'AppData', 'Local', 'Temp', 'claude',
    'c--My-project--2-', '2ee56b43-3292-45a5-b9f4-ae2667166af5', 'scratchpad')

BOTTLES = [
    # (ingredient id, capped take, capless take, label sprite)
    ('vodka_astra', 'v4_raw/bottle_1.png', 'v4_raw/bottle_1_open.png',
     'v4_layers/label_smirkoff.png'),
]


def main():
    sys.path.insert(0, PILOT)
    import v4_stack as V
    import v5_stack as V5

    for bid, capped, capless, label_path in BOTTLES:
        lab = Image.open(os.path.join(PILOT, label_path)).convert('RGBA')
        for suffix, take in (('_flat', capped), ('_flat_open', capless)):
            im = Image.open(os.path.join(PILOT, take)).convert('RGBA')
            im = im.crop(im.getbbox())
            rows, top, base, shoulder, widest = V.cavity(im)
            glass, thinned = V5.glass_layer_v2(im, top, base)
            floor = V5.drink_floor(glass, rows, base)
            label = V5.place(im.size, rows, shoulder, floor, lab)

            out = Image.new('RGBA', im.size, (0, 0, 0, 0))
            out.alpha_composite(glass)
            out.alpha_composite(label)

            px = out.load()
            W, H = out.size
            see = [px[x, y][3] for y in range(top, base + 1) for x in range(W)
                   if px[x, y][3] > 0]
            clear = sum(1 for a in see if a < 250) / float(len(see)) if see else 0
            name = 'v3_%s%s.png' % (bid, suffix)
            out.save(os.path.join(DEST, name))
            print('%-30s %dx%-3d  thinned %d  see-through %.0f%%'
                  % (name, W, H, thinned, clear * 100))


# The shelf-of-ten chain (2026-08-10) composes its own sprites end to end — chequer
# flattened, real wordmark replaced with ours, palette rotated off the brand's, cap
# grafted, glass thinned, label forced opaque — and writes the finished layers to the
# pilot's shelf10d/. There is nothing left here to bake: the install is a copy onto the
# names ItemArt already looks for, with the see-through measured on the way past so a
# bottle that would hide its own drink cannot ship quietly.
SHELF10 = os.path.join(PILOT, 'shelf10d')

APPROVED = [
    'vodka_vor',            # ABSOLVE      (ref Absolut)
    'vodka_leonid',         # GREY GANDER  (ref Grey Goose)
    'gin_boothby',          # GARDEN'S     (ref Gordon's)
    'gin_juniper_crown',    # LEAFEATER    (ref Beefeater)
]


def install(ids=None):
    """Put the approved shelf-of-ten bottles into the game's sprite slots."""
    for bid in (ids or APPROVED):
        for suffix, tag in (('_flat', 'capped'), ('_flat_open', 'capless')):
            src = os.path.join(SHELF10, '%s_%s.png' % (bid, tag))
            if not os.path.exists(src):
                print('%-30s MISSING %s' % (bid, src))
                continue
            im = Image.open(src).convert('RGBA')
            im = im.crop(im.getbbox())
            px = im.load()
            W, H = im.size
            see = [px[x, y][3] for y in range(H) for x in range(W) if px[x, y][3] > 0]
            clear = sum(1 for a in see if a < 250) / float(len(see)) if see else 0
            name = 'v3_%s%s.png' % (bid, suffix)
            im.save(os.path.join(DEST, name))
            print('%-30s %dx%-3d  see-through %.0f%%' % (name, W, H, clear * 100))


if __name__ == '__main__':
    if '--install' in sys.argv:
        install([a for a in sys.argv[1:] if not a.startswith('-')] or None)
    else:
        main()
