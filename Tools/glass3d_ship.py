# -*- coding: utf-8 -*-
"""SHIP THE GENERATED GLASSES (2026-09-06): the staging set becomes the Items set the game
loads through GlassArt.FromGenerated — one sprite and one cavity mask a glass.

The generator draws each glass SOLID (frosted, opaque), which is what the author asked to
look at; the game needs the drink to show through the walls, so this cuts the CAVITY:

  * `glass3d_<id>_fill.png`  — white where the drink may be drawn: the interior, from the
    mouth's lower lip down to the floor, inset from the silhouette by the wall's thickness;
  * `glass3d_<id>.png`       — the glass itself, its cavity pixels turned translucent
    (GLASS_ALPHA) so the drink reads THROUGH thick frosted glass rather than over it.

The cavity is a few numbers per glass, read off the drawing by eye at 5x (rows from the
top, in the sprite's own pixels), because the artist's walls are not where a formula would
put them and a mask a pixel wrong shows the drink on the steel. The numbers print the
Gen3D fractions GlassArt wants (floor, rim, interior half) so the table is copied, not typed.

    py -3 -X utf8 Tools/glass3d_ship.py            writes the six-file set into Resources/Items
    py -3 -X utf8 Tools/glass3d_ship.py preview    also writes Tools/glass3d_ship_preview.png
    py -3 -X utf8 Tools/glass3d_ship.py --dry preview   the preview only, nothing into Assets
"""
import io
import json
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
STAGING = os.path.join(HERE, 'AssetPipeline', 'staging', 'glass3d')

GLASS_ALPHA = 118          # 0..255: how much of the drink the frosted wall lets through

# id: (mouth_row, floor_row, wall_px) — the cavity runs from the mouth's lower lip (the
# first row the drink may reach) to the floor's top edge, inset `wall_px` from the
# silhouette on each row. None = not landed / not measured yet.
CAVITY = {
    'rocks':    (15, 47, 5),
    'pint':     (19, 72, 5),
    'highball': (21, 78, 4),
    'martini':  (18, 40, 4),
    'coupe':    (19, 39, 4),
}


def silhouette(im):
    px = im.load()
    rows = {}
    for y in range(im.height):
        xs = [x for x in range(im.width) if px[x, y][3] > 0]
        if xs:
            rows[y] = (min(xs), max(xs))
    return rows


def ship(key, preview_cells):
    src = os.path.join(STAGING, key + '.png')
    if not os.path.exists(src) or CAVITY.get(key) is None:
        print('  %-9s skipped (not landed or not measured)' % key)
        return None
    im = Image.open(src).convert('RGBA')
    mouth, floor, wall = CAVITY[key]
    rows = silhouette(im)
    fill = Image.new('RGBA', im.size, (0, 0, 0, 0))
    fp = fill.load()
    gp = im.load()
    widest = 0
    for y in range(mouth, floor):
        if y not in rows:
            continue
        x0, x1 = rows[y]
        x0 += wall; x1 -= wall
        if x1 < x0:
            continue
        widest = max(widest, x1 - x0 + 1)
        for x in range(x0, x1 + 1):
            fp[x, y] = (255, 255, 255, 255)
            r, g, b, a = gp[x, y]
            gp[x, y] = (r, g, b, min(a, GLASS_ALPHA))
    if '--dry' not in sys.argv:
        os.makedirs(ITEMS, exist_ok=True)
        im.save(os.path.join(ITEMS, 'glass3d_%s.png' % key))
        fill.save(os.path.join(ITEMS, 'glass3d_%s_fill.png' % key))
    # GlassArt.Gen3D wants fractions of the sprite rect, measured from the BOTTOM.
    h = im.height
    floor_y = (h - floor) / h
    rim_y = (h - mouth) / h
    interior_half = widest / im.width
    print('  %-9s shipped %dx%d  Gen3D(%.3ff, %.3ff, %.3ff, density)' % (key, im.width, h, floor_y, rim_y, interior_half))
    preview_cells.append((key, im, fill))
    return (floor_y, rim_y, interior_half)


def main():
    cells = []
    out = {}
    for key in CAVITY:
        got = ship(key, cells)
        if got:
            out[key] = got
    io.open(os.path.join(HERE, 'glass3d_gen3d.json'), 'w', encoding='utf-8').write(json.dumps(out, indent=1))
    if 'preview' in sys.argv and cells:
        k = 4
        W = sum(im.width * k * 2 + 16 for _, im, _ in cells) + 8
        H = max(im.height for _, im, _ in cells) * k + 16
        sheet = Image.new('RGBA', (W, H), (36, 24, 48, 255))
        x = 8
        for key, im, fill in cells:
            # the glass over a pink pool so the translucent cavity shows what it does
            pool = Image.new('RGBA', im.size, (0, 0, 0, 0))
            pp = pool.load(); fp = fill.load()
            for yy in range(im.height):
                for xx in range(im.width):
                    if fp[xx, yy][3] and yy > im.height * 0.45:
                        pp[xx, yy] = (0xE8, 0x4D, 0xA6, 255)
            over = Image.alpha_composite(pool, im)
            sheet.alpha_composite(im.resize((im.width * k, im.height * k), Image.NEAREST), (x, 8))
            x += im.width * k + 8
            sheet.alpha_composite(over.resize((im.width * k, im.height * k), Image.NEAREST), (x, 8))
            x += im.width * k + 8
        p = os.path.join(HERE, 'glass3d_ship_preview.png')
        sheet.save(p)
        print('preview', p)


if __name__ == '__main__':
    main()
