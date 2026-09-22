# -*- coding: utf-8 -*-
"""The olive's dish, derived from the five that already stand on the counter (2026-09-22).

Run:  py -3 Tools/olive_bowl.py [--out DIR]

WHY (the author's eighth list: "Ana sahnede gözüken buz, limon, tuz, şeker, nane kaplarına benzer şekilde zeytini de
aynı tarzda bir kaba koyalım"). Five garnishes stand in one glass bowl, 35x33, drawn by the author; the olives stand
in a tall jar with cocktail picks in it, 25x44 - a different object from a different set.

DERIVED, NOT DRAWN AGAIN (the house rule for every second version of a thing). The BOWL is the author's own bowl,
taken off the salt dish: its rim band and its glass wall are kept pixel for pixel, and only what shows THROUGH the
glass is re-tinted - the dishes are translucent, which is why the five are not identical and why an intersection of
them comes back empty (measured: the glass carries its contents' colour). The OLIVES are the jar's own olives: its
fruit palette, stacked over the rim the way the salt's mound and the mint's leaves stand.
"""
import argparse
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..'))
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')

RIM_TOP = 14          # the rim band's first row on the author's dish (measured)
BODY_TOP = 16         # the glass body below it


def load(name):
    return Image.open(os.path.join(ITEMS, name + '.png')).convert('RGBA')


def palette(jar):
    """The jar's fruit colours, darkest first: the olives are its low, cool pixels (the glass and brass are warm)."""
    seen = {}
    px = jar.load()
    for y in range(jar.size[1]):
        for x in range(jar.size[0]):
            r, g, b, a = px[x, y]
            if a < 200:
                continue
            if r - b > 45 or (r > 180 and g > 140):      # amber glass, brass lid, the picks
                continue
            seen[(r, g, b)] = seen.get((r, g, b), 0) + 1
    ranked = sorted(seen.items(), key=lambda kv: -kv[1])[:6]
    tones = sorted((c for c, _ in ranked), key=lambda c: 0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2])
    return tones


def olive_tones(jar):
    """The fruit's three tones: the jar's olives are neutral (a black olive reads grey-brown against amber glass), so
    the body and the light are its two greyest fruit colours and the ring is the body at 45% - the ring the counter's
    item art always wears, never a colour picked by eye."""
    tones = [c for c in palette(jar) if max(c) - min(c) < 40 and sum(c) > 180]
    body = tones[0] if tones else (110, 100, 89)
    lit = tones[-1] if len(tones) > 1 else tuple(min(255, int(v * 1.35)) for v in body)
    dark = tuple(int(v * 0.42) for v in body)
    return dark, body, lit


def draw_olive(px, cx, cy, dark, body, lit, pimento):
    """One olive: a 7x6 fruit with the ring the counter art wears, a highlight, and its red heart where it shows."""
    for dy in range(-3, 4):
        for dx in range(-4, 5):
            u, v = dx / 4.0, dy / 3.0
            d = u * u + v * v
            if d > 1.0:
                continue
            x, y = cx + dx, cy + dy
            if not (0 <= x < 35 and 0 <= y < 33):
                continue
            edge = d > 0.62
            px[x, y] = (dark if edge else body) + (255,)
    px[cx - 2, cy - 1] = lit + (255,)
    px[cx - 1, cy - 2] = lit + (255,)
    if pimento:
        px[cx + 2, cy] = (166, 48, 40, 255)
        px[cx + 3, cy] = (146, 38, 32, 255)


def interior(dish):
    """Which pixels are INSIDE the glass rather than its wall: each row's span, pulled in by the wall's three
    pixels, which is where the author drew the bowl's own edge."""
    px = dish.load()
    w, h = dish.size
    inside = set()
    for y in range(BODY_TOP, h):
        cols = [x for x in range(w) if px[x, y][3] > 40]
        if len(cols) < 10:
            continue
        for x in range(cols[0] + 3, cols[-1] - 2):
            inside.add((x, y))
    return inside


def through_glass(px, inside, cx, cy, dark, body, lit):
    """An olive lying in the bowl, SEEN THROUGH it: the fruit blended into whatever the glass already shows, the
    way the mint's leaves and the ice's cubes read below their rims."""
    for dy in range(-3, 4):
        for dx in range(-4, 5):
            u, v = dx / 4.0, dy / 3.0
            d = u * u + v * v
            if d > 1.0:
                continue
            x, y = cx + dx, cy + dy
            if (x, y) not in inside:
                continue
            r, g, b, a = px[x, y]
            tone = dark if d > 0.62 else body
            k = 0.62                                   # the glass keeps a little of its own light over the fruit
            px[x, y] = (int(tone[0] * k + r * (1 - k)), int(tone[1] * k + g * (1 - k)),
                        int(tone[2] * k + b * (1 - k)), a)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=ITEMS)
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)

    salt = load('counter_salt')
    jar = load('counter_olive')
    dark, body, lit = olive_tones(jar)
    print('olive tones', dark, body, lit)

    # the glass: the author's dish with everything above the rim cleared
    dish = salt.copy()
    px = dish.load()
    for y in range(0, RIM_TOP):
        for x in range(35):
            px[x, y] = (0, 0, 0, 0)

    # what lies in the bowl, seen through it: two rows of fruit, dimmed by the glass
    inside = interior(dish)
    for cx in (9, 17, 25):
        through_glass(px, inside, cx, 20, dark, body, lit)
    for cx in (6, 13, 21, 28):
        through_glass(px, inside, cx, 25, dark, body, lit)

    # the mound: three at the back over the rim, four in front of them, the way the mint's leaves stack
    for cx in (10, 17, 24):
        draw_olive(px, cx, 8, dark, body, lit, pimento=cx == 17)
    for cx in (7, 14, 21, 28):
        draw_olive(px, cx, 12, dark, body, lit, pimento=cx in (14, 28))
    # the rim band goes back on top of the front row, so the olives sit IN the bowl
    rim = salt.crop((0, RIM_TOP, 35, BODY_TOP))
    dish.alpha_composite(rim, (0, RIM_TOP))

    dst = os.path.join(args.out, 'counter_olive_bowl.png')
    dish.save(dst)
    print('wrote', dst)


if __name__ == '__main__':
    main()
