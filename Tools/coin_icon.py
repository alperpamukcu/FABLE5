# -*- coding: utf-8 -*-
"""The COIN — money's own mark (2026-09-07, the author: "2D siyah kontras yesil ic planli dolar
iconu uret ve oyunda para gosteren her yere $ yerine o iconu koy").

Every number in this game that means money was printed with a typed dollar sign: one glyph from
whatever face the row happened to be set in, so the till read in Silkscreen, the market in
Silkscreen Bold and the slip in the display face - three different dollars for one currency, and
none of them an object. The star and the heart were fixed exactly this way on 2026-09-04 ("one
star and one heart"); this is the same fix for money.

It is built in the house language so it reads as a third member of that set:

  * the same 32x32 canvas and the same one-pixel INK keyline INSIDE the silhouette - which is
    the "siyah kontras" asked for, and is how every other mark here is drawn;
  * the same three-tone body lit from the upper left, and the same five-pixel sparkle;
  * the interior in UITheme.Lime - the "yesil ic" - so money is the only green thing in the
    chrome and cannot be confused with the amber of a star or the red of a fine.

The shape is a DISC struck with an S and two bars: a coin, not a banknote and not a bare glyph.
The S is cut out of the metal to the keyline ink, so it holds its shape at 16 px, where a
green-on-green relief would silt up. The 16 px pair is derived by Tools/icon_sizes.py, which
lists the coin beside the star, the heart and the medallion.

Nothing here is generated art; it is the procedural kit, like every other mark in the UI
(memory art-direction-rules).

  py -3 -X utf8 Tools/coin_icon.py          # writes the PNGs into Resources/Items
  py -3 -X utf8 Tools/coin_icon.py preview  # ...and a 6x contact sheet beside the other marks
"""
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
INK = (0x0D, 0x08, 0x13)
SPARK = (0xF2, 0xE8, 0xD5)

# UITheme.Lime, lightest first - the author's "yesil ic".
LIT = [(0xA8, 0xF0, 0x77), (0x6F, 0xCC, 0x4B), (0x47, 0x99, 0x38), (0x2A, 0x59, 0x26)]
# A darker green band for the milled rim, so the edge separates from the face.
RIM = [(0x6F, 0xCC, 0x4B), (0x47, 0x99, 0x38), (0x2A, 0x59, 0x26), (0x16, 0x33, 0x1B)]
# The socket's violets, read off star3d_socket.png so every empty state in the game matches.
SOCKET = [(0x4A, 0x31, 0x60), (0x36, 0x24, 0x47), (0x24, 0x18, 0x30), (0x1A, 0x10, 0x23)]

# The geometry is written as FRACTIONS of the canvas so the same code draws the 16 and the 32.
# The coin cannot be derived by Tools/icon_sizes.py the way the star and the heart are: that
# derivation PEELS the keyline ink before it averages (an ink ring dragged into the body turns a
# small icon muddy), and this icon's dollar sign IS keyline ink struck through the metal - it
# would peel the glyph off and leave a blank green disc. So the 16 is drawn, not shrunk, which is
# the law icon_sizes.py itself opens with: at a size the art was not drawn for, you REDRAW.
S = 32
CX = CY = 0.5           # centre, as a fraction
R_F = 0.444             # radius
RIM_F = 0.075           # milled band, measured inward from the edge

# The S: three horizontal arms joined by two uprights, plus the vertical bar through it, all as
# fractions. Measured against the first preview - at a half-width of 0.06 the glyph closed its
# own counters and the coin read as a black disc with green edges; 0.042 leaves two clear metal
# cells inside each bowl at 32 and one at 16, which is the floor.
# The stroke is a FRACTION at 32 and a measured minimum at 16: proportional scaling closed the
# S's counters at the small size (drawn and measured), because a counter needs whole cells and a
# proportional stroke does not know that. 0.042 * 32 = 1.34 cells at 32; at 16 the same fraction
# is 0.67, which the 4x sampler rounds up into a full cell either side and silts the bowls.
SW_F = 0.042            # stroke half-width, of the canvas
SW_MIN_CELLS = 0.34     # ...but never thicker than this many cells at a small canvas
S_HALF = 0.119          # how far the S runs either side of centre
S_RISE = 0.156          # top and bottom arms, from centre
BAR_RISE = 0.250        # the vertical bar, from centre


def on_s(u, v, n):
    """Is this sample inside the dollar glyph (the S plus its vertical bar)? u, v in cells.

    At n=8 the whole coin is eight cells across and the S's three arms plus two uprights want
    five rows of ink inside a disc that is six rows tall - it cannot be drawn, and shrinking
    the 16 into it produces the blob measured on 2026-09-07. So the smallest coin carries a
    STEM AND TWO BARS instead: the dollar reduced to the part of it that still reads at that
    size, which is the same reduction a real coin's lettering gets when it is struck small.
    """
    # NO EIGHT-PIXEL COIN. It was drawn, measured and thrown away on 2026-09-07: at eight
    # cells the disc is six across, and a dollar sign - even reduced to a stem and two bars -
    # takes every one of them, so the mark came back as a black blob with a green rim. The
    # house's other marks (star, heart, medallion) read at 16 because their SILHOUETTES carry
    # the meaning and their interiors are only shading; a coin's meaning is interior detail by
    # nature, so it has a floor the others do not. Sixteen is that floor, and the caller is
    # responsible for giving it sixteen screen pixels - see ItemArt.Coin.

    cx, cy = CX * n, CY * n
    sw = SW_F * n if n >= 32 else min(SW_F * n, SW_MIN_CELLS * 2)
    half, rise, bar = S_HALF * n, S_RISE * n, BAR_RISE * n
    x0, x1 = cx - half, cx + half
    top, mid, bot = cy - rise, cy, cy + rise
    # the vertical stroke through the middle
    if abs(u - cx) <= sw * 0.62 and cy - bar <= v <= cy + bar:
        return True
    # three horizontal arms of the S
    for y in (top, mid, bot):
        if abs(v - y) <= sw * 0.72 and x0 <= u <= x1:
            return True
    # upper upright on the left, lower upright on the right - what makes it an S and not an equals
    if abs(u - x0) <= sw * 0.72 and top <= v <= mid:
        return True
    if abs(u - x1) <= sw * 0.72 and mid <= v <= bot:
        return True
    return False


def region(u, v, n):
    """'ink' / 'rim' / 'face' / None for a sample point on an n-cell canvas."""
    cx, cy, r, rim_w = CX * n, CY * n, R_F * n, RIM_F * n
    d = ((u - cx) ** 2 + (v - cy) ** 2) ** 0.5
    if d > r:
        return None
    if on_s(u, v, n):
        return 'ink'            # the glyph is struck THROUGH the metal, in the keyline ink
    return 'rim' if d >= r - rim_w else 'face'


def cells(n):
    """Sampled 4x4 and thresholded, so the outline lands where the eye puts it. Four rather
    than two because at 16 cells a 2x2 sample cannot tell a 0.7-cell stroke from nothing."""
    grid = []
    for ty in range(n):
        row = []
        for x in range(n):
            hits = {}
            for sy in range(4):
                for sx in range(4):
                    r = region(x + 0.125 + sx * 0.25, ty + 0.125 + sy * 0.25, n)
                    if r:
                        hits[r] = hits.get(r, 0) + 1
            if sum(hits.values()) < 8:
                row.append(None)
            else:
                # THE GLYPH WINS EVERY TIE. A dollar sign that thins out is not a dollar sign,
                # and at 16 cells the stroke is under a cell wide, so it is claimed on a
                # QUARTER of the samples rather than a half - the one place this drawing is
                # deliberately fatter than its geometry, because the alternative is a blank
                # green disc.
                # The glyph's QUARTER-claim is what keeps a sub-cell stroke alive at 16 and
                # 32. At 8 it would paint a whole cell off one sample and swallow the disc,
                # so there the ink has to earn its cell on half the samples like anything
                # else (drawn both ways and measured).
                need = max(1, sum(hits.values()) // (2 if n <= 8 else 4))
                if hits.get('ink', 0) >= need:
                    row.append('ink')
                else:
                    for pick in ('face', 'rim'):
                        if hits.get(pick, 0) >= 8:
                            row.append(pick)
                            break
                    else:
                        row.append(max(hits, key=hits.get))
        grid.append(row)
    return grid


def shade(x0, x1, y0, y1, x, ty):
    """0..3: how far a cell is from a light point off the upper-left shoulder."""
    span = (x1 - x0) + (y1 - y0)
    d = (((x - (x0 + (x1 - x0) * 0.30)) ** 2 + (ty - (y0 + (y1 - y0) * 0.22)) ** 2) ** 0.5)
    t = min(1.0, d / float(max(1, span) * 0.52))
    return 0 if t < 0.34 else 1 if t < 0.62 else 2 if t < 0.90 else 3


def draw(body, rim, n=32):
    grid = cells(n)
    im = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    px = im.load()
    pts = [(x, ty) for ty in range(n) for x in range(n) if grid[ty][x]]
    x0, x1 = min(p[0] for p in pts), max(p[0] for p in pts)
    y0, y1 = min(p[1] for p in pts), max(p[1] for p in pts)
    for ty in range(n):
        for x in range(n):
            kind = grid[ty][x]
            if not kind:
                continue
            if kind == 'ink':
                px[x, ty] = INK + (255,)
                continue
            edge = (ty == 0 or ty == n - 1 or x == 0 or x == n - 1
                    or not grid[ty - 1][x] or not grid[ty + 1][x]
                    or not grid[ty][x - 1] or not grid[ty][x + 1])
            if edge:
                px[x, ty] = INK + (255,)
                continue
            i = shade(x0, x1, y0, y1, x, ty)
            px[x, ty] = (body if kind == 'face' else rim)[i] + (255,)
    # The sparkle, where the light lands on the metal: a plus, five pixels, kept off the glyph.
    cx, cy = int((CX - R_F * 0.52) * n), int((CY - R_F * 0.52) * n)
    spark = ((0, 0), (-1, 0), (1, 0), (0, -1), (0, 1)) if n >= 32 else ((0, 0),)
    for dx, dy in spark:
        if grid[cy + dy][cx + dx] in ('face', 'rim'):
            px[cx + dx, cy + dy] = SPARK + (255,)
    return im


# ── THE SIXTEEN, DRAWN BY HAND ────────────────────────────────────────────────────────────
# The geometry above draws a beautiful 32 and cannot draw a 16, and it took five attempts to
# accept why: a legible dollar needs three horizontal arms with a clear counter between each
# pair, which is 3 + 2 + 2 + 2 + 3 = eleven rows at one cell of stroke - and a 16px coin has
# ten rows inside its rim. Every sampled version therefore closed its own counters and came
# back as a black blob with a green edge (measured against star3d_16, whose SILHOUETTE carries
# its meaning and whose interior is only shading - which is why the star survives the size and
# a coin does not).
#
# So the small coin is WRITTEN, the way every mark in ChromeArt is written: the S drops its
# middle arm and becomes a stem with two hooks, which is what a dollar reduces to when it is
# struck small, and the counters are placed by hand where they will survive.
#
#   #  the keyline and the glyph      o  metal (shaded by the ramp below)      .  nothing
COIN16 = [
    "................",
    ".....######.....",
    "...##oooooo##...",
    "..#oooo##oooo#..",
    ".#ooo##oo##ooo#.",
    ".#ooo#oooooooo#.",
    ".#oooo##ooooo o#",
    ".#oooooo##oooo#.",
    ".#oooooooo##oo#.",
    ".#ooooooooo#oo#.",
    ".#ooo##oo##ooo#.",
    "..#oooo##oooo#..",
    "..#oooooooooo#..",
    "...##oooooo##...",
    ".....######.....",
    "................",
]


def draw16(body, rim):
    """The hand-written 16, shaded by the same ramp and light as its big sister."""
    im = Image.new('RGBA', (16, 16), (0, 0, 0, 0))
    px = im.load()
    grid = [list(r.ljust(16)[:16]) for r in COIN16]
    pts = [(x, y) for y in range(16) for x in range(16) if grid[y][x] in '#o']
    x0, x1 = min(p[0] for p in pts), max(p[0] for p in pts)
    y0, y1 = min(p[1] for p in pts), max(p[1] for p in pts)
    for y in range(16):
        for x in range(16):
            c = grid[y][x]
            if c == '#':
                px[x, y] = INK + (255,)
            elif c == 'o':
                d = ((x - (x0 + (x1 - x0) * 0.32)) ** 2 + (y - (y0 + (y1 - y0) * 0.24)) ** 2) ** 0.5
                t = min(1.0, d / (((x1 - x0) + (y1 - y0)) * 0.52))
                i = 0 if t < 0.34 else 1 if t < 0.62 else 2 if t < 0.90 else 3
                # the outermost ring of metal takes the rim's darker ramp
                edge = any(grid[y + dy][x + dx] == '.' for dx, dy in ((1,0),(-1,0),(0,1),(0,-1))
                           if 0 <= x + dx < 16 and 0 <= y + dy < 16)
                px[x, y] = (rim if edge else body)[i] + (255,)
    # one sparkle, where the light lands
    if grid[4][4] == 'o':
        px[4, 4] = SPARK + (255,)
    return im


# ── THE TWENTY-FOUR ───────────────────────────────────────────────────────────────────────
# The author asked for the coin about half again as big (2026-09-08: "daha 1.5 kat daha buyuk
# bir tasarim olabilir"). 16 * 1.5 = 24, and 24 is a whole multiple of neither master, so it is
# drawn at 24 rather than resampled - but by the SAME geometry as the 32, not by hand. Hand-
# writing it was tried first and thrown away the same hour: at 24 cells the S has enough room to
# be a real S, so there is nothing for a hand-drawn grid to buy, and mine came back with a
# lopsided bowl and a notch bitten out of the rim.
#
# What the geometry needs at 24 is what it needed at 16: a stroke that does not close the
# counters. SW_MIN_CELLS handles it - at 24 the fraction gives 1.0 cells and the cap leaves it
# there, which is exactly one cell of ink with clear metal either side.


def main():
    made = {}
    for n, suffix in ((32, ''), (24, '_24'), (16, '_16')):
        lit = draw16(LIT, RIM) if n == 16 else draw(LIT, RIM, n)
        socket = draw16(SOCKET, SOCKET) if n == 16 else draw(SOCKET, SOCKET, n)
        sp = socket.load()
        for y in range(n):
            for x in range(n):
                if sp[x, y][:3] == SPARK:
                    sp[x, y] = SOCKET[0] + (255,)
        lit.save(os.path.join(ITEMS, 'coin3d%s.png' % suffix))
        socket.save(os.path.join(ITEMS, 'coin3d_socket%s.png' % suffix))
        made['coin3d' + suffix] = lit
        made['coin3d_socket' + suffix] = socket
    print('wrote ' + ', '.join(k + '.png' for k in made))

    if len(sys.argv) > 1 and sys.argv[1] == 'preview':
        names = ['star3d', 'heart3d', 'medal3d']
        big = [Image.open(os.path.join(ITEMS, n + '.png')).convert('RGBA') for n in names]
        big += [made['coin3d_socket'], made['coin3d']]
        small = [Image.open(os.path.join(ITEMS, n + '_16.png')).convert('RGBA') for n in names]
        small += [made['coin3d_socket_16'], made['coin3d_16']]
        k, cell = 6, 32 * 6 + 8
        sheet = Image.new('RGBA', (len(big) * cell + 8, 32 * k + 16 * k + 24), (36, 24, 48, 255))
        for i, (b, sm) in enumerate(zip(big, small)):
            x = 8 + i * cell
            r = b.resize((32 * k, 32 * k), Image.NEAREST)
            sheet.paste(r, (x, 8), r)
            q = sm.resize((16 * k, 16 * k), Image.NEAREST)
            sheet.paste(q, (x + (32 * k - 16 * k) // 2, 32 * k + 14), q)
        out = os.path.join(os.environ.get('TEMP', '.'), 'coin_preview.png')
        sheet.save(out)
        print('preview', out)


if __name__ == '__main__':
    main()
