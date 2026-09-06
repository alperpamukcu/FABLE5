# -*- coding: utf-8 -*-
"""The restock crate, redrawn (2026-09-06, the author: "restock görseli yenilensin").

A wooden crate seen a little from above with three bottle necks standing out of it and a
strap across its front — the delivery, not the department. Drawn in the upgrade icons' kit
(same ink, same ramps) at 24x20 and written at 2x (48x40), which is the sheet the old crate
was drawn on, so the tile's fitting arithmetic is unchanged.

    py -3 Tools/restock_icon.py          writes Assets/Resources/Items/sh_p_crate.png
"""
import os
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
W, H = 24, 20
INK = (0x24, 0x27, 0x2D)
WOOD = [(0x6B, 0x44, 0x16), (0x9E, 0x6A, 0x1D), (0xC9, 0x8F, 0x2B), (0xE6, 0xB9, 0x59)]   # Malt
STRAP = [(0x38, 0x3D, 0x45), (0x54, 0x5A, 0x64)]                                       # Graphite
GLASS = [(0x1B, 0x5F, 0x66), (0x26, 0x91, 0x8F), (0x7D, 0xF0, 0xE3)]                   # Cyan
CAP = (0xD9, 0x45, 0x5C)                                                               # ViceRed


def box(d, x0, y0, x1, y1, fill, ink=INK):
    d.rectangle([x0, y0, x1, y1], fill=fill, outline=ink)


def main():
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    # three bottle necks, behind the crate's back wall
    for x in (6, 11, 16):
        box(d, x, 1, x + 2, 8, GLASS[1])
        d.point((x + 1, 3), fill=GLASS[2])
        d.rectangle([x, 1, x + 2, 2], fill=CAP)
    # the crate: a lid rim, the front, its slats
    box(d, 1, 7, 22, 9, WOOD[2])                       # the top rim
    box(d, 1, 9, 22, 18, WOOD[1])                      # the front
    for y in (12, 15):
        d.line([(2, y), (21, y)], fill=WOOD[0])        # the slats' shadow lines
        d.line([(2, y - 1), (21, y - 1)], fill=WOOD[3])  # and their lit edges
    # the strap across it
    box(d, 10, 8, 13, 18, STRAP[0])
    d.line([(11, 9), (11, 17)], fill=STRAP[1])
    # a stencil mark, two dots — a label without a word
    d.point((4, 11), fill=WOOD[3]); d.point((5, 11), fill=WOOD[3])
    big = im.resize((W * 2, H * 2), Image.NEAREST)
    os.makedirs(OUT, exist_ok=True)
    big.save(os.path.join(OUT, 'sh_p_crate.png'))
    print('sh_p_crate.png', big.size)


if __name__ == '__main__':
    main()
