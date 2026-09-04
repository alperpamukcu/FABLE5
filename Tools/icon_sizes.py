# -*- coding: utf-8 -*-
"""The house icons at the size they are actually drawn (2026-09-04).

Every star in this game is drawn between 8 and 16 units except the night's standing gauge,
which is 40. A 32x32 shaded icon put on a 14-unit square is a 0.44 scale under a POINT
filter, which is the mud the bottle work spent a week learning about: at a size the art was
not drawn for, you REDRAW rather than shrink (PLAN_bottle_art_v4 §9.18, cellar_box).

So each icon ships at two sizes. The 16 is derived from the 32 the same way the cellar
copies are derived from their masters — peel the keyline so it cannot darken the body,
average by AREA, snap every colour back to the icon's own palette, then put a fresh
one-pixel keyline round it — and ItemArt picks whichever is nearer what it is about to be
drawn at.

  py -3 -X utf8 Tools/icon_sizes.py           # writes the *_16 pairs
  py -3 -X utf8 Tools/icon_sizes.py preview   # …and a contact sheet at 1x and 6x
"""
import os
import sys
from collections import Counter

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
INK = (0x0D, 0x08, 0x13)
ICONS = ['star3d', 'star3d_socket', 'heart3d', 'heart3d_socket']


def palette_of(im):
    """The icon's own colours, keyline excluded — what the small one may be painted with."""
    c = Counter(p[:3] for p in im.convert('RGBA').getdata() if p[3] > 128 and p[:3] != INK)
    return [col for col, _ in c.most_common()]


def peel(im):
    """Drop the keyline: an ink ring averaged into the body turns a small icon muddy."""
    out = im.copy()
    px = out.load()
    w, h = out.size
    for y in range(h):
        for x in range(w):
            if px[x, y][3] > 128 and px[x, y][:3] == INK:
                px[x, y] = (0, 0, 0, 0)
    return out


def box_down(im, f, cut=0.5):
    """Area average of the opaque pixels in each f*f cell; opaque at `cut` coverage."""
    w, h = im.width // f, im.height // f
    src = im.load()
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    dst = out.load()
    for y in range(h):
        for x in range(w):
            r = g = b = n = 0
            for dy in range(f):
                for dx in range(f):
                    q = src[x * f + dx, y * f + dy]
                    if q[3] >= 128:
                        r += q[0]; g += q[1]; b += q[2]; n += 1
            if n / float(f * f) >= cut:
                dst[x, y] = (r // n, g // n, b // n, 255)
    return out


def snap(im, palette):
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            if not px[x, y][3]:
                continue
            c = px[x, y][:3]
            best = min(palette, key=lambda p: sum((p[i] - c[i]) ** 2 for i in range(3)))
            px[x, y] = best + (255,)
    return im


def ring(im):
    """One pixel of keyline outside the silhouette — never two, and never inside it."""
    px = im.load()
    w, h = im.size
    add = []
    for y in range(h):
        for x in range(w):
            if px[x, y][3]:
                continue
            if ((x > 0 and px[x - 1, y][3]) or (x < w - 1 and px[x + 1, y][3])
                    or (y > 0 and px[x, y - 1][3]) or (y < h - 1 and px[x, y + 1][3])):
                add.append((x, y))
    for x, y in add:
        px[x, y] = INK + (255,)
    return im


def shrink(name):
    src = Image.open(os.path.join(ITEMS, name + '.png')).convert('RGBA')
    pal = palette_of(src)
    small = box_down(peel(src), 2, 0.5)
    # one cell of margin all round, so the fresh keyline has somewhere to go
    body = Image.new('RGBA', (16, 16), (0, 0, 0, 0))
    body.paste(small, (0, 0), small)
    out = ring(snap(body, pal))
    out.save(os.path.join(ITEMS, name + '_16.png'))
    return out


def main():
    made = [(n, shrink(n)) for n in ICONS]
    print('wrote', ', '.join(n + '_16.png' for n, _ in made))
    if len(sys.argv) > 1 and sys.argv[1] == 'preview':
        k = 6
        sheet = Image.new('RGBA', (len(made) * (16 * k + 8) + 8, 16 * k + 16 + 24),
                          (36, 24, 48, 255))
        for i, (_, im) in enumerate(made):
            sheet.paste(im.resize((16 * k, 16 * k), Image.NEAREST), (8 + i * (16 * k + 8), 8),
                        im.resize((16 * k, 16 * k), Image.NEAREST))
            sheet.paste(im, (8 + i * (16 * k + 8), 16 * k + 14), im)
        out = os.path.join(os.environ.get('TEMP', '.'), 'icon16_preview.png')
        sheet.save(out)
        print('preview', out)


if __name__ == '__main__':
    main()
