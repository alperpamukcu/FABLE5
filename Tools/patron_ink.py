# -*- coding: utf-8 -*-
"""NO BLACK KEYLINE (2026-09-07, the author: "leopar üstlü kadının görselinde siyah kontras
kullanılmış, kaldırılsın"). A shipped patron whose frames carry pure-black (or near-black)
pixels gets them re-inked with NATURAL contrast: each such pixel takes the darkened average of
the coloured pixels around it, the way heavyset's darks are dark navy and dark brown rather
than black. Idempotent - once no pixel is under the threshold, a second run changes nothing.

    py -3 -X utf8 Tools/patron_ink.py leopard [more slugs]
    py -3 -X utf8 Tools/patron_ink.py --check leopard      report only
"""
import glob
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
PATRON = os.path.join(os.path.dirname(HERE), 'Assets', 'Resources', 'Patron')
BLACK_MAX = 14          # a channel maximum under this is "black" for the house
DARKEN = 0.38           # the re-inked pixel is the neighbourhood's colour at this brightness
FLOOR = 16              # ...and never darker than this on any channel


def reink(im):
    px = im.load()
    w, h = im.size
    src = [[px[x, y] for y in range(h)] for x in range(w)]
    changed = 0
    for y in range(h):
        for x in range(w):
            r, g, b, a = src[x][y]
            if a == 0 or max(r, g, b) >= BLACK_MAX:
                continue
            # the coloured neighbourhood, widening until it finds something
            for radius in (1, 2, 3, 4):
                acc = [0, 0, 0]
                n = 0
                for dy in range(-radius, radius + 1):
                    for dx in range(-radius, radius + 1):
                        nx, ny = x + dx, y + dy
                        if nx < 0 or ny < 0 or nx >= w or ny >= h:
                            continue
                        cr, cg, cb, ca = src[nx][ny]
                        if ca == 0 or max(cr, cg, cb) < BLACK_MAX:
                            continue
                        acc[0] += cr; acc[1] += cg; acc[2] += cb
                        n += 1
                if n:
                    break
            if not n:
                continue
            nr, ng, nb = (max(FLOOR, int(acc[i] / n * DARKEN)) for i in range(3))
            px[x, y] = (nr, ng, nb, a)
            changed += 1
    return changed


def main():
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    check = '--check' in sys.argv
    for slug in args:
        folder = os.path.join(PATRON, slug)
        files = sorted(glob.glob(os.path.join(folder, '*', '*.png'))) + \
            sorted(glob.glob(os.path.join(folder, '*.png')))
        total = 0
        for f in files:
            im = Image.open(f).convert('RGBA')
            if check:
                px = im.load()
                total += sum(1 for y in range(im.height) for x in range(im.width)
                             if px[x, y][3] > 0 and max(px[x, y][:3]) < BLACK_MAX)
                continue
            n = reink(im)
            if n:
                im.save(f)
                total += n
        print('  %-12s %s %d black pixels in %d frames' % (slug, 'found' if check else 're-inked', total, len(files)))


if __name__ == '__main__':
    main()
