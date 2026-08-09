# -*- coding: utf-8 -*-
"""Measure the 9-slice border of the market app's frames.

A 9-slice whose border is guessed either eats the corner detail or stretches it.
This reads each frame and reports where its INNER field starts: for the tablet
that is the flat screen well, for the tab keys and cards the flat fill inside
the outline. Paste the numbers into PatronArtPostprocessor.

Run: python Tools/market_borders.py
"""
import os
from PIL import Image

ITEMS = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                     '..', 'Assets', 'Resources', 'Items')
FRAMES = ['mk_tablet', 'mk_tab_on', 'mk_tab_off', 'mk_card', 'mk_appbar']


def luma(c):
    return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]


def border_of(im):
    """How deep the DETAILED frame runs, per side.

    A 9-slice may only stretch a run that is uniform ALONG the edge it lies on.
    So walk inward and stop at the first line whose middle 80% is flat — every
    line before it carries a corner arc, an outline or a highlight and must sit
    inside the border.
    """
    W, H = im.size
    px = im.load()

    def flat(line):
        vals = [luma(c[:3]) for c in line if c[3] > 20]
        if len(vals) < max(4, len(line) // 3):
            return False                      # mostly transparent: still the corner
        return max(vals) - min(vals) <= 4

    def scan(line_at, depth, span):
        lo, hi = int(span * 0.1), int(span * 0.9)
        for i in range(depth):
            if flat([line_at(i, j) for j in range(lo, hi)]):
                return max(1, i)
        return max(1, depth // 3)

    left = scan(lambda i, j: px[i, j], W // 2, H)
    right = scan(lambda i, j: px[W - 1 - i, j], W // 2, H)
    bottom = scan(lambda i, j: px[j, H - 1 - i], H // 2, W)
    top = scan(lambda i, j: px[j, i], H // 2, W)
    return left, bottom, right, top   # Unity's Vector4 order


def main():
    for name in FRAMES:
        p = os.path.join(ITEMS, name + '.png')
        if not os.path.exists(p):
            print('%-12s (missing)' % name)
            continue
        im = Image.open(p).convert('RGBA')
        l, b, r, t = border_of(im)
        print('%-12s %3dx%-3d  border L%d B%d R%d T%d' % (name, im.width, im.height, l, b, r, t))


if __name__ == '__main__':
    main()
