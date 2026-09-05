# -*- coding: utf-8 -*-
"""The market's UPGRADE ICONS (2026-09-06, the author: "Upgrade görsellerinde ürünün resmi
yerine geliştirme iconu gibi bir görsel üretilsin ve o kullanılsın, örneğin duvar
güncellemelerinde duvar iconu üstünde yukarı yeşil ok, mobilya geliştirmelerinde sandalye
iconu üzerinde yeşil ok gibi").

One pictogram per upgrade GROUP — what KIND of thing the tile improves — with the same green
up-arrow badge on every one, so the aisle reads as a list of things to raise rather than a
catalogue of things to look at. Drawn here, not generated: these are UI chrome (GDD 16), in
the palette's own ramps, 24x24 so the tile's 96-unit art band shows them at exactly 4x.

    py -3 Tools/upgrade_icons.py          writes Assets/Resources/Items/up_<group>.png
    py -3 Tools/upgrade_icons.py --sheet  also writes Tools/upgrade_icons_preview.png at 4x
"""
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
SIZE = 24

# UITheme ramps (Assets/Scripts/UI/UITheme.cs), darkest first.
NIGHT = [(0x0D, 0x08, 0x13), (0x1A, 0x10, 0x23), (0x24, 0x18, 0x30), (0x36, 0x24, 0x47), (0x4A, 0x31, 0x60)]
LIME = [(0x16, 0x33, 0x1B), (0x2A, 0x59, 0x26), (0x47, 0x99, 0x38), (0x6F, 0xCC, 0x4B), (0xA8, 0xF0, 0x77)]
CREAM = [(0x45, 0x3E, 0x38), (0x6E, 0x64, 0x59), (0x9C, 0x8F, 0x80), (0xC9, 0xBC, 0xA8), (0xF2, 0xE8, 0xD5)]
GRAPHITE = [(0x14, 0x16, 0x1A), (0x24, 0x27, 0x2D), (0x38, 0x3D, 0x45), (0x54, 0x5A, 0x64), (0x80, 0x88, 0x93)]
AMBER = [(0x4A, 0x2E, 0x14), (0x8F, 0x5A, 0x1E), (0xC9, 0x82, 0x2B), (0xE8, 0xA3, 0x3D), (0xF5, 0xC9, 0x7B)]
CYAN = [(0x12, 0x3B, 0x45), (0x1B, 0x5F, 0x66), (0x26, 0x91, 0x8F), (0x3B, 0xC8, 0xBE), (0x7D, 0xF0, 0xE3)]

INK = GRAPHITE[1]          # every pictogram's outline: one line weight, one ink
BODY = CREAM[3]            # the pictogram's face
SHADE = CREAM[2]           # its turned side
LIGHT = CREAM[4]


def canvas():
    return Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))


def box(d, x0, y0, x1, y1, fill, ink=INK):
    """A filled rectangle with a 1px ink ring (inclusive coordinates)."""
    d.rectangle([x0, y0, x1, y1], fill=ink)
    if x1 - x0 >= 2 and y1 - y0 >= 2:
        d.rectangle([x0 + 1, y0 + 1, x1 - 1, y1 - 1], fill=fill)


def arrow(im):
    """The badge: a green up-arrow in the top-right corner, 9 wide, 10 tall, ringed in the
    ramp's dark so it stands off any pictogram under it."""
    d = ImageDraw.Draw(im)
    ox, oy = SIZE - 10, 0
    # ring first (one px wider all round), then the arrow inside it
    head = [(4, 0), (8, 4), (6, 4), (6, 9), (2, 9), (2, 4), (0, 4)]
    ring = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))
    rd = ImageDraw.Draw(ring)
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            rd.polygon([(ox + x + dx, oy + y + dy) for x, y in head], fill=LIME[0])
    im.alpha_composite(ring)
    d = ImageDraw.Draw(im)
    d.polygon([(ox + x, oy + y) for x, y in head], fill=LIME[3])
    # a lighter edge on the left of the head and the shaft: the badge has a light side
    d.line([(ox + 4, oy + 0), (ox + 1, oy + 3)], fill=LIME[4])
    d.line([(ox + 2, oy + 5), (ox + 2, oy + 8)], fill=LIME[4])
    return im


def walls():
    """A wall: a plaster panel with a cornice line and a wainscot band."""
    im = canvas(); d = ImageDraw.Draw(im)
    box(d, 1, 4, 16, 22, BODY)
    d.line([(2, 7), (15, 7)], fill=SHADE)              # the cornice stripe
    d.rectangle([2, 16, 15, 21], fill=SHADE)           # the wainscot
    for x in (5, 9, 13):
        d.line([(x, 17), (x, 20)], fill=INK)            # its panels
    return arrow(im)


def light():
    """A wall lamp: a sconce bracket under a lit shade."""
    im = canvas(); d = ImageDraw.Draw(im)
    d.polygon([(3, 12), (13, 12), (11, 5), (5, 5)], fill=INK)           # the shade's ring
    d.polygon([(4, 11), (12, 11), (10, 6), (6, 6)], fill=AMBER[4])       # lit
    d.polygon([(5, 10), (11, 10), (10, 8), (6, 8)], fill=AMBER[3])
    box(d, 7, 12, 9, 17, GRAPHITE[3])                                    # the stem
    box(d, 4, 17, 12, 22, BODY)                                          # the bracket
    d.rectangle([5, 18, 11, 19], fill=SHADE)
    return arrow(im)


def furniture():
    """A chair, in profile: seat, back and two legs."""
    im = canvas(); d = ImageDraw.Draw(im)
    box(d, 3, 3, 6, 15, AMBER[2])                                        # the back
    box(d, 3, 12, 15, 16, AMBER[3])                                      # the seat
    d.rectangle([4, 13, 14, 13], fill=AMBER[4])
    box(d, 4, 16, 6, 22, AMBER[1])                                       # legs
    box(d, 12, 16, 14, 22, AMBER[1])
    return arrow(im)


def greenery():
    """A plant: three leaves in a pot."""
    im = canvas(); d = ImageDraw.Draw(im)
    box(d, 5, 15, 12, 22, AMBER[2])                                      # the pot
    d.rectangle([6, 16, 11, 16], fill=AMBER[3])
    d.line([(8, 8), (8, 15)], fill=LIME[1])                              # the stem
    for pts, fill in (([(8, 9), (3, 6), (2, 10), (7, 12)], LIME[3]),
                      ([(9, 9), (14, 5), (15, 9), (10, 12)], LIME[3]),
                      ([(8, 8), (6, 2), (10, 2)], LIME[2])):
        d.polygon(pts, fill=fill, outline=LIME[0])
    return arrow(im)


def counter():
    """A tap: the counter's own fitting — spout, handle, and the bar it stands on."""
    im = canvas(); d = ImageDraw.Draw(im)
    box(d, 2, 18, 16, 22, GRAPHITE[3])                                   # the bar top
    box(d, 8, 8, 11, 18, GRAPHITE[4])                                    # the column
    d.rectangle([9, 9, 9, 17], fill=CREAM[4])
    box(d, 3, 8, 11, 11, GRAPHITE[4])                                    # the spout, leftward
    d.rectangle([3, 11, 4, 13], fill=INK)                                # its lip
    box(d, 8, 2, 11, 8, AMBER[3])                                        # the handle
    return arrow(im)


def seats():
    """A bar stool: a round top on a stem and a foot."""
    im = canvas(); d = ImageDraw.Draw(im)
    box(d, 2, 6, 15, 10, CYAN[3])                                        # the cushion
    d.rectangle([3, 7, 14, 7], fill=CYAN[4])
    box(d, 7, 10, 10, 19, GRAPHITE[4])                                   # the stem
    box(d, 3, 19, 14, 22, GRAPHITE[3])                                   # the foot ring
    return arrow(im)


def bar():
    """The bar top itself: a counter slab with its front face."""
    im = canvas(); d = ImageDraw.Draw(im)
    box(d, 1, 8, 16, 12, AMBER[3])                                       # the top
    d.rectangle([2, 9, 15, 9], fill=AMBER[4])
    box(d, 2, 12, 15, 22, AMBER[1])                                      # the front
    for x in (6, 11):
        d.line([(x, 13), (x, 21)], fill=AMBER[0])                        # its panels
    return arrow(im)


def glass():
    """A glass: a tumbler with a drink line."""
    im = canvas(); d = ImageDraw.Draw(im)
    d.polygon([(3, 4), (14, 4), (12, 22), (5, 22)], fill=INK)
    d.polygon([(4, 5), (13, 5), (11, 21), (6, 21)], fill=CYAN[4])
    d.polygon([(5, 12), (12, 12), (11, 21), (6, 21)], fill=CYAN[2])      # the drink
    d.line([(5, 6), (5, 20)], fill=LIGHT)                                # the sheen
    return arrow(im)


ICONS = {
    'walls': walls, 'light': light, 'furniture': furniture, 'greenery': greenery,
    'counter': counter, 'seats': seats, 'bar': bar, 'glass': glass,
}


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, draw in ICONS.items():
        im = draw()
        assert im.size == (SIZE, SIZE)
        im.save(os.path.join(OUT, 'up_%s.png' % name))
        print('up_%s.png' % name, len({p for p in im.getdata() if p[3] > 0}), 'colours')
    if '--sheet' in sys.argv:
        k = 4
        sheet = Image.new('RGBA', (len(ICONS) * (SIZE * k + 12) + 12, SIZE * k + 24), (0xC8, 0xC8, 0xD8, 255))
        x = 12
        for name, draw in ICONS.items():
            sheet.alpha_composite(draw().resize((SIZE * k, SIZE * k), Image.NEAREST), (x, 12))
            x += SIZE * k + 12
        sheet.save(os.path.join(HERE, 'upgrade_icons_preview.png'))
        print('preview written')


if __name__ == '__main__':
    main()
