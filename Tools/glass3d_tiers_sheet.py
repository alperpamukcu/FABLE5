# -*- coding: utf-8 -*-
"""A contact sheet of the glass ladder as SHIPPED (2026-09-07): one row a glass, tier 1 (the
base) through tier 6 (the legendary), read from Resources/Items at 3x over the room's dark.

    py -3 -X utf8 Tools/glass3d_tiers_sheet.py      -> Tools/glass3d_tiers_preview.png
"""
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ITEMS = os.path.join(os.path.dirname(HERE), 'Assets', 'Resources', 'Items')
GLASSES = ['rocks', 'pint', 'highball', 'martini', 'coupe']
K = 3


def main():
    rows = []
    for g in GLASSES:
        row = []
        for t in range(1, 7):
            p = os.path.join(ITEMS, 'glass3d_%s%s.png' % (g, '' if t == 1 else '_t%d' % t))
            row.append(Image.open(p).convert('RGBA') if os.path.exists(p) else None)
        rows.append(row)
    cw = max(im.width for row in rows for im in row if im) * K + 16
    ch = max(im.height for row in rows for im in row if im) * K + 16
    out = Image.new('RGBA', (6 * cw + 16, len(rows) * ch + 16), (36, 24, 48, 255))
    for r, row in enumerate(rows):
        for c, im in enumerate(row):
            if im is None:
                continue
            big = im.resize((im.width * K, im.height * K), Image.NEAREST)
            x = 16 + c * cw + (cw - 16 - big.width) // 2
            y = 16 + r * ch + (ch - 16 - big.height)
            out.alpha_composite(big, (x, y))
    p = os.path.join(HERE, 'glass3d_tiers_preview.png')
    out.save(p)
    print('sheet', p, out.size)


if __name__ == '__main__':
    main()
