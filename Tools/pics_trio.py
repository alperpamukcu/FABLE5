# -*- coding: utf-8 -*-
"""The three small pictures as ONE picture (2026-09-12, the author: "bu üç görseli birleştirip
diğer orta duvar tabloları gibi 3lü tek tablo olsun").

The round-3 smalls were drawn as three separate framed pictures; the middle wall hangs ONE
piece, so they become one: three frames in a row, hung on a common centre line the way a wall
of pictures is hung, trimmed to what is drawn. The result is a candidate like any other —
Tools/room_variants3/picS_trio.png — and the upgrade tree stands it where the triptych hangs.

  py -3 -X utf8 Tools/pics_trio.py
"""
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, 'room_variants3')
PARTS = ('picS_flamingo', 'picS_cherry', 'picS_lips')
GAP = 10          # art px between frames — the wall's own breathing room
OUT = os.path.join(SRC, 'picS_trio.png')


def main():
    pics = []
    for n in PARTS:
        im = Image.open(os.path.join(SRC, n + '.png')).convert('RGBA')
        box = im.getbbox()
        pics.append(im.crop(box))
        print('%-14s %dx%d' % (n, box[2] - box[0], box[3] - box[1]))
    w = sum(p.width for p in pics) + GAP * (len(pics) - 1)
    h = max(p.height for p in pics)
    sheet = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    x = 0
    for p in pics:
        sheet.alpha_composite(p, (x, (h - p.height) // 2))   # hung on one centre line
        x += p.width + GAP
    sheet.save(OUT)
    print('trio %dx%d  ->  %s' % (w, h, OUT))


if __name__ == '__main__':
    main()
