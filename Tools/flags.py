# -*- coding: utf-8 -*-
"""THE LICENCE'S FLAGS (2026-09-07). Every country in Assets/Data/customers/roster.json needs
a 16x11 flag under Resources/Items/fl_<iso>.png, because that is what the licence prints in
its seal. They are drawn here rather than downloaded: sixteen pixels wide is a flag reduced to
its geometry, and a resampled photograph of one comes out as mud at this size.

    py -3 -X utf8 Tools/flags.py            draw whatever the roster needs and is missing
    py -3 -X utf8 Tools/flags.py all        redraw every flag this file knows
    py -3 -X utf8 Tools/flags.py sheet      a 6x contact sheet of them

The palette is the flag's own, one step softened: a licence is printed stock, not a screen.
"""
import io
import json
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
ROSTER = os.path.join(ROOT, 'Assets', 'Data', 'customers', 'roster.json')
W, H = 16, 11

# a printed-stock palette: nothing at full screen chroma
RED = (206, 43, 55); DEEPRED = (172, 32, 44); WHITE = (244, 242, 236); BLACK = (38, 34, 40)
BLUE = (44, 74, 150); NAVY = (32, 52, 108); SKY = (86, 148, 206); GREEN = (46, 132, 82)
DARKGREEN = (28, 96, 62); YELLOW = (232, 190, 66); GOLD = (206, 160, 48); ORANGE = (226, 128, 52)
SAFFRON = (232, 150, 62)


def bands(colors, horizontal=True):
    """Equal stripes, the flag's commonest shape."""
    def draw(d, im):
        n = len(colors)
        for i, c in enumerate(colors):
            if horizontal:
                y0, y1 = round(i * H / n), round((i + 1) * H / n) - 1
                d.rectangle([0, y0, W - 1, y1], fill=c)
            else:
                x0, x1 = round(i * W / n), round((i + 1) * W / n) - 1
                d.rectangle([x0, 0, x1, H - 1], fill=c)
    return draw


def disc(base, colour, cx=None, cy=None, r=3):
    def draw(d, im):
        base(d, im)
        x, y = (W - 1) / 2 if cx is None else cx, (H - 1) / 2 if cy is None else cy
        d.ellipse([x - r, y - r, x + r, y + r], fill=colour)
    return draw


def canton(base, field, w=7, h=6, star=None):
    """A block in the upper hoist, with an optional light in it."""
    def draw(d, im):
        base(d, im)
        d.rectangle([0, 0, w - 1, h - 1], fill=field)
        if star:
            d.point((2, 2), fill=star)
            d.point((4, 3), fill=star)
    return draw


def cross(base, colour, x=5, y=5):
    def draw(d, im):
        base(d, im)
        d.rectangle([x, 0, x + 1, H - 1], fill=colour)
        d.rectangle([0, y, W - 1, y + 1], fill=colour)
    return draw


FLAGS = {
    # already drawn, kept so `all` can redraw the set from one place
    'jp': disc(bands([WHITE]), RED, r=2.6),
    'us': None, 'de': None, 'it': None, 'tr': None, 'nl': None,
    'se': None, 'pl': None, 'gb': None, 'ph': None,

    # the twelfth list's countries
    'fr': bands([BLUE, WHITE, RED], horizontal=False),
    'es': bands([DEEPRED, YELLOW, YELLOW, DEEPRED]),
    'pt': (lambda base=bands([DARKGREEN, DARKGREEN, RED, RED, RED], horizontal=False):
           disc(base, YELLOW, cx=5, r=2.2))(),
    'ie': bands([DARKGREEN, WHITE, ORANGE], horizontal=False),
    'ro': bands([NAVY, YELLOW, RED], horizontal=False),
    'no': cross(bands([RED]), WHITE, x=4, y=4),
    'dk': cross(bands([RED]), WHITE, x=4, y=4),
    'fi': cross(bands([WHITE]), BLUE, x=4, y=4),
    'cn': canton(bands([RED]), RED, w=0, h=0, star=None),
    'kr': None,   # drawn below: the taegeuk needs two colours, which disc() cannot say
    'in': (lambda base=bands([SAFFRON, WHITE, DARKGREEN]): disc(base, NAVY, r=1.4))(),
    'th': bands([RED, WHITE, NAVY, NAVY, WHITE, RED]),
    'id': bands([RED, WHITE]),
    'au': canton(bands([NAVY]), NAVY, w=8, h=5, star=WHITE),
    'lb': (lambda base=bands([RED, WHITE, WHITE, RED]): disc(base, DARKGREEN, r=1.6))(),
    'eg': bands([RED, WHITE, BLACK]),
    'ae': canton(bands([GREEN, WHITE, BLACK]), RED, w=4, h=H),
    'jo': canton(bands([BLACK, WHITE, DARKGREEN]), RED, w=5, h=H),
    'ma': (lambda base=bands([DEEPRED]): disc(base, DARKGREEN, r=2.4))(),
    'ng': bands([DARKGREEN, WHITE, DARKGREEN], horizontal=False),
    'gh': (lambda base=bands([RED, YELLOW, DARKGREEN]): disc(base, BLACK, r=1.6))(),
    'ke': bands([BLACK, RED, DARKGREEN]),
    'za': bands([DARKGREEN, WHITE, GOLD]),
}

# Korea's taegeuk: red over blue in one circle, and the four trigrams as corner marks.
def _kr(d, im):
    d.rectangle([0, 0, W - 1, H - 1], fill=WHITE)
    cx, cy, r = (W - 1) / 2, (H - 1) / 2, 2.6
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=RED)
    d.chord([cx - r, cy - r, cx + r, cy + r], 0, 180, fill=NAVY)
    for x, y in ((1, 1), (1, H - 2), (W - 2, 1), (W - 2, H - 2)):
        d.point((x, y), fill=BLACK)
FLAGS['kr'] = _kr


# the Chinese flag wants its stars, which bands cannot say
def _cn(d, im):
    d.rectangle([0, 0, W - 1, H - 1], fill=RED)
    d.rectangle([1, 2, 3, 4], fill=YELLOW)
    for p in ((5, 1), (6, 3), (5, 5)):
        d.point(p, fill=YELLOW)
FLAGS['cn'] = _cn


def draw(iso):
    fn = FLAGS.get(iso)
    if fn is None:
        return False
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    fn(d, im)
    im.save(os.path.join(ITEMS, 'fl_%s.png' % iso))
    return True


def needed():
    roster = json.load(io.open(ROSTER, encoding='utf-8'))['people']
    return sorted({r['iso'] for r in roster})


def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'missing'
    if cmd == 'sheet':
        isos = [i for i in needed() if os.path.exists(os.path.join(ITEMS, 'fl_%s.png' % i))]
        k = 6
        cols = 8
        rows = (len(isos) + cols - 1) // cols
        sheet = Image.new('RGBA', (cols * (W * k + 8) + 8, rows * (H * k + 20) + 8), (36, 24, 48, 255))
        for i, iso in enumerate(isos):
            im = Image.open(os.path.join(ITEMS, 'fl_%s.png' % iso)).convert('RGBA')
            sheet.alpha_composite(im.resize((W * k, H * k), Image.NEAREST),
                                  (8 + (i % cols) * (W * k + 8), 8 + (i // cols) * (H * k + 20)))
        p = os.path.join(HERE, 'flags_preview.png')
        sheet.save(p)
        print('sheet', p, len(isos), 'flags')
        return
    made, skipped = [], []
    for iso in needed():
        path = os.path.join(ITEMS, 'fl_%s.png' % iso)
        if cmd != 'all' and os.path.exists(path):
            continue
        (made if draw(iso) else skipped).append(iso)
    print('drew:', ' '.join(made) or '-')
    if skipped:
        print('NO DESIGN for:', ' '.join(skipped))


if __name__ == '__main__':
    main()
