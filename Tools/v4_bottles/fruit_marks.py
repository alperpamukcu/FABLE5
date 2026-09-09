# -*- coding: utf-8 -*-
"""THE FRUIT MARK FOR A SHELF COPY (2026-09-09, the author: "meyve sularının raf için olan
küçük görsellerini tekrar üret, küçük hangi meyvenin meyve suyu olduğu anlaşılmalı").

A 32x32 emblem taken down to thirteen pixels is a dark blob with a colour in it — the same
lesson the bottles learned twice (PLAN_bottle_art_v4 §9.18: at a size the art was not drawn
for, you REDRAW). So these five are drawn at the size they are used at, by hand, in the
house palette, in the same language as the star and the heart: a flat body, one shade, one
highlight, a dark keyline.

They are pressed onto the cellar copies by cellar_fruit.py.

  py -3 -X utf8 Tools/v4_bottles/fruit_marks.py            # writes marks/<id>.png
  py -3 -X utf8 Tools/v4_bottles/fruit_marks.py preview    # …and a 10x sheet
"""
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'marks')
S = 13
INK = (0x14, 0x0C, 0x1B, 255)
LEAF = (0x4E, 0x8C, 0x3A, 255)
LEAF_D = (0x33, 0x5F, 0x27, 255)


def blank():
    return Image.new('RGBA', (S, S), (0, 0, 0, 0))


def disc(px, cx, cy, r, body, shade, light):
    for y in range(S):
        for x in range(S):
            dx, dy = x - cx, y - cy
            d = (dx * dx + dy * dy) ** 0.5
            if d > r + 0.35:
                continue
            if d > r - 0.75:
                px[x, y] = INK
            elif dx - dy < -r * 0.55:
                px[x, y] = light
            elif dx - dy > r * 0.45:
                px[x, y] = shade
            else:
                px[x, y] = body


def leaf(px, x0, y0, flip=False):
    for i, (dx, dy) in enumerate(((0, 0), (1, 0), (2, 0), (1, -1), (2, -1), (3, -1))):
        x = x0 + (-dx if flip else dx)
        y = y0 + dy
        if 0 <= x < S and 0 <= y < S:
            px[x, y] = LEAF if i % 3 else LEAF_D


def orange():
    im = blank(); px = im.load()
    disc(px, 6, 7, 4.6, (0xE8, 0x8A, 0x2E, 255), (0xB4, 0x63, 0x1C, 255), (0xF5, 0xA9, 0x53, 255))
    leaf(px, 7, 2)
    return im


def lemon():
    im = blank(); px = im.load()
    # an ellipse with a nub at each end — the shape that is not a circle
    for y in range(S):
        for x in range(S):
            dx, dy = (x - 6) / 5.2, (y - 7) / 3.9
            d = (dx * dx + dy * dy) ** 0.5
            if d > 1.08:
                continue
            px[x, y] = INK if d > 0.82 else \
                ((0xF7, 0xE0, 0x66, 255) if dx - dy < -0.35
                 else (0xC9, 0xA8, 0x2A, 255) if dx - dy > 0.45 else (0xE9, 0xC9, 0x3E, 255))
    for x, y in ((0, 7), (12, 7)):
        px[x, y] = INK
    leaf(px, 7, 3)
    return im


def lime():
    im = blank(); px = im.load()
    disc(px, 6, 7, 4.6, (0x6F, 0xB4, 0x36, 255), (0x47, 0x7D, 0x24, 255), (0x96, 0xD1, 0x54, 255))
    # the cut face: a pale wedge across it, which is how a lime reads at this size
    for x in range(3, 10):
        px[x, 7] = (0xDD, 0xEF, 0xC0, 255)
    for y in range(4, 11):
        px[6, y] = (0xDD, 0xEF, 0xC0, 255)
    leaf(px, 7, 2)
    return im


def cranberry():
    im = blank(); px = im.load()
    disc(px, 4, 9, 3.1, (0xC8, 0x35, 0x4A, 255), (0x8E, 0x1F, 0x33, 255), (0xE8, 0x62, 0x73, 255))
    disc(px, 9, 9, 3.0, (0xB0, 0x2B, 0x40, 255), (0x7C, 0x19, 0x2C, 255), (0xD8, 0x53, 0x66, 255))
    disc(px, 6, 4, 3.0, (0xD8, 0x45, 0x59, 255), (0x9C, 0x25, 0x39, 255), (0xF2, 0x74, 0x84, 255))
    return im


def pineapple():
    im = blank(); px = im.load()
    for y in range(S):
        for x in range(S):
            dx, dy = (x - 6) / 3.6, (y - 8) / 4.4
            d = (dx * dx + dy * dy) ** 0.5
            if d > 1.08:
                continue
            px[x, y] = INK if d > 0.82 else \
                ((0xF2, 0xC0, 0x4A, 255) if dx - dy < -0.35
                 else (0xB8, 0x88, 0x22, 255) if dx - dy > 0.45 else (0xDD, 0xA6, 0x33, 255))
    # the crosshatch: two diagonals, which is what says pineapple and not lemon
    for i in range(-3, 4):
        for x, y in ((6 + i, 8 + i), (6 + i, 8 - i)):
            if 0 <= x < S and 0 <= y < S and px[x, y][3] and px[x, y] != INK:
                px[x, y] = (0x8E, 0x67, 0x18, 255)
    for i, x in enumerate((4, 6, 8)):
        for y in range(max(0, 3 - i % 2), 4):
            px[x, y] = LEAF if i != 1 else LEAF_D
        px[x, 3] = LEAF_D
    return im


MARKS = {'orange_grove': orange, 'lemon_fresh': lemon, 'lime_fresh': lime,
         'cranberry_north': cranberry, 'pineapple_isla': pineapple}

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    made = []
    for cid, fn in MARKS.items():
        im = fn()
        im.save(os.path.join(OUT, cid + '.png'))
        made.append(im)
        print('  wrote', cid + '.png')
    if len(sys.argv) > 1 and sys.argv[1] == 'preview':
        K = 12
        sheet = Image.new('RGBA', (len(made) * (S * K + 8) + 8, S * K + 16), (58, 32, 64, 255))
        x = 8
        for im in made:
            sheet.alpha_composite(im.resize((S * K, S * K), Image.NEAREST), (x, 8))
            x += S * K + 8
        p = os.path.join(HERE, 'fruit_marks_preview.png')
        sheet.save(p)
        print('  wrote', p)
