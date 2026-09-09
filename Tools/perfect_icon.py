# -*- coding: utf-8 -*-
"""The PERFECT mark, drawn in the star's own language (2026-09-09).

The author asked for an icon for a perfected recipe — "perfect tarif için bir icon oluştur
bu iconu perfect tarif için kullanalım" — and the game already has a written law for marks
like this: one star and one heart, drawn on a 32x32 canvas with a one-pixel INK keyline
inside the silhouette, a three-tone body lit from the upper left and a single sparkle where
that light lands (Tools/heart_icon.py). This is the third mark in that set.

It is NOT a star and NOT a medal: both are spoken for (the star is the rating, the medal is
comfort), and a second drawing of either would break the one-star rule. A perfected page is
platinum in the book, so this is a platinum ROSETTE — a scalloped seal with a ring inside
it and a pip at the centre, the shape a bar prints on the page it is proud of.

Nothing here is generated art — it is the procedural kit, like every other mark in the UI
(the house rule, memory art-direction-rules).

  py -3 -X utf8 Tools/perfect_icon.py          # writes perfect3d.png into Resources/Items
  py -3 -X utf8 Tools/perfect_icon.py preview  # …and a 6x sheet beside the star
"""
import math
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
S = 32
INK = (0x0D, 0x08, 0x13)
SPARK = (0xF2, 0xE8, 0xD5)
# The book's platinum, light to deep (TycoonHud.BkPlatinum / BkPlatinumInk and two steps
# under them), so the mark and the page it stamps are the same metal.
LIT = [(0xE8, 0xEC, 0xF5), (0xD3, 0xDA, 0xEA), (0xA8, 0xB2, 0xC8), (0x6B, 0x75, 0x8C)]


def disc():
    """The seal: a disc with twelve shallow scallops, sampled 2x2 and thresholded at half so
    every edge lands on a hard pixel boundary (no anti-aliasing, house rule)."""
    hit = [[False] * S for _ in range(S)]
    cx, cy = 15.5, 12.5
    for y in range(S):
        for x in range(S):
            n = 0
            for sy in (0.25, 0.75):
                for sx in (0.25, 0.75):
                    dx, dy = x + sx - 0.5 - cx, y + sy - 0.5 - cy
                    d = math.hypot(dx, dy)
                    a = math.atan2(dy, dx) if d > 0.0001 else 0.0
                    if d <= 10.0 + 1.1 * math.cos(12 * a):
                        n += 1
            hit[y][x] = n >= 2
    return hit, cx, cy


def tails(hit):
    """Two ribbon tails under the seal, cut with a notch — the silhouette that says AWARD at
    sixteen pixels, where the scallops on the disc have already gone."""
    for y in range(19, 31):
        t = (y - 19) / 11.0
        halfw = 3.1 - 0.7 * t
        for side in (-1, 1):
            cx = 15.5 + side * (1.7 + 3.4 * t)
            for x in range(S):
                if abs(x + 0.5 - cx) <= halfw:
                    hit[y][x] = True
    # the notch: the tails end in a V, so they read as cut ribbon rather than as legs
    for y in range(28, 31):
        for side in (-1, 1):
            cx = 15.5 + side * (1.7 + 3.4 * (y - 19) / 11.0)
            x = int(round(cx - 0.5))
            for dx in (0, 1):
                if y >= 31 - (2 - (30 - y)) and 0 <= x + dx < S:
                    hit[y][x + dx] = False
    return hit


def draw():
    hit, cx, cy = disc()
    hit = tails(hit)
    im = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    px = im.load()
    for y in range(S):
        for x in range(S):
            if not hit[y][x]:
                continue
            edge = any(not (0 <= x + dx < S and 0 <= y + dy < S) or not hit[y + dy][x + dx]
                       for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            if edge:
                px[x, y] = INK + (255,)
                continue
            dx, dy = x - cx, y - cy
            d = math.hypot(dx, dy)
            if y >= 22:                      # the tails, a step deeper than the seal
                px[x, y] = LIT[2 if x < 15.5 else 3] + (255,)
                continue
            if 4.6 <= d <= 5.6:              # the ring inside the seal
                px[x, y] = LIT[3] + (255,)
                continue
            t = (dx - dy) / 20.0 + 0.5       # light from the upper left
            px[x, y] = LIT[0 if t < 0.34 else (1 if t < 0.66 else 2)] + (255,)
    for x, y in ((11, 8), (12, 8), (11, 9)):     # the sparkle where the light lands
        px[x, y] = SPARK + (255,)
    for x, y in ((15, 12), (16, 12), (15, 13), (16, 13)):   # the pip at the centre
        px[x, y] = SPARK + (255,)
    return im


if __name__ == '__main__':
    im = draw()
    out = os.path.join(ITEMS, 'perfect3d.png')
    im.save(out)
    print('wrote', out)
    if len(sys.argv) > 1 and sys.argv[1] == 'preview':
        sheet = Image.new('RGBA', (S * 6 * 2 + 24, S * 6 + 16), (58, 32, 64, 255))
        sheet.alpha_composite(im.resize((S * 6, S * 6), Image.NEAREST), (8, 8))
        star = os.path.join(ITEMS, 'star3d.png')
        if os.path.exists(star):
            sheet.alpha_composite(Image.open(star).convert('RGBA').resize((S * 6, S * 6), Image.NEAREST),
                                  (16 + S * 6, 8))
        p = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'perfect_icon_preview.png')
        sheet.save(p)
        print('wrote', p)
