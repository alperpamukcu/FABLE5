# -*- coding: utf-8 -*-
"""THE MARBLE SINK — rung 3 of the `sink` ladder (GDD 27 §3.1: marble $140 · +0.8 · 3.0★).

REFUSED, 2026-09-23. The author saw all four candidates in the room and turned them all down
("sinklerin hiçbirini beğenmedim eklenmesin"). Nothing from here is in the game and nothing from
here may be shipped: this script and `Docs/reports/sink_marble/` are the RECORD of an attempt.
Run it again only if the author asks for a new direction — the stone, the fittings and the grain
are separate inputs, so a different stone is a small change and not a new tool.

  py -3 -X utf8 Tools/sink_marble.py            # writes the candidates next to a report page
  py -3 -X utf8 Tools/sink_marble.py --out DIR  # ...somewhere else

DERIVED, NOT DRAWN AGAIN — twice over, which is the whole reason this rung can ship at all:

  * THE SILHOUETTE is the author's own sink. The three rungs that exist (`fx_sink_old`,
    `fx_sink`, `fx_sink_gold`) are one drawing in three materials, pixel for pixel — measured:
    the spout stands in rows 0-13, the handles join at 14-15, and the basin is rows 16-34,
    each row stepping one pixel left with the perspective. So a fourth rung is a fourth
    MATERIAL, and the outline, the drain and the fittings are lifted straight off the third.

  * THE MARBLE is the room's own marble. The game already carries two: `fx_floor_marble` (the
    dusk rose) and `fx_floor7_marble` (the bright pink of rung 7), both the author's. Inventing
    a white Italian marble for the sink would put a stone in the room that exists nowhere else
    in it, so the basin is re-cut in those two palettes and nothing else — five tones each,
    read off the floors.

The mapping keeps the drawing's own shading: the basin's tones are ranked by brightness and
laid on the marble ramp in that order, so where the author put a highlight there is a highlight.
The ink (the outline and the drain) is never touched, and the FITTINGS are whichever sink they
came from — the brass tap of rung 2 on marble, or the steel of rung 1 — which is the choice the
report asks the author to make.
"""
import argparse
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..'))
FIX = os.path.join(ROOT, 'Assets', 'Resources', 'Fixtures')

BASIN_TOP = 16          # the first row of the basin; everything above it is the tap (measured)
INK = 25                # anything darker than this is the drawing's line, and stays

# The room's own marble, read off the author's floors (both are in Fixtures), darkest first - and each
# floor's VEIN colours kept apart from its shading ramp, because that is how the author drew them: the
# rose floor's magenta (180,40,124) and the pink floor's near-white (254,207,227) are the grain, not a
# step of the stone. Putting the grain in the ramp turned the basin's rim into a neon lip (it did).
ROSE = {'ramp': [(62, 62, 68), (195, 124, 135), (210, 164, 171), (232, 166, 177)],
        'light': (232, 166, 177), 'grain': (180, 40, 124)}
PINK = {'ramp': [(62, 18, 49), (175, 90, 127), (245, 146, 174), (254, 142, 178), (254, 207, 227)],
        'light': (254, 207, 227), 'grain': (175, 90, 127)}


def lum(c):
    return 0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2]


def load(name):
    return Image.open(os.path.join(FIX, name + '.png')).convert('RGBA')


def basin_tones(img):
    """Every colour the basin is drawn in, with how much of it there is."""
    px, seen = img.load(), {}
    for y in range(BASIN_TOP, img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            if a == 0 or lum((r, g, b)) < INK:
                continue
            seen[(r, g, b)] = seen.get((r, g, b), 0) + 1
    return seen


def marble_map(tones, ramp):
    """Which marble tone each of the sink's own tones becomes: the drawing's brightness order,
    laid on the ramp. Tones with real area set the steps; the stray ones (antialiasing) follow
    whichever of those they are nearest in brightness, so nothing lands off the ramp."""
    main = sorted([c for c, n in tones.items() if n >= 15], key=lum)
    if not main:
        main = sorted(tones, key=lum)
    out = {}
    for i, c in enumerate(main):
        j = 0 if len(main) == 1 else round(i * (len(ramp) - 1) / (len(main) - 1))
        out[c] = ramp[j]
    for c in tones:
        if c not in out:
            near = min(main, key=lambda m: abs(lum(m) - lum(c)))
            out[c] = out[near]
    return out


def vein(img, stone_def, rows=((20, 9), (25, 34), (30, 18)), stone=None):
    """The stone's grain. It follows the DRAWING's perspective, not the screen: every row of the
    basin steps one pixel left, so a vein running down through the stone runs down-and-left at
    one to one. Three of them, broken, in the ramp's lightest tone with one darker hairline
    beside each — the way the author's floors carry theirs."""
    px = img.load()
    light, dark = stone_def['light'], stone_def['grain']
    for (y0, x0) in rows:
        x, y = float(x0), y0
        step = 0
        while y < img.height and 0 <= x < img.width:
            step += 1
            xi = int(round(x))
            if 0 <= xi < img.width:
                r, g, b, a = px[xi, y]
                if a and lum((r, g, b)) >= INK and (stone is None or (r, g, b) in stone):
                    if step % 7 != 0:                      # broken, not a drawn line
                        px[xi, y] = light + (255,)
                    if step % 3 == 0 and xi + 1 < img.width:
                        r2, g2, b2, a2 = px[xi + 1, y]
                        if a2 and lum((r2, g2, b2)) >= INK and (stone is None or (r2, g2, b2) in stone):
                            px[xi + 1, y] = dark + (255,)
            x -= 1.0                                        # the row's own shift
            y += 1


def cut(fittings, stone_def):
    """One candidate: a sink's fittings with its basin re-cut in marble."""
    out = fittings.copy()
    px = out.load()
    ramp = stone_def['ramp']
    table = marble_map(basin_tones(fittings), ramp)
    for y in range(BASIN_TOP, out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a == 0 or lum((r, g, b)) < INK:
                continue
            px[x, y] = table[(r, g, b)] + (255,)
    vein(out, stone_def, stone=set(ramp))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=os.path.join(ROOT, 'Docs', 'reports', 'sink_marble'))
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)

    gold, steel = load('fx_sink_gold'), load('fx_sink')
    made = [
        ('M1_pink_brass', cut(gold, PINK)),
        ('M2_pink_steel', cut(steel, PINK)),
        ('M3_rose_brass', cut(gold, ROSE)),
        ('M4_rose_steel', cut(steel, ROSE)),
    ]
    for name, im in made:
        im.save(os.path.join(args.out, 'sink_' + name + '.png'))
        print('wrote', name, im.size)
    return made


if __name__ == '__main__':
    main()
