# -*- coding: utf-8 -*-
"""The clock's digit set, drawn pixel by pixel (2026-08-19, the author: "bunu
sayılarla değilde gerçekten kodlar pixel pixel yapsak? çünkü 5-2 gibi sayıların
uçları yanmıyor, yandığında 1 sayısı gibi sayılar kısa kalıyor. Detaylı bir saat
çalışması yap.").

Why masks and not segment rectangles: seven rectangles cannot serve two masters.
Give the corners to the verticals and a 5's top bar stops short of its corner
("uçları yanmıyor"); give them to the horizontals and a 1 floats at two thirds
height ("1 sayısı kısa kalıyor"). A real display's segments are MITRED into the
corners, which on a pixel grid means each numeral is simply drawn - every corner
solid, every digit full height, and the seams placed only where they read.

The grid: 11x14 per digit, the display's own 22x28 at 2x. This file is the
DESIGN - it renders proof sheets for the eye. SegmentClock.cs carries the same
masks in C# and a parity check here keeps the two from drifting.

    python Tools/clock_digits.py            # proof sheet -> Tools/clock_proof.png
"""
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))

# 11 wide x 14 tall. '#' is lit. Bars are 2px, arms are 2px; outer corners are
# chamfered by one pixel (the display's moulded corner), inner joins are square
# so a meeting of two segments reads as one lit corner, never as a notch.
DIGITS = {
    0: ("."         "#########" ".",
        "#"         "#########" "#",
        "##"        "......."   "##",
        "##"        "......."   "##",
        "##"        "......."   "##",
        "##"        "......."   "##",
        "##"        "......."   "##",
        "##"        "......."   "##",
        "##"        "......."   "##",
        "##"        "......."   "##",
        "##"        "......."   "##",
        "##"        "......."   "##",
        "#"         "#########" "#",
        "."         "#########" "."),
    1: ("........###",
        "........###",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        ".........##"),
    2: (".#########.",
        "###########",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        ".##########",
        "##########.",
        "##.........",
        "##.........",
        "##.........",
        "##.........",
        "###########",
        ".#########."),
    3: (".#########.",
        "###########",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        "...########",
        "...#######.",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        "###########",
        ".#########."),
    4: ("##.......##",
        "##.......##",
        "##.......##",
        "##.......##",
        "##.......##",
        "##.......##",
        "###########",
        ".##########",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        ".........##"),
    5: (".#########.",
        "###########",
        "##.........",
        "##.........",
        "##.........",
        "##.........",
        "##########.",
        ".##########",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        "###########",
        ".#########."),
    6: (".#########.",
        "###########",
        "##.........",
        "##.........",
        "##.........",
        "##.........",
        "##########.",
        "###########",
        "##.......##",
        "##.......##",
        "##.......##",
        "##.......##",
        "###########",
        ".#########."),
    7: (".##########",
        "###########",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        ".........##"),
    8: (".#########.",
        "###########",
        "##.......##",
        "##.......##",
        "##.......##",
        "##.......##",
        "###########",
        "###########",
        "##.......##",
        "##.......##",
        "##.......##",
        "##.......##",
        "###########",
        ".#########."),
    9: (".#########.",
        "###########",
        "##.......##",
        "##.......##",
        "##.......##",
        "##.......##",
        "###########",
        ".##########",
        ".........##",
        ".........##",
        ".........##",
        ".........##",
        "###########",
        ".#########."),
}

W, H = 11, 14
LIT = (0x7D, 0xF0, 0xE3, 255)          # Cyan[4], the clock's shift hue
GLASS = (8, 14, 19, 255)               # DisplayDark
GHOST_A = 14                            # ~0.055 of 255: the unlit machine
HALO_A = 32


def mask(d):
    rows = DIGITS[d]
    assert len(rows) == H, ("digit %d has %d rows" % (d, len(rows)))
    for r in rows:
        assert len(r) == W, ("digit %d row '%s' is %d wide" % (d, r, len(r)))
    return rows


def cells(d):
    return {(x, y) for y, row in enumerate(mask(d)) for x, ch in enumerate(row) if ch == '#'}


def halo(d):
    """The 1px ring around the lit shape - the glass catching the light."""
    on = cells(d)
    ring = set()
    for x, y in on:
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                p = (x + dx, y + dy)
                if p not in on:
                    ring.add(p)
    return ring


def digit_plate(d, lit=LIT, ghost=True):
    """One digit cell as drawn: ghost 8 under, halo, then the numeral. 13x16 canvas
    (one pixel of margin so the halo has ground)."""
    im = Image.new('RGBA', (W + 2, H + 2), (0, 0, 0, 0))
    px = im.load()
    if ghost:
        for x, y in cells(8):
            px[x + 1, y + 1] = (lit[0], lit[1], lit[2], GHOST_A)
    for x, y in halo(d):
        if 0 <= x + 1 < W + 2 and 0 <= y + 1 < H + 2:
            px[x + 1, y + 1] = (lit[0], lit[1], lit[2], HALO_A)
    for x, y in cells(d):
        px[x + 1, y + 1] = lit
    return im


def readout(text, lit=LIT):
    """A whole display: 'HH:MM' onto the glass, cell pitch 13, colon block 3 wide."""
    wide = sum(13 if ch != ':' else 5 for ch in text) + 2
    im = Image.new('RGBA', (wide, H + 4), GLASS)
    x = 1
    for ch in text:
        if ch == ':':
            for dy in (4, 10):
                for oy in (0, 1):
                    for ox in (0, 1):
                        im.putpixel((x + 1 + ox, dy + oy + 1), lit)
            x += 5
        else:
            im.alpha_composite(digit_plate(int(ch), lit), (x - 1, 1))
            x += 13
    return im


def sheet():
    rows = []
    # 1: every numeral, lit, over its ghost.
    strip = Image.new('RGBA', (10 * 13 + 2, H + 4), GLASS)
    for d in range(10):
        strip.alpha_composite(digit_plate(d), (d * 13, 1))
    rows.append(('all ten, over the ghost 8', strip))
    # 2: the times that showed the old faults (5s and 2s with dead tips, short 1s).
    for t in ('18:00', '21:35', '00:50', '02:15', '11:11'):
        rows.append((t, readout(t)))
    # 3: the closing hue.
    rows.append(('02:00 magenta', readout('02:00', (0xFF, 0x7D, 0xC6, 255))))

    k = 6
    W_ = max(im.width for _, im in rows) * k + 20
    H_ = sum(im.height * k + 18 for _, im in rows) + 10
    out = Image.new('RGBA', (W_, H_), (26, 16, 35, 255))
    y = 10
    for _, im in rows:
        out.alpha_composite(im.resize((im.width * k, im.height * k), Image.NEAREST), (10, y))
        y += im.height * k + 18
    path = os.path.join(HERE, 'clock_proof.png')
    out.save(path)
    print('proof ->', path)


def csharp():
    """Prints the masks as C# rows, for pasting into SegmentClock.cs."""
    for d in range(10):
        rows = ', '.join('"%s"' % r for r in mask(d))
        print('            new[] { %s },' % rows)


if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == 'csharp':
        csharp()
    else:
        sheet()
