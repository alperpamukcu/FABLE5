# -*- coding: utf-8 -*-
"""The olive's dish, derived from the five that already stand on the counter (2026-09-22).

Run:  py -3 Tools/olive_bowl.py [--out DIR]

SHIPPED 2026-09-23 (the author: "kap girsin"). The dish now IS `Items/counter_olive.png` - the name every
caller already asks for (`ItemArt.Load("counter_" + style + "")`), so nothing in code changed and the file
keeps its GUID. The jar it was made from would have been overwritten by its own output, so the source moved
to `Tools/olive_bowl_src/counter_olive_jar.png`: that copy is what the fruit ramp is read off from now.

WHY (the author's eighth list: "Ana sahnede gözüken buz, limon, tuz, şeker, nane kaplarına benzer şekilde zeytini de
aynı tarzda bir kaba koyalım"). Five garnishes stand in one glass bowl, 35x33, drawn by the author; the olives stand
in a tall jar with cocktail picks in it, 25x44 - a different object from a different set.

DERIVED, NOT DRAWN AGAIN (the house rule for every second version of a thing). The BOWL is the author's own bowl,
taken off the salt dish: its rim band and its glass wall are kept pixel for pixel. The OLIVES are the jar's own
olives, in the jar's own four-tone ramp.

TWO THINGS MEASURED OFF THE AUTHOR'S DISHES, AFTER A FIRST TRY GOT BOTH WRONG (2026-09-23):
  * the palette is TIGHT - eleven to seventeen colours a dish, of which the contents use two or three. Nothing is
    mixed by arithmetic: the mint is (108,186,52) over (49,129,14), the ice (141,192,247) over (62,128,209). So the
    olives use the jar's four and nothing else, snapped, never a lifted or multiplied tone.
  * what lies BELOW the rim is not the glass blended with the fruit - it is the fruit drawn flat, filling the bowl,
    with a sliver of the glass's own cream left at the wall. A dish whose inside stays cream reads as a salt dish
    with pebbles in it (it did).
"""
import argparse
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..'))
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
JAR = os.path.join(HERE, 'olive_bowl_src', 'counter_olive_jar.png')     # the author's jar, kept as the source

# The bowl's own grid, measured across all five dishes (2026-09-23): the rim band is rows 16-18 - its top face, its
# grey middle and its lower edge - and whatever a dish holds starts at row 19 below it and stops at row 27, where the
# glass's own lit foot begins. Everything above 16 is the heap standing proud of the rim.
BAND_TOP, BAND_END = 16, 19
BODY_TOP, BODY_END = 19, 28

# The jar's own fruit ramp, read off counter_olive.png once and asserted against it on every run: the ring, the
# shaded underside, the body and the lit crescent. The last two are the pit's reds, off the same jar.
RAMP = [(26, 16, 35), (54, 36, 71), (110, 100, 89), (156, 143, 128)]
PIT = [(166, 43, 68), (110, 27, 50)]


def load(name):
    return Image.open(os.path.join(ITEMS, name + '.png')).convert('RGBA')


def check_palette(jar):
    """Every tone an olive is drawn in must come off the author's jar - if one does not, the jar was redrawn and this
    script must be re-read against it rather than quietly inventing a colour."""
    px = jar.load()
    have = set()
    for y in range(jar.size[1]):
        for x in range(jar.size[0]):
            r, g, b, a = px[x, y]
            if a > 200:
                have.add((r, g, b))
    missing = [c for c in RAMP + PIT if c not in have]
    if missing:
        raise SystemExit('counter_olive.png no longer carries %s - re-read its fruit ramp' % (missing,))


def shade(u, v, d, deep=False):
    """Which of the four tones a point on a ball takes. The counter's dishes are drawn from a little above and lit
    from the upper left - the ice's cubes carry their highlight on the top-left face, the sugar's heap the same - so
    an olive is a ball with a ring where its silhouette turns away, a shaded underside and a lit crescent up-left.
    `deep` drops the whole ball one step: that is how the fruit under the glass reads, without leaving the palette.
    """
    s = (-u * 0.55) + (-v * 0.72)          # y grows downward, so up is -v
    # WEIGHTED TOWARDS THE BODY: the jar's own fruit is mostly (110,100,89) with the dark used as a separator between
    # one olive and the next, not as half the ball - shaded too far, a bowl of them reads as blackberries.
    if d > 0.87:
        i = 0
    elif s > 0.55:
        i = 3
    elif s > -0.26:
        i = 2
    else:
        i = 1
    if deep:
        i = max(0, i - 1)
    return RAMP[i]


def draw_olive(px, cx, cy, rx, ry, w=35, h=33, pit=False, deep=False, inside=None):
    """One olive, round, in the dishes' own perspective."""
    cx0, cy0 = int(round(cx)), int(round(cy))
    for dy in range(int(-ry) - 2, int(ry) + 3):
        for dx in range(int(-rx) - 2, int(rx) + 3):
            u, v = dx / rx, dy / ry
            d = u * u + v * v
            if d > 1.0:
                continue
            x, y = cx0 + dx, cy0 + dy
            if not (0 <= x < w and 0 <= y < h):
                continue
            if inside is not None and (x, y) not in inside:
                continue
            px[x, y] = shade(u, v, d, deep) + (255,)
    if pit:
        # the stone's hole, a little up and right of centre, the way a pitted olive faces the light
        # two pixels, not four: at this size a wider hole reads as a berry rather than a stoned olive
        px0, py0 = int(round(cx + rx * 0.22)), int(round(cy - ry * 0.12))
        for dx, dy, tone in ((0, 0, PIT[0]), (0, 1, PIT[1])):
            x, y = px0 + dx, py0 + dy
            if 0 <= x < w and 0 <= y < h and (inside is None or (x, y) in inside):
                px[x, y] = (tone if not deep else PIT[1]) + (255,)


def interior(dish):
    """Which pixels are INSIDE the glass rather than its wall: each row's span, pulled in by the wall's pixels, which
    is where the author drew the bowl's own edge. Two columns of glass are left showing at each wall, the way the
    ice's cream shows past its cubes."""
    px = dish.load()
    w, h = dish.size
    inside = set()
    for y in range(BODY_TOP, min(BODY_END, h)):
        cols = [x for x in range(w) if px[x, y][3] > 40]
        if len(cols) < 10:
            continue
        for x in range(cols[0] + 3, cols[-1] - 2):
            inside.add((x, y))
    return inside


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=ITEMS)
    ap.add_argument('--name', default='counter_olive')
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)

    salt = load('counter_salt')
    jar = Image.open(JAR).convert('RGBA')
    check_palette(jar)

    # the glass: the author's dish with everything above the rim cleared
    dish = salt.copy()
    px = dish.load()
    for y in range(0, BAND_TOP):
        for x in range(35):
            px[x, y] = (0, 0, 0, 0)

    # WHAT FILLS THE BOWL: the fruit packed wall to wall. The bowl is FLOODED with the body tone first and the olives
    # are then drawn into it - anything left of the glass's cream between two olives reads as a gap, and a dish with
    # gaps in it reads half empty (the mint's greens leave none).
    inside = interior(dish)
    for (x, y) in inside:
        px[x, y] = RAMP[2] + (255,)
    for (cx, cy, rx, ry, pit) in [(7.5, 21.4, 4.3, 3.0, False), (16.6, 20.8, 4.3, 3.0, True), (25.7, 21.4, 4.3, 3.0, False),
                                  (11.5, 25.2, 4.3, 3.0, False), (21.5, 25.2, 4.3, 3.0, True),
                                  (6.0, 25.6, 4.0, 2.8, False), (28.0, 25.6, 4.0, 2.8, False),
                                  (16.5, 28.4, 4.3, 3.0, False)]:
        draw_olive(px, cx, cy, rx, ry, pit=pit, inside=inside)
    # the bowl's own shadow: the fruit sitting deepest in the glass goes down a step, so the dish has a floor
    deeper = {RAMP[3]: RAMP[2], RAMP[2]: RAMP[1], RAMP[1]: RAMP[0]}
    for (x, y) in inside:
        if y >= BODY_END - 2:
            r, g, b, a = px[x, y]
            px[x, y] = deeper.get((r, g, b), (r, g, b)) + (a,)

    # THE HEAP, IN THE DISHES' OWN PERSPECTIVE: the bowls are drawn from a little above, so a mound is read back to
    # front - the fruit at the back sits higher, smaller and further in; the fruit at the front is bigger, lower and
    # overlaps what is behind it; and the whole heap narrows to a peak, the way the salt's mound does.
    for (cx, cy, rx, ry, pit) in [
        (17.2, 4.2, 3.3, 2.7, False),                       # the peak, furthest in and smallest
        (10.4, 7.0, 3.7, 3.0, False), (24.0, 7.0, 3.7, 3.0, True),
        (6.4, 10.6, 4.2, 3.3, False), (17.2, 9.8, 4.2, 3.3, True), (28.0, 10.6, 4.2, 3.3, False),
        (11.4, 13.4, 4.5, 3.5, False), (22.9, 13.4, 4.5, 3.5, False),
    ]:
        draw_olive(px, cx, cy, rx, ry, pit=pit)
    # the rim band goes back on top of the front row, so the olives sit IN the bowl
    dish.alpha_composite(salt.crop((0, BAND_TOP, 35, BAND_END)), (0, BAND_TOP))
    # ...AND TWO HANG OVER IT (2026-09-23, seen in the room): with every olive tucked behind the band, the band was
    # the brightest thing in the dish and the heap looked laid on top of a lid. The mint's leaves and the ice's cubes
    # both break their band at the ENDS and leave its middle showing - so two olives are drawn last, at the ends,
    # over the band.
    for (cx, cy, rx, ry, pit) in [(6.8, 16.8, 3.7, 3.0, False), (28.2, 16.8, 3.7, 3.0, True)]:
        draw_olive(px, cx, cy, rx, ry, pit=pit)

    dst = os.path.join(args.out, args.name + '.png')
    dish.save(dst)
    print('wrote', dst)


if __name__ == '__main__':
    main()
