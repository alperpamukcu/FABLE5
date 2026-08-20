# -*- coding: utf-8 -*-
"""Take the icon off the generated key, so the key can be any width (2026-08-21).

The sheet came back with a beer glass drawn into the square key and a cocktail glass in
the wide one, which looks good and is exactly wrong for a nine-slice: anything in the
middle of the sprite lives in the STRETCHABLE centre, so a 300 px button drew a 300 px
wide beer glass. The icon has to be a separate sprite drawn on top, not part of the plate.

Removing it cleanly is not a flood fill. The face is not one colour - it carries a lighter
band across its top, which is half of what makes the key look raised - so a single fill
would flatten the very thing the author asked for. Instead each interior ROW is set to its
own dominant colour: the banding survives because it is horizontal, and the icon vanishes
because it is not.

Out:  ui_key.png        the blank raised key, nine-sliceable in both axes
      ui_icon_beer.png  the glass that was inside it, cut out to be drawn on top
"""
import collections, os, sys
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'scene_cast_raw')


def clean(src, border, out_key, out_icon=None):
    im = Image.open(src).convert('RGBA')
    px = im.load()
    W, H = im.size
    l, b, r, t = border
    icon = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    ip = icon.load()

    # FIRST FIND THE ICON, THEN REPAINT ONLY IT. The first version repainted every
    # interior row with that row's dominant colour, and it streaked the face with white
    # bars: on the rows crossing the glass's outline the dominant colour WAS the outline,
    # so the whole row became it. Dominance is the wrong question when the thing being
    # removed is sometimes the majority of its own row.
    #
    # So the icon is located first - the interior pixels that differ from their row's left
    # margin, which is always face - and then only its bounding box is repainted, each row
    # taking the colour from just outside the box on its left. Everything the icon does not
    # cover is left byte for byte alone, which is what keeps the raised banding intact.
    def face_at(y):
        for x in range(l + 2, W - r):
            if px[x, y][3] > 40:
                return px[x, y]
        return None

    xs, ys = [], []
    for y in range(t, H - b):
        f = face_at(y)
        if f is None:
            continue
        for x in range(l, W - r):
            c = px[x, y]
            if c[3] > 40 and c != f:
                xs.append(x); ys.append(y)
    if xs:
        x0, x1 = min(xs), max(xs)
        y0, y1 = min(ys), max(ys)
        for y in range(y0, y1 + 1):
            src_x = max(l, x0 - 2)
            fill = px[src_x, y]
            for x in range(x0, x1 + 1):
                if px[x, y][3] > 40:
                    ip[x, y] = px[x, y]
                    px[x, y] = fill
    im.save(out_key)
    if out_icon:
        bb = icon.getbbox()
        if bb:
            icon.crop(bb).save(out_icon)
            print('icon %dx%d -> %s' % (bb[2] - bb[0], bb[3] - bb[1], out_icon))
    print('blank key -> %s' % out_key)
    return im


if __name__ == '__main__':
    src = os.path.join(RAW, '_ui3d_shape1.png')
    if not os.path.exists(src):
        sys.exit('run the sheet extraction first')
    clean(src, (18, 18, 18, 24),
          os.path.join(RAW, 'ui_key.png'),
          os.path.join(RAW, 'ui_icon_beer.png'))
