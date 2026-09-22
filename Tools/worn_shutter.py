# -*- coding: utf-8 -*-
"""The counter's shutter, aged: the one the bar opens with before anybody has spent a dollar on the place.

Run:  py -3 Tools/worn_shutter.py [--out DIR]

WHY (the author's eighth list: "Tezgah kepenginin düz başlangıç günü için gri eskimiş bir tarzını da üretelim").
The shutter is a clean sheet of grey slats; the room it belongs to opens broke. This ages the author's own drawing
rather than drawing a second one: the slats, their shadow lines and their proportions are untouched, and what is
added is what time adds to a steel roller - dirt where it meets the floor, rust blooming out of the rail and the
fixing points, streaks running down from them, scuffs where hands and stools have hit it, and a wash of grime in
the corners. Deterministic: the same picture every run, no seed to remember.
"""
import argparse
import math
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..'))
ART = os.path.join(ROOT, 'Assets', 'Art', 'Backgrounds')

RUST = [(0x5A, 0x33, 0x1C), (0x7A, 0x44, 0x22), (0x99, 0x58, 0x2C), (0xB0, 0x6E, 0x3A)]
DIRT = (0x3A, 0x36, 0x34)
CHIP = (0xC6, 0xC2, 0xC4)


def mix(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def hash01(x, y, salt=0):
    n = (x * 374761393 + y * 668265263 + salt * 1442695040888963407) & 0xFFFFFFFF
    n = (n ^ (n >> 13)) * 1274126177 & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFFFFFF) / 0xFFFFFF


def noise(x, y, cell, salt=0):
    gx, gy = x / cell, y / cell
    x0, y0 = math.floor(gx), math.floor(gy)
    fx, fy = gx - x0, gy - y0
    fx = fx * fx * (3 - 2 * fx)
    fy = fy * fy * (3 - 2 * fy)
    a = hash01(x0, y0, salt); b = hash01(x0 + 1, y0, salt)
    c = hash01(x0, y0 + 1, salt); d = hash01(x0 + 1, y0 + 1, salt)
    return (a + (b - a) * fx) * (1 - fy) + (c + (d - c) * fx) * fy


def age(im):
    """Grime, then rust, then scuffs - in that order and all of it thin. A shutter that has stood in weather for
    twenty years is still grey: what says its age is the dirt in its bottom third, a few rust bleeds under the
    fixings, and the marks of everything that has been dragged past it."""
    out = im.copy()
    px = out.load()
    w, h = out.size
    # the fixings rust first, and the wet corner at the foot: small, and they bleed DOWN the slats
    seeds = [(int(w * 0.09), 4, 7.0), (int(w * 0.35), 3, 5.5), (int(w * 0.66), 3, 6.0),
             (int(w * 0.93), 4, 7.0), (int(w * 0.21), h - 6, 7.5), (int(w * 0.78), h - 5, 6.5),
             (int(w * 0.50), h - 4, 6.0)]
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            base = (r, g, b)
            lum = 0.3 * r + 0.59 * g + 0.11 * b
            dark_line = lum < 125                     # the slat's own shadow: it takes dirt, never a highlight

            # grime: the foot, the corners, and a thin wash over everything
            foot = max(0.0, (y - h * 0.72) / (h * 0.28))
            corner = max(0.0, 1.0 - min(x, w - 1 - x) / (w * 0.08))
            grime = 0.07 + 0.30 * foot ** 1.7 + 0.16 * corner * (0.4 + 0.6 * noise(x, y, 29, 3))
            grime *= 0.5 + 0.7 * noise(x, y, 19, 1)
            col = mix(base, DIRT, min(0.42, grime))

            # rust: a small bloom at each fixing, and a streak under it that dies out down the slats
            rust = 0.0
            for (sx, sy, rad) in seeds:
                dx = (x - sx) / rad
                dy = (y - sy) / rad
                blob = math.exp(-(dx * dx + dy * dy)) * (0.6 + 0.4 * noise(x, y, 7, 2))
                run = 0.0
                if 0 <= y - sy < h * 0.35 and abs(x - sx) < rad * 1.3:
                    fade = 1.0 - (y - sy) / (h * 0.35)
                    run = 0.60 * fade * fade * max(0.0, noise(x * 2.2, y * 0.4, 6, 5) - 0.38)
                rust = max(rust, blob, run)
            if rust > 0.06:
                tone = RUST[min(3, int(rust * 3.4))]
                col = mix(col, tone, min(0.70, rust * (0.40 if dark_line else 0.85)))

            # scuffs: short bright marks along the slat faces, where stools and crates have hit it
            if not dark_line and noise(x * 0.35, y * 6.0, 15, 11) > 0.93 and hash01(x, y, 19) > 0.4:
                col = mix(col, CHIP, 0.35)
            # and the paint has gone flat: the whole sheet a step down in contrast, the way sun leaves steel
            col = mix(col, (0x8C, 0x88, 0x88), 0.10)
            px[x, y] = col + (a,)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=ART)
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    src = Image.open(os.path.join(ART, 'counter_shutter.png')).convert('RGBA')
    dst = os.path.join(args.out, 'counter_shutter_worn.png')
    age(src).save(dst)
    print('wrote', dst, src.size)


if __name__ == '__main__':
    main()
