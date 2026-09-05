# -*- coding: utf-8 -*-
"""THE LID, WITH ITS CAP OFF (2026-09-04, the author: "Su anki shaker gorseli
kullanilsin bardaga koyarken ucundaki kapak acik olmali").

A cobbler shaker does not pour through its cap — you lift the little cap off and the
drink comes out of the strainer under it. The serve bench was drawing the tin with the
whole lid seated, so a closed shaker was pouring into the glass.

Nothing new is generated. The art the game already uses stays exactly as it is, and the
open state is CUT FROM IT (the house rule, memory open-states-derive: an open shot drawn
in a second take comes back a different object — it has cost this project three rounds).
So `shaker_cap_pour.png` is `shaker_cap.png` with two edits and no third:

  * every row above the cap's SEAM is cleared — the seam is found on the silhouette, at
    the waist between the cap's straight cylinder and the dome's shoulder, not typed in;
  * the neck it leaves behind is opened: a lip round the rim, the bore behind it, and the
    far wall of the bore catching the light — drawn in colours SAMPLED FROM THIS SPRITE,
    so a lid that is dark stays dark and nothing off-palette is introduced.

The canvas is never resized and nothing is rescaled: the open lid has to line up with the
closed one pixel for pixel, or the shaker jumps between the two benches.

  py -3 -X utf8 Tools/shaker_cap_open.py            # writes Resources/Items
  py -3 -X utf8 Tools/shaker_cap_open.py preview    # ...and a 5x sheet beside the closed one
"""
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
SRC = os.path.join(ITEMS, 'shaker_cap.png')
DST = os.path.join(ITEMS, 'shaker_cap_pour.png')
PREVIEW = os.path.join(HERE, 'shaker_cap_open_preview.png')


def rows(im):
    px = im.load()
    out = []
    for y in range(im.height):
        xs = [x for x in range(im.width) if px[x, y][3] >= 128]
        out.append((min(xs), max(xs), len(xs)) if xs else None)
    return out


def seam(r):
    """Where the cap ends and the dome begins.

    The cap is a straight cylinder and the dome swells out from under it, so the
    silhouette narrows once on the way down and then only widens. That waist is the
    seam — and it is the LAST of the narrow rows, not the first: the cylinder's own
    rows all tie for narrowest, and cutting at the first would take the seam through
    the top of the cap."""
    ys = [y for y, v in enumerate(r) if v]
    top, bot = ys[0], ys[-1]
    h = bot - top + 1
    widest = max(r[y][2] for y in range(top, bot + 1))
    collar = min(y for y in range(top, bot + 1) if r[y][2] == widest)
    search = [y for y in range(top + max(3, int(h * 0.08)), collar - int(h * 0.10))]
    narrow = min(r[y][2] for y in search)
    return max(y for y in search if r[y][2] <= narrow + 1)


def tones(im):
    """Four steps off this sprite's own ramp, by luma: the bore, the shaded inside,
    the far wall the light reaches, and the lip. Sampling rather than choosing keeps
    the open lid in whatever palette the closed one was drawn in."""
    px = im.load()
    seen = {}
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a >= 128:
                seen[(r, g, b)] = seen.get((r, g, b), 0) + 1
    ramp = sorted(seen, key=lambda c: 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2])
    def at(f):
        return ramp[max(0, min(len(ramp) - 1, int(len(ramp) * f)))]
    return at(0.10), at(0.30), at(0.62), at(0.90)


def open_cap(im):
    im = im.convert('RGBA')
    r = rows(im)
    cut = seam(r)
    px = im.load()
    for y in range(0, cut):
        for x in range(im.width):
            px[x, y] = (0, 0, 0, 0)

    bore, inside, back, lip = tones(im)
    x0, x1, _ = r[cut]
    cx = (x0 + x1) / 2.0
    rx = (x1 - x0) / 2.0
    # The rim's depth follows the neck's width at the same slant every other ellipse on
    # this sprite is drawn at (the cap's own top reads 38 wide by 7 deep).
    ry = max(2.5, (x1 - x0) * 0.19)
    cy = cut + ry
    for y in range(int(cy - ry) - 1, int(cy + ry) + 3):
        if not (0 <= y < im.height):
            continue
        for x in range(x0 - 1, x1 + 2):
            if not (0 <= x < im.width) or px[x, y][3] < 128:
                continue
            dx = (x - cx) / rx
            dy = (y - cy) / ry
            d = dx * dx + dy * dy
            if d <= 0.52:
                px[x, y] = bore + (255,)          # straight down the strainer
            elif d <= 1.0:
                # the far wall of the bore catches the room; the near one does not
                px[x, y] = (back if y < cy else inside) + (255,)
            elif d <= 1.34:
                px[x, y] = (lip if y <= cy else inside) + (255,)   # the rolled rim
    return im


def preview():
    shut = Image.open(SRC).convert('RGBA')
    pour = Image.open(DST).convert('RGBA')
    box = shut.getbbox()
    box = (box[0] - 2, box[1] - 2, box[2] + 2, box[3] + 2)
    k = 5
    tiles = [i.crop(box) for i in (shut, pour)]
    w = tiles[0].width * k
    h = tiles[0].height * k
    sheet = Image.new('RGBA', (w * 2 + 24, h), (32, 26, 40, 255))
    for i, t in enumerate(tiles):
        sheet.alpha_composite(t.resize((w, h), Image.NEAREST), (i * (w + 24), 0))
    sheet.save(PREVIEW)
    print('wrote', PREVIEW)


if __name__ == '__main__':
    src = Image.open(SRC).convert('RGBA')
    out = open_cap(src)
    out.save(DST)
    print('wrote %s (%dx%d, seam at row %d)'
          % (DST, out.width, out.height, seam(rows(Image.open(SRC).convert('RGBA')))))
    if len(sys.argv) > 1 and sys.argv[1] == 'preview':
        preview()
