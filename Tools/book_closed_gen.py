# -*- coding: utf-8 -*-
"""The recipe book SHUT, standing on the bar (2026-08-25, the author: "Book butonu ise
tezgahin ustune sabitlensin ve yeni uretilen book arka plan ince uzun menu tasariminin
kapali kucuk bir goruntusunu olusturup tezgahin ustune yerlestirelim ona tiklayarak menu
acilacak").

DERIVED FROM THE OPEN BOOK, NOT DRAWN BESIDE IT. This project has paid three times for the
opposite (see memory: open states are derived, never generated) - a second take of the same
object comes back as a DIFFERENT object, and the player is the one who notices that the thing
on the counter is not the thing that opens. So every colour here is READ OFF
Items/menu_booklet.png at run time and every proportion comes from the page the booklet
actually turns:

    cover      #4A2E14  the booklet's outer border          (Amber[0])
    board      #3A2410  its inner board, and the spine's sides (Malt[0])
    well       #2A1A0C  the stitched well down its middle
    leaf       #F2E8D5  the page                            (Cream[4])
    leaf edge  #C9BCA8 / #9C8F80  the page block's shading  (Cream[3] / Cream[2])
    gilt       #C9822B / #8F5A1E  the rules                 (Amber[2] / Amber[1])
    ribbon     #6E1B32  the marker hanging out of the foot  (ViceRed[1])

The shape is one leaf of the open booklet stood on its foot: 167 x 326 art px is the page
menu_page_frame is cut to, so the closed book is that aspect and nothing else. It is struck
at the size it will be DRAWN - one art pixel to one stage unit, the counter's own grain -
because a book downscaled from a big drawing lands between the counter's pixels.

    py -3 Tools/book_closed_gen.py            # write the sprite + a preview
    py -3 Tools/book_closed_gen.py --preview  # the preview only, nothing shipped
"""
import io
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
BOOKLET = os.path.join(ITEMS, 'menu_booklet.png')

# Where each colour is READ from on the open booklet, so this file names positions and the
# art names the colours. A booklet that is ever re-drawn re-colours this without an edit.
PROBES = {
    'cover': (4, 4),
    'board': (178, 100),
    'well': (185, 100),
    'leaf': (20, 20),
    'ribbon': (185, 345),
}

# One leaf of the booklet, stood up. 167 x 326 is the page; 28 wide keeps that aspect at the
# size a prop on this counter wants to be (the till beside it is 57 stage units wide).
W = 28
H = int(round(W * 326.0 / 167.0))       # 55


def probe():
    im = Image.open(BOOKLET).convert('RGBA')
    px = im.load()
    out = {}
    for name, (x, y) in PROBES.items():
        r, g, b, a = px[x, y]
        if a == 0:
            sys.exit('probe %s at %s is transparent - the booklet art moved' % (name, (x, y)))
        out[name] = (r, g, b, 255)
    return out


def shade(c, k):
    """A step along the colour itself. Used only for the page block's own banding, which is
    the same leaf in shadow and not a new colour in the palette."""
    return (max(0, min(255, int(c[0] * k))),
            max(0, min(255, int(c[1] * k))),
            max(0, min(255, int(c[2] * k))), 255)


def book():
    c = probe()
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    px = img.load()

    def rect(x0, y0, x1, y1, col):
        for y in range(max(0, y0), min(H, y1 + 1)):
            for x in range(max(0, x0), min(W, x1 + 1)):
                px[x, y] = col

    # THE BLOCK. Cover across the whole face, then the two edges that make it read as a book
    # rather than as a card: a spine on the left with a well down it, and the page block
    # showing at the right as a stack of leaves.
    rect(0, 0, W - 1, H - 1, c['cover'])

    # The fore-edge: four columns of leaf, banded so the stack reads as paper and not as a
    # cream stripe. The outermost is the darkest - it is the edge in shadow.
    leaf = c['leaf']
    rect(W - 5, 1, W - 5, H - 2, shade(leaf, 0.62))
    rect(W - 4, 1, W - 4, H - 2, shade(leaf, 0.80))
    rect(W - 3, 1, W - 3, H - 2, leaf)
    rect(W - 2, 2, W - 2, H - 3, shade(leaf, 0.86))

    # The spine: the board's own darker step, with the stitched well down the middle of it,
    # exactly as the open booklet draws its gutter.
    rect(0, 0, 3, H - 1, c['board'])
    rect(1, 1, 1, H - 2, c['well'])
    for y in range(3, H - 3, 4):             # the stitch, dashed the way the gutter's is
        px[2, y] = c['well']
        px[2, y + 1] = c['well']

    # THE COVER'S OWN FURNITURE: a gilt rule inset from the edge and a title band across the
    # top third. At 28 px a drawn emblem is four brown pixels and a guess; two rules and a
    # band are what a menu cover actually has and they survive the size.
    gilt = (0xC9, 0x82, 0x2B, 255)
    gilt_lo = (0x8F, 0x5A, 0x1E, 255)
    rect(6, 3, W - 7, 3, gilt_lo)
    rect(6, H - 4, W - 7, H - 4, gilt_lo)
    rect(6, 3, 6, H - 4, gilt_lo)
    rect(W - 7, 3, W - 7, H - 4, gilt_lo)
    rect(8, 10, W - 9, 10, gilt)
    rect(8, 13, W - 9, 13, gilt)
    rect(9, 16, W - 10, 16, gilt_lo)

    # The board's edge, so the cover is not a flat slab: one darker column down the left of
    # the face and one darker row along the foot, which is where a closed book takes its
    # shadow from the thing it is standing on.
    rect(4, 0, 4, H - 1, c['board'])
    rect(0, H - 1, W - 1, H - 1, c['board'])
    rect(0, 0, W - 1, 0, c['board'])

    # THE MARKER, hanging out of the foot. Two pixels of it, which is all a ribbon is at this
    # size, and the one warm accent that says this book is in use.
    px[W - 4, H - 1] = c['ribbon']
    px[W - 4, H - 2] = c['ribbon']
    return img


def preview(img):
    scale = 8
    pad = 12
    bar = (0x24, 0x27, 0x2D, 255)          # the counter's own graphite, to stand it on
    out = Image.new('RGBA', (img.size[0] * scale + pad * 2,
                             img.size[1] * scale + pad * 2), bar)
    big = img.resize((img.size[0] * scale, img.size[1] * scale), Image.NEAREST)
    out.alpha_composite(big, (pad, pad))
    return out.convert('RGB')


META_SRC = os.path.join(HERE, 'open_sign_gen.py')


def ship(img, name):
    """Written with the same hand-authored .meta the sign uses: Resources/Items is not under
    LastCallImporter's rule, so a PNG that lands there without one imports as a blurry
    Default texture until somebody force-reimports it."""
    sys.path.insert(0, HERE)
    import open_sign_gen as sign
    path = os.path.join(ITEMS, name + '.png')
    img.save(path)
    meta = path + '.meta'
    if not os.path.exists(meta):
        io.open(meta, 'w', encoding='utf-8', newline='\n').write(
            sign.META % sign.guid_for(name))
    print('  %-20s %dx%d' % (name + '.png', img.size[0], img.size[1]))


def main():
    b = book()
    preview(b).save(os.path.join(HERE, 'book_closed_preview.png'))
    print('  preview              Tools/book_closed_preview.png')
    if '--preview' in sys.argv:
        return
    ship(b, 'book_closed')


if __name__ == '__main__':
    main()
