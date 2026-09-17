# -*- coding: utf-8 -*-
"""THE BENCH'S ART, HANDED TO THE AUTHOR (2026-09-16, the author: "Built ekranında kullanılan tezgahı aseprite'da
editleyeceğim oyunda kullanılan boyunda o görseli png olarak ver, aynısını ana sahnede kullandığımız ... shakeri da ver").

Writes, into Desktop/konsept art/export/bench/:
  bench_counter_tile_320x96.png        the counter's grain tile as the code draws it (ChromeArt.Counter, exported from
                                       the editor into Tools/bench_export_tile.png first — see Tools/bench_probe)
  bench_counter_native_320x122.png     the whole bar top the shaker bench shows, at the art's own scale (1 px = 4 units):
                                       the far edge (ridge, seam, six rails, seam) over the tiled grain — the bench's
                                       Band() rows are 8/5/5x6/5 units, i.e. 2/1.25/1.25.. px, so this is the drawing the
                                       game's 4x zoom is a zoom OF; edit THIS one
  bench_counter_ingame_1280x486.png    the same at exactly the size it stands on a 1280x720 screen (4x), for reference
  shaker_prop_48x48.png + _t2          the tin standing on the room's counter (ItemArt "shaker_prop"), shown at 2x (96)
  tin_open_116x208.png (+_Front, caps) the bench's own tin, its front plate and lids, shown at 2x (232x416)
  bench_spoon_32x128.png               the bar spoon, shown at 2x (64x256)
  gauge_tin_outline/solid_136x300.png  the standing measure's silhouette (exported from the editor like the tile)
  README.txt                           what each file is and how big it is drawn in the game

    py -3 -X utf8 Tools/bench_export.py
"""
import os
import shutil

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
OUT = os.path.join(os.path.expanduser('~'), 'Desktop', 'konsept art', 'export', 'bench')
TILE = os.path.join(HERE, 'bench_export_tile.png')          # ChromeArt.Counter(320, 96, Slate), written by the editor
OUTLINE = os.path.join(HERE, 'bench_export_gauge_outline.png')
SOLID = os.path.join(HERE, 'bench_export_gauge_solid.png')
WEAR = os.path.join(HERE, 'bench_export_wear.png')           # ChromeArt.CounterWear(320, 122), written by the editor

# the bench's far edge, in UNITS (TycoonServiceFlow.Shaker.AddBenchCounter): one art pixel is four units
RIDGE = (0x31, 0x2E, 0x3A); SEAM = (0x17, 0x14, 0x1C)
RAIL = [(0xD7, 0x7B, 0xBA), (0xB7, 0x69, 0x9F), (0x97, 0x58, 0x85), (0x77, 0x47, 0x6B), (0x57, 0x36, 0x50), (0x37, 0x25, 0x36)]
BANDS_UNITS = [('ridge', 8, RIDGE), ('seam', 5, SEAM)] + [('rail%d' % i, 5, c) for i, c in enumerate(RAIL)] + [('seam2', 5, SEAM)]
BAND_H = 486                       # the band's height on a 1280x720 screen: 0.675 of 720
SHEEN_FROM, SHEEN_TO = 62, 74      # the sheen band's units under the far edge's foot, white at 4.5%


def counter(scale):
    """The bar top at scale (1 = native art pixels, 4 = the screen)."""
    tile = Image.open(TILE).convert('RGBA')
    w, h = 1280 * scale // 4, BAND_H * scale // 4
    im = Image.new('RGBA', (w, h))
    tw, th = tile.size
    t = tile if scale == 1 else tile.resize((tw * scale // 4 * 4 // 4 * 1, th * scale // 4 * 4 // 4 * 1), Image.NEAREST) if False else tile.resize((tw * scale, th * scale), Image.NEAREST)
    # the grain is tiled at 4x on screen: one tile pixel = 4 units, so at scale s a tile pixel is s px
    if scale != 1:
        t = tile.resize((tw * scale, th * scale), Image.NEAREST)
        tw, th = t.size
    for y in range(0, h, th):
        for x in range(0, w, tw):
            im.alpha_composite(t, (x, y))
    # the far edge, from the top down; units to pixels at this scale (a 5-unit band is 1.25 px native: rounded)
    y = 0.0
    for name, units, col in BANDS_UNITS:
        px = units * scale / 4.0
        y0, y1 = int(round(y)), int(round(y + px))
        if y1 <= y0:
            y1 = y0 + 1
        for yy in range(y0, min(h, y1)):
            for xx in range(w):
                im.putpixel((xx, yy), col + (255,))
        y += px
    foot = y
    s0, s1 = int(round(foot + SHEEN_FROM * scale / 4.0)), int(round(foot + SHEEN_TO * scale / 4.0))
    for yy in range(s0, min(h, s1)):
        for xx in range(w):
            r, g, b, a = im.getpixel((xx, yy))
            im.putpixel((xx, yy), (min(255, r + 12), min(255, g + 12), min(255, b + 12), 255))
    # THE FALL OF LIGHT AND THE WEAR (2026-09-17): the counter is lit under the rail and falls away toward the
    # player, and the work's rings and scratches lie over the whole band at once (never tiled).
    for yy in range(h):
        t = yy / float(max(1, h - 1))
        k = 1.0 - 0.34 * (t * t)
        for xx in range(w):
            r, g, b, a = im.getpixel((xx, yy))
            im.putpixel((xx, yy), (int(r * k), int(g * k), int(b * k), a))
    if os.path.exists(WEAR):
        wear = Image.open(WEAR).convert('RGBA').resize((w, h), Image.NEAREST)
        im.alpha_composite(wear)
    return im


def main():
    os.makedirs(OUT, exist_ok=True)
    notes = []
    if os.path.exists(TILE):
        shutil.copyfile(TILE, os.path.join(OUT, 'bench_counter_tile_320x96.png'))
        counter(1).save(os.path.join(OUT, 'bench_counter_native_320x122.png'))
        counter(4).save(os.path.join(OUT, 'bench_counter_ingame_1280x486.png'))
        notes.append('bench_counter_tile_320x96.png     the grain tile the code tiles at 4x (one tile pixel = 4 screen units)')
        notes.append('bench_counter_native_320x122.png  THE ONE TO EDIT: the whole bar top at the art scale (far edge + grain + sheen)')
        notes.append('bench_counter_ingame_1280x486.png the same at screen size on 1280x720 (4x), reference only')
        notes.append('  -> the game will load Assets/Resources/Items/bench_counter.png if it exists (any width; tiled at 4x, height 122 fills the band)')
    else:
        notes.append('(counter tile not exported yet: run the bench probe in the editor first)')
    for name, out, scale in [('shaker_prop', 'shaker_prop_48x48.png', 2), ('shaker_prop_t2', 'shaker_prop_t2_48x48.png', 2),
                             ('tin_open', 'tin_open_116x208.png', 2), ('tin_open_Front', 'tin_open_Front_82x124.png', 2),
                             ('tin_open_t2', 'tin_open_t2_116x208.png', 2), ('shaker_cap', 'shaker_cap_116x208.png', 2),
                             ('shaker_cap_pour', 'shaker_cap_pour_116x208.png', 2), ('bench_spoon', 'bench_spoon_32x128.png', 2)]:
        src = os.path.join(ITEMS, name + '.png')
        if not os.path.exists(src):
            continue
        shutil.copyfile(src, os.path.join(OUT, out))
        im = Image.open(src)
        notes.append('%-34s native %dx%d, drawn in the game at %dx (%dx%d)' % (out, im.width, im.height, scale, im.width * scale, im.height * scale))
    for src, out in [(OUTLINE, 'gauge_tin_outline_136x300.png'), (SOLID, 'gauge_tin_solid_136x300.png')]:
        if os.path.exists(src):
            shutil.copyfile(src, os.path.join(OUT, out))
            notes.append('%-34s the standing measure on both benches, drawn at 1x; drop an edited pair into Items as gauge_tin.png / gauge_tin_solid.png' % out)
    with open(os.path.join(OUT, 'README.txt'), 'w', encoding='utf-8') as f:
        f.write('LAST CALL - bench art export (2026-09-16)\n\n' + '\n'.join(notes) + '\n\n'
                'Edit at the native size and keep the canvas size; the game scales by whole numbers (2x, 4x).\n')
    print('exported to', OUT)
    print('\n'.join(notes))


if __name__ == '__main__':
    main()
