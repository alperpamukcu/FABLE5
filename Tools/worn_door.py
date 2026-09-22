# -*- coding: utf-8 -*-
"""The counter's shutter as the bar opens: the same turquoise, stripped of its pictures, and worn through to the wood.

Run:  py -3 Tools/worn_door.py [--out DIR]

WHY (the author, 2026-09-22): "Tezgah kepenginden kastım yeni kepenk olan turkuaz kepenkin rengi yine turkuaz olacak,
üstündeki görseller kaldırılacak, turkuazlığı aşınmış altındaki ahşap gözükecek eskidiği için."

The shutter (Assets/Resources/Scene/counter_door.png) is the author's drawing: turquoise boards with a flamingo, a
sunset behind the pull and two palms. This makes the OPENING DAY's shutter out of it, and it only ever removes:

  1. The pictures go. The door is a vertical pattern - every column is one colour all the way down - so a picture is
     any pixel that is not its column's own colour. The pull and the rails stay: they are the door, not a drawing.
  2. The paint wears. Turquoise flakes off where a shutter is actually worn - the foot, the edges, around the pull,
     along the board seams - and under it is the wood it was painted onto, with its grain. What is left of the paint
     is a little flatter and a little dustier, with a hard chipped edge where it has broken away.

Deterministic: the same shutter every run.
"""
import argparse
import math
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..'))
SCENE = os.path.join(ROOT, 'Assets', 'Resources', 'Scene')

# the boards' own colours, off the drawing (measured): face, shade, seam, deep seam, highlight
PAINT = {(59, 200, 190), (38, 145, 143), (18, 59, 69), (27, 95, 102), (125, 240, 227)}
# what the paint was brushed onto: warm boards, dark to light
WOOD = [(0x3A, 0x26, 0x17), (0x5A, 0x3A, 0x20), (0x7A, 0x52, 0x2C), (0x99, 0x6B, 0x3D), (0xB2, 0x84, 0x51)]
CHIP = (0x16, 0x4C, 0x52)          # the broken edge of a flake: the paint seen end-on


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


def strip(im):
    """The pictures off the boards. The door is a VERTICAL pattern - every column is one colour from top to bottom -
    so a picture is exactly what differs from its own column, and painting it back is painting the column in. What
    stays: the rows the drawing crosses end to end (the top edge and the middle rail, which are the door), and the
    pull, which is the only thing on it made of metal."""
    w, h = im.size
    px = im.load()
    out = im.copy()
    dst = out.load()
    profile = [px[x, h - 12][:3] for x in range(w)]       # a clean row near the foot
    keep_rows = set()
    for y in range(h):
        same = sum(1 for x in range(w) if px[x, y][:3] == profile[x])
        if same < w * 0.35:
            keep_rows.add(y)
    # the pull: the door's only neutral metal, and the LARGEST such piece of it - the flamingo's eye and the sun's
    # rays carry near-neutral pixels too, and a box round all of them swallows half the drawing (measured: it did)
    neutral = [[px[x, y][3] > 0 and max(px[x, y][:3]) - min(px[x, y][:3]) < 26 and sum(px[x, y][:3]) > 300
                for x in range(w)] for y in range(h)]
    seen = [[False] * w for _ in range(h)]
    box = None
    best = 0
    for y0 in range(h):
        for x0 in range(w):
            if not neutral[y0][x0] or seen[y0][x0]:
                continue
            stack = [(x0, y0)]
            seen[y0][x0] = True
            cells = []
            while stack:
                cx, cy = stack.pop()
                cells.append((cx, cy))
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)):
                    nx, ny = cx + dx, cy + dy
                    if 0 <= nx < w and 0 <= ny < h and neutral[ny][nx] and not seen[ny][nx]:
                        seen[ny][nx] = True
                        stack.append((nx, ny))
            if len(cells) > best:
                best = len(cells)
                ys = [c[1] for c in cells]
                y0, y1 = min(ys), max(ys)
                # the pull runs in a band of rows: take every neutral pixel in those rows, so its far knob - which
                # the drawing joins to the bar through a darker pixel - comes with it
                band = [x for y in range(y0, y1 + 1) for x in range(w) if neutral[y][x]]
                box = (min(band) - 3, y0 - 3, max(band) + 3, y1 + 3)
    print('rows kept as the door:', sorted(keep_rows), '| the pull:', box)
    painted = 0
    for y in range(h):
        if y in keep_rows:
            continue
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0 or (r, g, b) == profile[x]:
                continue
            if box and box[0] <= x <= box[2] and box[1] <= y <= box[3]:
                # inside the pull's box: the handle itself and its own dark outline stay; a picture that happens to
                # pass behind it (the sunset's rays) does not
                if max(r, g, b) - min(r, g, b) < 26 or (r, g, b) == (36, 24, 48):
                    continue
            dst[x, y] = profile[x] + (a,)
            painted += 1
    print('picture pixels painted out:', painted)
    return out, profile, box


def wear(im, profile, box):
    """
    The paint coming off, where a shutter actually loses it: the FOOT, which is kicked and mopped and stands in
    whatever runs down the bar; the two ends, which the frame rubs; the board SEAMS, where a brush never reached and
    the edge lifts first; and the reach of the pull, where every hand for twenty years has taken hold. Everywhere
    else keeps its paint, a little dustier than it was. Under it is the wood it was painted onto, its grain running
    down the plank.
    """
    w, h = im.size
    out = im.copy()
    px = out.load()
    # the seams: the darkest columns of the drawing are where two boards meet
    seam = {x for x in range(w) if profile[x] == (18, 59, 69)}
    near_seam = {x + d for x in seam for d in (-1, 0, 1)}
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0 or (r, g, b) not in PAINT:
                continue                                      # the pull and the door's own rails are not painted wood
            if box and box[0] <= x <= box[2] and box[1] <= y <= box[3]:
                continue
            foot = max(0.0, (y - h * 0.68) / (h * 0.32))      # it wears from the ground up
            ends = max(0.0, 1.0 - min(x, w - 1 - x) / (w * 0.05)) * (0.30 + 0.70 * (y / h))
            # the hands: a band across the door at the pull's height, not a blob at its middle
            grip = math.exp(-((y - (h * 0.22)) / (h * 0.10)) ** 2) * (0.25 + 0.35 * noise(x, y, 37, 13))
            lift = 0.30 if x in seam else (0.16 if x in near_seam else 0.0)
            bare = 0.55 * foot ** 1.4 + 0.45 * ends + grip + lift
            bare *= 0.45 + 0.8 * noise(x, y, 19, 2)
            bare += 0.7 * max(0.0, noise(x, y, 6, 5) - 0.80)   # the odd flake anywhere
            if bare > 0.55:
                grain = 0.4 * noise(x * 2.6, y * 0.45, 9, 9) + 0.6 * noise(x, y, 23, 11)
                tone = WOOD[min(len(WOOD) - 1, int(grain * len(WOOD)))]
                if x in seam:
                    tone = mix(tone, WOOD[0], 0.5)
                px[x, y] = tone + (a,)
            elif bare > 0.47:
                px[x, y] = mix((r, g, b), CHIP, 0.7) + (a,)    # the flake's broken edge, the paint seen end-on
            else:
                worn = mix((r, g, b), (0xA8, 0xBE, 0xBA), 0.08 + 0.30 * max(0.0, bare - 0.20))
                px[x, y] = worn + (a,)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=SCENE)
    ap.add_argument('--plain', action='store_true', help='also write the stripped door with its paint intact')
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    src = Image.open(os.path.join(SCENE, 'counter_door.png')).convert('RGBA')
    stripped, profile, box = strip(src)
    if args.plain:
        stripped.save(os.path.join(args.out, 'counter_door_plain.png'))
    dst = os.path.join(args.out, 'counter_door_worn.png')
    wear(stripped, profile, box).save(dst)
    print('wrote', dst, src.size)


if __name__ == '__main__':
    main()
