# -*- coding: utf-8 -*-
"""THE MENU'S BUTTON PACK AND KEY CAPS (2026-09-15, the author: "KEybind için ...\\konsept art\\Classic burdaki
dosyalardaki görselleri kullan animasyonlu olduğundan 2 frame olabilir. Butonlar içinde bu dosya yolundaki butonları
kullan ...\\konsept art\\buton").

Two packs the author dropped on the Desktop, brought into Resources by this script and by nothing else:

  buton/    ten 80x96 sheets of thirty 16x16 icon buttons, five across and six down — grey, orange and green, each in
            a resting, a lit (brighter glyph) and a pressed (frame one pixel down, shadow one row thinner) state.
            Copied as they are to Resources/Menu/pack_<colour>_<state>.png; the game slices a cell at run time.
            Derived here, because the pause menu's keys are WORDS and the pack has none:
              pack_<colour>_blank.png / pack_<colour>_blank_pressed.png   one cell with its glyph painted over in
                                                                          the face colour — 9-sliced at 4, it is the
                                                                          plate a worded key stands on
              pack_glyphs.png                                             every glyph as a white mask (primary tone
                                                                          alpha 255, secondary 128) in the pack's own
                                                                          cell order, plus a seventh row of two the
                                                                          player needs and the pack lacks — the song
                                                                          before and after, built from its own play
  Classic/  key caps, 17x16 a frame, two frames a sheet (up, and pressed); a few keys wider. The Dark set is copied to
            Resources/Keys/<NAME>.png, ENTER (31 tall, L-shaped) and the ALTERNATIVE twins left behind. A key the
            pack has no cap for is drawn on EMPTY1 (one letter) or EMPTY2 (a word) with the word printed on it.

The packs keep their own palettes (GDD 16's palette-token rule bends here on the author's word, the way the flags and
the bottles keep theirs); everything the game draws around them stays in the palette.

    py -3 -X utf8 Tools/menu_pack.py        copy and derive; prints the pack's palette for MenuPack.cs
"""
import os
import shutil

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SRC_BUTTONS = os.path.join(os.path.expanduser('~'), 'Desktop', 'konsept art', 'buton')
SRC_CAPS = os.path.join(os.path.expanduser('~'), 'Desktop', 'konsept art', 'Classic', 'Dark', 'Spritesheets')
OUT_MENU = os.path.join(ROOT, 'Assets', 'Resources', 'Menu')
OUT_KEYS = os.path.join(ROOT, 'Assets', 'Resources', 'Keys')

SHEETS = {
    'grey_normal': 'UI_grey_buttons_1.png',
    'grey_light': 'UI_grey_buttons_light_1.png',
    'grey_light_pressed': 'UI_grey_buttons_light_pressed_1.png',
    'orange_normal': 'UI_orange_buttons_3.png',
    'orange_light': 'UI_orange_buttons_light_3.png',
    'orange_light_pressed': 'UI_orange_buttons_light_pressed_3.png',
    'orange_pressed': 'UI_orange_buttons_pressed_3.png',
    'green_normal': 'UI_green_buttons_4.png',
    'green_light_pressed': 'UI_green_buttons_light_pressed_4.png',
    'green_pressed': 'UI_green_buttons_pressed_4.png',
}

# the pack's cells, read off the sheet (row-major, five a row)
ICONS = [
    'exit', 'tent', 'play', 'home', 'stats',
    'back', 'pause', 'menu', 'music', 'gamepad',
    'expand', 'sound_on', 'sound_off', 'cog', 'save',
    'restart', 'close', 'trophy', 'mail', 'heart_line',
    'check', 'trash', 'chevron_down', 'chevron_up', 'heart',
    'plus', 'minus', 'lock', 'unlock', 'info',
]
CELL = 16
INNER = (2, 3, 14, 12)      # x0, y0, x1, y1 (exclusive): where a glyph can be, inside the rim, on a resting cell

# caps that come across: every 17-wide key, and the wide ones that stay 16 tall
CAPS_SKIP = {'ENTER', 'ENTERALTERNATIVE', 'BACKSPACEALTERNATIVE', 'SHIFTALTERNATIVE', 'SPACEALTERNATIVE', 'TABALTERNATIVE'}


def cell(im, index):
    r, c = divmod(index, 5)
    return im.crop((c * CELL, r * CELL, c * CELL + CELL, r * CELL + CELL))


def anatomy(im):
    """The face, the rim highlight, the shadow, and the two glyph tones of a sheet, from its cells: the face is the
    commonest opaque colour, the shadow the colour of row 13, the highlight the colour at (2,1); the glyph tones are
    what is left inside the inner area, the primary being the one the 'expand' glyph's diagonals use."""
    counts = {}
    for i in range(30):
        for px in cell(im, i).getdata():
            if px[3]:
                counts[px] = counts.get(px, 0) + 1
    face = max(counts, key=counts.get)
    c0 = cell(im, 0)
    highlight = c0.getpixel((2, 1))
    shadow = c0.getpixel((8, 13))
    outline = c0.getpixel((8, 0))
    expand = cell(im, ICONS.index('expand'))
    primary = expand.getpixel((3, 3))          # the diagonal's first pixel
    tones = set()
    for i in range(30):
        cl = cell(im, i)
        for y in range(INNER[1], INNER[3]):
            for x in range(INNER[0], INNER[2]):
                px = cl.getpixel((x, y))
                if px[3] and px != face:
                    tones.add(px)
    secondary = [t for t in tones if t != primary]
    return dict(face=face, highlight=highlight, shadow=shadow, outline=outline, primary=primary,
                secondary=secondary[0] if len(secondary) == 1 else secondary)


def blank(im, pressed):
    """One cell with the glyph painted over in the face colour. A pressed sheet's frame sits one row lower."""
    a = anatomy(im)
    cl = cell(im, ICONS.index('info')).copy()
    x0, y0, x1, y1 = INNER
    if pressed:
        y0, y1 = y0 + 1, y1 + 1
    for y in range(y0, y1):
        for x in range(x0, x1):
            cl.putpixel((x, y), a['face'])
    return cl


def glyph_mask(im, index, pressed=False):
    a = anatomy(im)
    cl = cell(im, index)
    out = Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0))
    x0, y0, x1, y1 = INNER
    if pressed:
        y0, y1 = y0 + 1, y1 + 1
    for y in range(y0, y1):
        for x in range(x0, x1):
            px = cl.getpixel((x, y))
            if px[3] and px != a['face']:
                out.putpixel((x, y), (255, 255, 255, 255 if px == a['primary'] else 128))
    return out


def derived_glyphs(play):
    """The song before and after: the pack's play triangle with a bar on the far side, and its mirror."""
    bb = play.getbbox()
    tri = play.crop(bb)
    w, h = tri.size
    nxt = Image.new('RGBA', (CELL, CELL), (0, 0, 0, 0))
    x = bb[0] - 1
    nxt.alpha_composite(tri, (x, bb[1]))
    for y in range(bb[1], bb[3]):
        for bx in (x + w + 1, x + w + 2):
            nxt.putpixel((bx, y), (255, 255, 255, 255))
    prv = nxt.transpose(Image.FLIP_LEFT_RIGHT)
    return prv, nxt


def main():
    os.makedirs(OUT_MENU, exist_ok=True)
    os.makedirs(OUT_KEYS, exist_ok=True)
    sheets = {}
    for name, fn in SHEETS.items():
        im = Image.open(os.path.join(SRC_BUTTONS, fn)).convert('RGBA')
        assert im.size == (80, 96), (fn, im.size)
        im.save(os.path.join(OUT_MENU, 'pack_%s.png' % name))
        sheets[name] = im
    for colour in ('grey', 'orange', 'green'):
        a = anatomy(sheets[colour + '_normal'])
        lit = anatomy(sheets[colour + '_light' if colour + '_light' in sheets else colour + '_light_pressed'])
        print('%-7s face %s  primary %s  secondary %s  lit primary %s  lit secondary %s  shadow %s  highlight %s'
              % (colour, a['face'][:3], a['primary'][:3], a['secondary'][:3] if isinstance(a['secondary'], tuple) else a['secondary'],
                 lit['primary'][:3], lit['secondary'][:3] if isinstance(lit['secondary'], tuple) else lit['secondary'],
                 a['shadow'][:3], a['highlight'][:3]))
        blank(sheets[colour + '_normal'], False).save(os.path.join(OUT_MENU, 'pack_%s_blank.png' % colour))
        blank(sheets[colour + '_light_pressed'], True).save(os.path.join(OUT_MENU, 'pack_%s_blank_pressed.png' % colour))

    # the glyph masks: the pack's six rows, and a seventh with the two the player lacks
    masks = Image.new('RGBA', (80, 112), (0, 0, 0, 0))
    grey = sheets['grey_normal']
    for i in range(30):
        r, c = divmod(i, 5)
        masks.alpha_composite(glyph_mask(grey, i), (c * CELL, r * CELL))
    prv, nxt = derived_glyphs(glyph_mask(grey, ICONS.index('play')))
    masks.alpha_composite(prv, (0, 6 * CELL))
    masks.alpha_composite(nxt, (1 * CELL, 6 * CELL))
    masks.save(os.path.join(OUT_MENU, 'pack_glyphs.png'))

    copied = 0
    for fn in sorted(os.listdir(SRC_CAPS)):
        name = os.path.splitext(fn)[0]
        if name in CAPS_SKIP:
            continue
        im = Image.open(os.path.join(SRC_CAPS, fn))
        assert im.size[1] == 16 and im.size[0] % 2 == 0, (fn, im.size)
        shutil.copyfile(os.path.join(SRC_CAPS, fn), os.path.join(OUT_KEYS, fn))
        copied += 1
    cap = Image.open(os.path.join(SRC_CAPS, 'B.png')).convert('RGBA')
    print('caps copied: %d   cap palette: %s' % (copied, sorted(set(px for px in cap.getdata() if px[3]), key=sum)))
    print('menu pack at', OUT_MENU)


if __name__ == '__main__':
    main()
