# -*- coding: utf-8 -*-
"""THE MONEY MARK, AGAIN (2026-09-25, the author: "YENİ $ İCONU OLUŞTUR VE ONU KULLANALIM OYUNDA, yeşil para da
olur dolar işareti de olur farklı alternatifler de üret").

Six candidates for the one mark every price, tip and till in the game will wear, drawn in the house language the
star, the heart, the medallion and the old coin share (Tools/coin_icon.py): a 32x32 canvas, a one-pixel INK
keyline inside the silhouette, a body in three tones lit from the upper left, the five-pixel sparkle, and money's
own green (UITheme.Lime) so it is the only green thing in the chrome. Every candidate is drawn from fractions of
the canvas and REDRAWN at 24 and 16 rather than shrunk (icon_sizes.py's law: at a size the art was not drawn for,
you redraw).

  A  BILL      a green banknote, a medallion with the $ in its middle
  B  STACK     three bills fanned, the top one with the medallion
  C  GOLD      a gold coin with the $ struck through it - money that is not green, for contrast
  D  GLYPH     the $ itself, heavy, green, keylined and lit, no plate behind it
  E  BAG       a money sack tied at the neck, the $ on its belly
  F  CASH      a bill with a gold coin standing in front of it

Nothing here is generated art; it is the procedural kit (memory art-direction-rules). Nothing is written into
Assets: the author picks first, then the pick ships through `ship <letter>`.

  py -3 -X utf8 Tools/money_icon.py            # writes Tools/money_icon_out/ + contact sheets
  py -3 -X utf8 Tools/money_icon.py ship       # the pick: the stack as money*, the glyph as price*, the bill
"""
import os
import shutil
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Tools', 'money_icon_out')
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')

INK = (0x0D, 0x08, 0x13)
SPARK = (0xF2, 0xE8, 0xD5)
CLEAR = (0, 0, 0, 0)
# UITheme.Lime, lightest first, and a darker band for rims and borders.
LIME = [(0xA8, 0xF0, 0x77), (0x6F, 0xCC, 0x4B), (0x47, 0x99, 0x38), (0x2A, 0x59, 0x26)]
DEEP = [(0x47, 0x99, 0x38), (0x2A, 0x59, 0x26), (0x16, 0x33, 0x1B), (0x10, 0x24, 0x14)]
# UITheme.Amber, lightest first: the gold.
GOLD = [(0xF5, 0xC9, 0x7B), (0xE8, 0xA3, 0x3D), (0xC9, 0x82, 0x2B), (0x8F, 0x5A, 0x1E)]
# The sack's cloth: Cream's warm steps.
SACK = [(0xE8, 0xD2, 0xA8), (0xCF, 0xB2, 0x80), (0xA8, 0x86, 0x58), (0x6E, 0x55, 0x36)]


class Canvas:
    def __init__(self, s):
        self.s = s
        self.px = [[None] * s for _ in range(s)]      # None = clear; else (rgb, layer)

    def put(self, x, y, c):
        if 0 <= x < self.s and 0 <= y < self.s:
            self.px[y][x] = c

    def get(self, x, y):
        if 0 <= x < self.s and 0 <= y < self.s:
            return self.px[y][x]
        return None

    def image(self):
        im = Image.new('RGBA', (self.s, self.s), CLEAR)
        for y in range(self.s):
            for x in range(self.s):
                c = self.px[y][x]
                if c is not None:
                    im.putpixel((x, y), c + (255,))
        return im


def shape(cv, inside, ramp, key=True, light=(-1, -1)):
    """Fills the pixels where inside(x, y) holds: three tones lit from `light` (a direction), the outer ring in
    INK when `key`. Returns the set of pixels it covered."""
    s = cv.s
    cells = {(x, y) for y in range(s) for x in range(s) if inside(x + 0.5, y + 0.5)}
    if not cells:
        return cells
    xs = [x for x, _ in cells]; ys = [y for _, y in cells]
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    for (x, y) in cells:
        edge = any((x + dx, y + dy) not in cells for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
        if key and edge:
            cv.put(x, y, INK)
            continue
        # position along the light: 0 = lit corner, 1 = far corner
        u = ((x - x0) / max(1, x1 - x0) * -light[0] + (y - y0) / max(1, y1 - y0) * -light[1]) / 2.0
        u = (u + 1) / 2 if light[0] > 0 or light[1] > 0 else u
        tone = ramp[0] if u < 0.30 else ramp[1] if u < 0.66 else ramp[2]
        cv.put(x, y, tone)
    return cells


def spark(cv, cx, cy, big=True):
    for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)) if big else ((0, 0),):
        cv.put(cx + dx, cy + dy, SPARK)


# ── the $ as a bitmap, drawn at the sizes the marks need ─────────────────────────────────────────────────────────

DOLLAR = {
    # 5 wide x 9 tall: the bar runs through, the S is three strokes
    'S': ["..#..",
          ".####",
          "#.#..",
          "#.#..",
          ".###.",
          "..#.#",
          "..#.#",
          "####.",
          "..#.."],
    # 7 wide x 13 tall
    'M': ["...#...",
          ".#####.",
          "##.#.##",
          "##.#...",
          "##.#...",
          ".####..",
          "..####.",
          "...#.##",
          "...#.##",
          "##.#.##",
          ".#####.",
          "...#...",
          "...#..."],
    # 3 wide x 7 tall, for the 16s: the bar stands proud above and below, or a 3x5 S reads as the number 5
    'T': [".#.",
          "###",
          "#..",
          "###",
          "..#",
          "###",
          ".#."],
}


def glyph(cv, name, cx, cy, colour):
    rows = DOLLAR[name]
    h, w = len(rows), len(rows[0])
    x0, y0 = cx - w // 2, cy - h // 2
    for j, row in enumerate(rows):
        for i, ch in enumerate(row):
            if ch == '#':
                cv.put(x0 + i, y0 + j, colour)


def glyph_for(s):
    return 'M' if s >= 32 else 'S' if s >= 20 else 'T'


# ── the six ──────────────────────────────────────────────────────────────────────────────────────────────────────

def bill(cv, x0, y0, x1, y1, ramp=LIME, medallion=True):
    s = cv.s
    shape(cv, lambda x, y: x0 <= x <= x1 and y0 <= y <= y1, ramp)
    # the printed border, one step in from the keyline
    for x in range(int(x0) + 1, int(x1)):
        for y in (int(y0) + 1, int(y1) - 1):
            if cv.get(x, y) not in (None, INK):
                cv.put(x, y, DEEP[1])
    for y in range(int(y0) + 1, int(y1)):
        for x in (int(x0) + 1, int(x1) - 1):
            if cv.get(x, y) not in (None, INK):
                cv.put(x, y, DEEP[1])
    if medallion:
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        rx, ry = (x1 - x0) * 0.22, (y1 - y0) * 0.36
        for y in range(s):
            for x in range(s):
                dx, dy = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
                if dx * dx + dy * dy <= 1.0:
                    cv.put(x, y, ramp[0])
        # the glyph only where the bill is tall enough to hold it inside its border; a smaller bill keeps the
        # medallion alone, which still reads as money at 16
        if (y1 - y0) >= 11:
            name = 'S' if (y1 - y0) >= 15 else 'T'
            glyph(cv, name, int(cx), int(cy), INK)


def draw_A(s):
    cv = Canvas(s)
    m = s / 32.0
    bill(cv, 1 * m, 7 * m, 30 * m, 24 * m)
    spark(cv, int(5 * m), int(10 * m), big=s >= 24)
    return cv


# THE STACK IS ONE BILL, THREE TIMES (2026-09-25, the author: "Deste güzel fakat ... 3ünü birlikte üretme 1 tane
# üret onu deste haline getir"). The first stack drew three different bills - the back two a darker ramp and no
# medallion - so the stack and the bill that flies out of it (the money flight, TycoonHud.MoneyFlight) were two
# drawings. Now ONE bill is drawn at each size and the stack is that bill laid down three times, each copy up and
# to the right of the one in front; the two behind take a shade (a multiply, not a second drawing) so the pile
# reads as depth. The single bill ships on its own as money_bill.png: it is what flies.

# The bill's size and the step between copies, per canvas.
BILL = {32: (26, 16, 2, 5), 24: (20, 12, 2, 4), 16: (13, 8, 1, 3)}
BACK_SHADE = (0.80, 0.90)          # the back copy, the middle copy


def bill_unit(s):
    """The one bill a stack of size s is made of, on its own canvas."""
    w, h, _, _ = BILL[s]
    cv = Canvas(max(w, h) + 1)
    cv.s = max(w, h) + 1
    bill(cv, 0, 0, w, h)
    spark(cv, 3 if s >= 24 else 2, 3 if s >= 24 else 2, big=s >= 24)
    im = cv.image().crop((0, 0, w, h))
    return im


def shade(im, k):
    px = im.load()
    out = im.copy()
    q = out.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a and (r, g, b) != INK:
                q[x, y] = (int(r * k), int(g * k), int(b * k), a)
    return out


def draw_B(s):
    w, h, dx, dy = BILL[s]
    unit = bill_unit(s)
    out = Image.new('RGBA', (s, s), CLEAR)
    # front copy at the bottom-left, the other two stepped up and right
    x0 = (s - (w + 2 * dx)) // 2
    y0 = s - h - (s - (h + 2 * dy)) // 2
    for i, k in ((2, BACK_SHADE[0]), (1, BACK_SHADE[1]), (0, 1.0)):
        copy = unit if k == 1.0 else shade(unit, k)
        out.alpha_composite(copy, (x0 + i * dx, y0 - i * dy))
    return out


def coin(cv, cx, cy, r, ramp, glyph_colour=INK):
    shape(cv, lambda x, y: (x - cx) ** 2 + (y - cy) ** 2 <= r * r, ramp)
    # the milled rim: a ring one step in, a step darker
    rr = r - max(1.0, r * 0.18)
    for y in range(cv.s):
        for x in range(cv.s):
            d = ((x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2) ** 0.5
            if rr <= d < r - 1 and cv.get(x, y) not in (None, INK):
                cv.put(x, y, ramp[2] if (x + y) % 2 else ramp[3])
    glyph(cv, 'M' if r * 2 >= 26 else 'S' if r * 2 >= 16 else 'T', int(cx), int(cy), glyph_colour)


def draw_C(s):
    cv = Canvas(s)
    m = s / 32.0
    coin(cv, 16 * m, 16 * m, 14.5 * m, GOLD)
    spark(cv, int(9 * m), int(8 * m), big=s >= 24)
    return cv


def draw_D(s):
    """The $ as the object: the medium glyph scaled up by whole pixels, keylined, lit, sparked."""
    cv = Canvas(s)
    rows = DOLLAR['M'] if s >= 24 else DOLLAR['S']
    k = max(1, (s - 4) // len(rows))
    h, w = len(rows) * k, len(rows[0]) * k
    x0, y0 = (s - w) // 2, (s - h) // 2
    solid = set()
    for j, row in enumerate(rows):
        for i, ch in enumerate(row):
            if ch == '#':
                for dy in range(k):
                    for dx in range(k):
                        solid.add((x0 + i * k + dx, y0 + j * k + dy))
    # a one-pixel keyline OUTSIDE the glyph (it is too thin to take one inside), the body lit top-left
    ring = {(x + dx, y + dy) for (x, y) in solid for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))} - solid
    for (x, y) in ring:
        cv.put(x, y, INK)
    for (x, y) in solid:
        u = ((x - x0) / max(1, w) + (y - y0) / max(1, h)) / 2
        cv.put(x, y, LIME[0] if u < 0.30 else LIME[1] if u < 0.62 else LIME[2])
    spark(cv, x0 + k, y0 + k * 2, big=False)
    return cv


def draw_E(s):
    cv = Canvas(s)
    m = s / 32.0
    # the belly: a wide ellipse; the neck: a narrow band; the tie; the ears of the knot
    cx, cy, rx, ry = 16 * m, 20 * m, 12.5 * m, 10.5 * m
    shape(cv, lambda x, y: ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1.0
          or (12 * m <= x <= 20 * m and 6 * m <= y <= 11 * m)
          or (((x - 11 * m) / (3.5 * m)) ** 2 + ((y - 4.5 * m) / (2.5 * m)) ** 2 <= 1.0)
          or (((x - 21 * m) / (3.5 * m)) ** 2 + ((y - 4.5 * m) / (2.5 * m)) ** 2 <= 1.0), SACK)
    for x in range(int(11 * m), int(21 * m) + 1):     # the tie
        for y in (int(9 * m),) if s < 24 else (int(9 * m), int(10 * m)):
            if cv.get(x, y) not in (None,):
                cv.put(x, y, LIME[2])
    glyph(cv, glyph_for(s) if s >= 24 else 'T', int(cx), int(cy) + (1 if s >= 24 else 0), LIME[3] if s >= 24 else INK)
    spark(cv, int(9 * m), int(15 * m), big=s >= 24)
    return cv


def draw_F(s):
    cv = Canvas(s)
    m = s / 32.0
    bill(cv, 1 * m, 4 * m, 27 * m, 19 * m, medallion=False)
    # the bill's own $ at its left, then the coin in front on the right
    glyph(cv, 'S' if s >= 24 else 'T', int(8 * m), int(11.5 * m), DEEP[2])
    coin(cv, 21 * m, 21 * m, 9.5 * m, GOLD)
    spark(cv, int(17 * m), int(15 * m), big=s >= 24)
    return cv


DESIGNS = [('A', 'bill', draw_A), ('B', 'stack', draw_B), ('C', 'gold', draw_C),
           ('D', 'glyph', draw_D), ('E', 'bag', draw_E), ('F', 'cash', draw_F)]
SIZES = (32, 24, 16)


def render():
    os.makedirs(OUT, exist_ok=True)
    made = {}
    for key, name, fn in DESIGNS:
        for s in SIZES:
            got = fn(s)
            im = got if isinstance(got, Image.Image) else got.image()
            path = os.path.join(OUT, 'money_%s_%d.png' % (key, s))
            im.save(path)
            made[(key, s)] = im
    return made


def sheet(made, bg, path, scale=6):
    cols = len(DESIGNS)
    cell = 32 * scale + 40
    W = cols * cell + 40
    H = 3 * (32 * scale + 24) + 40
    out = Image.new('RGBA', (W, H), bg + (255,))
    for c, (key, name, _) in enumerate(DESIGNS):
        y = 20
        for s in SIZES:
            im = made[(key, s)]
            k = scale * 32 // s if s != 24 else scale
            big = im.resize((s * k, s * k), Image.NEAREST)
            x = 20 + c * cell + (32 * scale - s * k) // 2
            out.paste(big, (x, y + (32 * scale - s * k) // 2), big)
            y += 32 * scale + 24
    out.save(path)


def ship(letter=None):
    """THE AUTHOR'S PICK (2026-09-25): "Deste güzel" + "fiyatlarda da D işaret'i kullanabiliriz". The money mark
    (the till, the account, a tip) is the stack; a PRICE wears the glyph; the single bill flies. Written as
    money[_24|_16].png, price[_24|_16].png and money_bill.png."""
    made = render()
    for s, suffix in ((32, ''), (24, '_24'), (16, '_16')):
        made[('B', s)].save(os.path.join(ITEMS, 'money' + suffix + '.png'))
        made[('D', s)].save(os.path.join(ITEMS, 'price' + suffix + '.png'))
        print('shipped Items/money%s.png and Items/price%s.png' % (suffix, suffix))
    bill_unit(32).save(os.path.join(ITEMS, 'money_bill.png'))
    print('shipped Items/money_bill.png', bill_unit(32).size)


if __name__ == '__main__':
    if len(sys.argv) >= 2 and sys.argv[1] == 'ship':
        ship()
        sys.exit(0)
    made = render()
    sheet(made, (0xF2, 0xE8, 0xD5), os.path.join(OUT, 'sheet_paper.png'))
    sheet(made, (0x1B, 0x14, 0x24), os.path.join(OUT, 'sheet_dark.png'))
    print('wrote', len(made), 'icons and two sheets to', OUT)
