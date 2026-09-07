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

# id: (mouth_centre, mouth_half, floor_centre, floor_half, wall_px) — rows from the top, in
# the sprite's own pixels. THE CAVITY IS NOT A BOX (2026-09-06, the author: "yeni bardak
# görsellerinin içerisinde kutu şeklinde boşluk var bu gerçekçi hissiyatı azaltıyor"): the
# glass is seen a little from above, so the mouth and the floor are ELLIPSES. The cavity's top
# edge is the mouth's FAR arc (highest in the middle, down to the mouth's centre row at the
# walls) and its bottom edge the floor's NEAR arc (lowest in the middle), inset `wall_px`
# from the silhouette on every row in between. A drink poured to any level then shows the
# curve of the glass it sits in instead of a flat-topped rectangle.
CAVITY = {
    'rocks':    (13.5, 3.5, 49.5, 2.5, 5),
    'pint':     (14.5, 4.5, 74.0, 4.0, 5),
    'highball': (16.0, 6.0, 78.0, 4.0, 4),
    'martini':  (14.0, 6.0, 41.0, 1.0, 4),
    'coupe':    (15.5, 5.5, 39.0, 1.5, 4),
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
    mouth_c, mouth_b, floor_c, floor_b, wall = CAVITY[key]
    rows = silhouette(im)
    fill = Image.new('RGBA', im.size, (0, 0, 0, 0))
    fp = fill.load()
    gp = im.load()
    widest = 0
    mouth = int(round(mouth_c - mouth_b))          # the far arc's crown: the cavity's top row
    floor = int(round(floor_c + floor_b)) + 1      # the near arc's foot: one past the last row
    # the mouth's half-width at its centre row, for the arcs' x extent
    mr = rows.get(int(round(mouth_c))) or rows.get(mouth)
    fr = rows.get(int(round(floor_c))) or rows.get(floor - 1)
    for y in range(mouth, floor):
        if y not in rows:
            continue
        x0, x1 = rows[y]
        x0 += wall; x1 -= wall
        if x1 < x0:
            continue
        for x in range(x0, x1 + 1):
            # inside the mouth's far arc? (an ellipse centred on mouth_c, the cavity is BELOW it
            # in the middle and reaches mouth_c at the walls)
            if mr is not None:
                a = max(1.0, (mr[1] - mr[0]) * 0.5 - wall + 0.5)
                u = (x - (mr[0] + mr[1]) * 0.5) / a
                top = mouth_c - mouth_b * (1.0 - u * u) ** 0.5 if abs(u) <= 1.0 else mouth_c
                if y < top:
                    continue
            # above the floor's near arc? (lowest in the middle)
            if fr is not None:
                a = max(1.0, (fr[1] - fr[0]) * 0.5 - wall + 0.5)
                u = (x - (fr[0] + fr[1]) * 0.5) / a
                bottom = floor_c + floor_b * (1.0 - u * u) ** 0.5 if abs(u) <= 1.0 else floor_c
                if y > bottom:
                    continue
            fp[x, y] = (255, 255, 255, 255)
            r, g, b, al = gp[x, y]
            gp[x, y] = (r, g, b, min(al, GLASS_ALPHA))
        widest = max(widest, x1 - x0 + 1)
    # CENTRE THE CANVAS ON THE CAVITY (2026-09-07, the author: "bardakların dolum bölgeleri
    # bozuk kaymalar var"). The generator draws where it likes on its canvas — the highball
    # sat in columns 0..33 of 44 — and every stage centres the drink on the RECT, so the pool
    # stood five art pixels right of the glass. The canvas is cut (or padded) so the cavity's
    # centre is the canvas's centre, one clear column each side of the silhouette; the fill
    # mask rides the same box, and the tier dress is cut with it (ninth_art_gen.py, 'crop').
    fb = fill.getbbox()
    sb = im.getbbox()
    cav_cx = (fb[0] + fb[2] - 1) / 2.0
    half = max(cav_cx - sb[0], (sb[2] - 1) - cav_cx) + 1.0
    left = int(round(cav_cx - half))
    right = int(round(cav_cx + half)) + 1
    if left < 0 or right > im.width:
        pad_l = max(0, -left)
        pad_r = max(0, right - im.width)
        wide = Image.new('RGBA', (im.width + pad_l + pad_r, im.height), (0, 0, 0, 0))
        wide.alpha_composite(im, (pad_l, 0))
        widef = Image.new('RGBA', wide.size, (0, 0, 0, 0))
        widef.alpha_composite(fill, (pad_l, 0))
        im, fill = wide, widef
        left += pad_l
        right += pad_l
    crop = (left, 0, right, im.height)
    im = im.crop(crop)
    fill = fill.crop(crop)
    if '--dry' not in sys.argv:
        os.makedirs(ITEMS, exist_ok=True)
        im.save(os.path.join(ITEMS, 'glass3d_%s.png' % key))
        fill.save(os.path.join(ITEMS, 'glass3d_%s_fill.png' % key))
    # GlassArt.Gen3D wants fractions of the sprite rect, measured from the BOTTOM.
    h = im.height
    floor_y = (h - floor) / h
    rim_y = (h - mouth) / h
    interior_half = widest / im.width
    floor_arc = floor_b / h          # how much higher the floor stands at the walls than mid-glass
    print('  %-9s shipped %dx%d  Gen3D(%.3ff, %.3ff, %.3ff, %.3ff, density)  crop %s' % (
        key, im.width, h, floor_y, rim_y, interior_half, floor_arc, crop))
    preview_cells.append((key, im, fill))
    return {'floor_y': floor_y, 'rim_y': rim_y, 'interior_half': interior_half,
            'floor_arc': floor_arc, 'crop': [crop[0], crop[2]], 'size': [im.width, h]}


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
