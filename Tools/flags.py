# -*- coding: utf-8 -*-
"""THE LICENCE'S FLAGS (2026-09-07, redrawn at 48x33 after the author: "ulke bayraklari ise
yamuk ve dogru degil, ulke bayraklari kesin ve dogru olmali, boyutu daha yuksek olabilir dusuk
cozunurluktense").

The first set was 16x11 shown at four times, which is a flag whose every edge is four pixels
wide and whose crosses and discs land wherever the rounding puts them - the "yamuk" the author
is looking at. These are 48x33 shown at 1.3x, so the geometry is drawn at the size it is read
and the proportions are the flag's own: a Nordic cross at its real offset, a canton at its
real fraction, a disc at its real diameter.

    py -3 -X utf8 Tools/flags.py            draw whatever the roster needs and is missing
    py -3 -X utf8 Tools/flags.py all        redraw every flag this file knows
    py -3 -X utf8 Tools/flags.py sheet      a contact sheet of them

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
W, H = 48, 33          # 3x the old rig: a flag drawn at the size the seal shows it

# a printed-stock palette: nothing at full screen chroma
RED = (206, 43, 55); DEEPRED = (172, 32, 44); WHITE = (244, 242, 236); BLACK = (38, 34, 40)
BLUE = (44, 74, 150); NAVY = (26, 44, 96); SKY = (86, 148, 206); GREEN = (46, 132, 82)
DARKGREEN = (24, 88, 56); YELLOW = (232, 190, 66); GOLD = (206, 160, 48); ORANGE = (226, 128, 52)
SAFFRON = (232, 150, 62); MAROON = (128, 32, 44)


def bands(colors, horizontal=True, weights=None):
    """Stripes, equal unless weighted. The commonest flag there is."""
    def draw(d, im):
        ws = weights or [1] * len(colors)
        total = float(sum(ws))
        span = H if horizontal else W
        at = 0.0
        for c, w in zip(colors, ws):
            a, b = round(at * span / total), round((at + w) * span / total) - 1
            d.rectangle([0, a, W - 1, b] if horizontal else [a, 0, b, H - 1], fill=c)
            at += w
    return draw


def over(base, *layers):
    def draw(d, im):
        base(d, im)
        for fn in layers:
            fn(d, im)
    return draw


def disc(colour, cx=None, cy=None, r=None):
    """A circle at a real fraction of the flag, not a rounded blob."""
    def draw(d, im):
        x = (W - 1) / 2.0 if cx is None else cx * W
        y = (H - 1) / 2.0 if cy is None else cy * H
        rr = (H * 0.30) if r is None else r * H
        d.ellipse([x - rr, y - rr, x + rr, y + rr], fill=colour)
    return draw


def nordic(field, cross, edge=None):
    """A Nordic cross: the bar at two fifths across, the arms a seventh of the height, with an
    optional outline stripe - which is what tells Norway and Iceland from Denmark."""
    def draw(d, im):
        d.rectangle([0, 0, W - 1, H - 1], fill=field)
        vx, vw = round(W * 0.30), max(2, round(H * 0.16))
        hy = round(H * 0.5 - vw / 2.0)
        if edge:
            pad = max(1, round(vw * 0.34))
            d.rectangle([vx - pad, 0, vx + vw - 1 + pad, H - 1], fill=edge)
            d.rectangle([0, hy - pad, W - 1, hy + vw - 1 + pad], fill=edge)
        d.rectangle([vx, 0, vx + vw - 1, H - 1], fill=cross)
        d.rectangle([0, hy, W - 1, hy + vw - 1], fill=cross)
    return draw


def canton(w, h, field, mark=None):
    """A block in the upper hoist at a real fraction of the flag."""
    def draw(d, im):
        d.rectangle([0, 0, round(W * w) - 1, round(H * h) - 1], fill=field)
        if mark:
            mark(d, im)
    return draw


def stars(points, colour, r=1.4):
    def draw(d, im):
        for fx, fy in points:
            x, y = fx * W, fy * H
            d.ellipse([x - r, y - r, x + r, y + r], fill=colour)
    return draw


def star(d, cx, cy, r, colour, points=5, rot=-90.0):
    """A real five-pointed star: the outer points on one circle, the inner on a smaller one.
    The disc stand-in the first set used is why Turkey's crescent had no star and America's
    canton read as a grid (2026-09-07)."""
    import math
    pts = []
    for k in range(points * 2):
        rr = r if k % 2 == 0 else r * 0.42
        a = math.radians(rot + k * 180.0 / points)
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    d.polygon(pts, fill=colour)


def union_jack(d, im):
    """The one flag that is all geometry: two saltires under a cross, on navy."""
    d.rectangle([0, 0, W - 1, H - 1], fill=NAVY)
    # the saltires, white then the red offset inside it
    for wide, colour in ((max(3, H // 6), WHITE), (max(1, H // 12), RED)):
        for x0, y0, x1, y1 in ((0, 0, W, H), (0, H, W, 0)):
            d.line([x0, y0, x1, y1], fill=colour, width=wide)
    # St George's cross over both
    vx, vw = round(W * 0.5 - H * 0.10), max(4, round(H * 0.20))
    hy, hh = round(H * 0.5 - H * 0.10), max(4, round(H * 0.20))
    d.rectangle([vx, 0, vx + vw - 1, H - 1], fill=WHITE)
    d.rectangle([0, hy, W - 1, hy + hh - 1], fill=WHITE)
    pad = max(1, vw // 3)
    d.rectangle([vx + pad, 0, vx + vw - 1 - pad, H - 1], fill=RED)
    d.rectangle([0, hy + pad, W - 1, hy + hh - 1 - pad], fill=RED)


def taegeuk(d, im):
    """Korea: red over blue in one circle, four trigram blocks at the corners."""
    d.rectangle([0, 0, W - 1, H - 1], fill=WHITE)
    cx, cy, r = (W - 1) / 2.0, (H - 1) / 2.0, H * 0.24
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=RED)
    d.chord([cx - r, cy - r, cx + r, cy + r], 0, 180, fill=NAVY)
    d.ellipse([cx - r / 2 - r / 2, cy - r / 2, cx - r / 2 + r / 2, cy + r / 2], fill=RED)
    d.ellipse([cx + r / 2 - r / 2, cy - r / 2, cx + r / 2 + r / 2, cy + r / 2], fill=NAVY)
    bw, bh, gap = max(4, W // 9), max(1, H // 16), max(1, H // 22)
    for fx, fy in ((0.14, 0.20), (0.14, 0.80), (0.86, 0.20), (0.86, 0.80)):
        x, y = fx * W - bw / 2, fy * H - (bh * 3 + gap * 2) / 2
        for k in range(3):
            d.rectangle([x, y + k * (bh + gap), x + bw, y + k * (bh + gap) + bh - 1], fill=BLACK)


def china(d, im):
    d.rectangle([0, 0, W - 1, H - 1], fill=RED)
    big = H * 0.13
    d.ellipse([W * 0.12 - big, H * 0.28 - big, W * 0.12 + big, H * 0.28 + big], fill=YELLOW)
    for fx, fy in ((0.26, 0.12), (0.32, 0.24), (0.32, 0.40), (0.26, 0.52)):
        r = H * 0.045
        d.ellipse([fx * W - r, fy * H - r, fx * W + r, fy * H + r], fill=YELLOW)


def maple(d, im):
    d.rectangle([0, 0, W - 1, H - 1], fill=WHITE)
    d.rectangle([0, 0, round(W * 0.25) - 1, H - 1], fill=RED)
    d.rectangle([W - round(W * 0.25), 0, W - 1, H - 1], fill=RED)
    cx, cy = W / 2.0, H / 2.0
    d.polygon([(cx, cy - H * 0.26), (cx + H * 0.10, cy - H * 0.04), (cx + H * 0.20, cy - H * 0.10),
               (cx + H * 0.14, cy + H * 0.14), (cx, cy + H * 0.10),
               (cx - H * 0.14, cy + H * 0.14), (cx - H * 0.20, cy - H * 0.10),
               (cx - H * 0.10, cy - H * 0.04)], fill=RED)
    d.rectangle([cx - 1, cy + H * 0.08, cx + 1, cy + H * 0.26], fill=RED)


def aus(d, im):
    """Australia: the jack in the canton, the Commonwealth star under it, the Cross at the fly."""
    d.rectangle([0, 0, W - 1, H - 1], fill=NAVY)
    jack = Image.new('RGBA', (W, H))
    union_jack(ImageDraw.Draw(jack), jack)
    im.paste(jack.resize((round(W * 0.5), round(H * 0.5)), Image.NEAREST), (0, 0))
    for fx, fy, r in ((0.25, 0.76, 3.0), (0.72, 0.22, 2.0), (0.80, 0.48, 2.2),
                      (0.71, 0.72, 2.0), (0.88, 0.62, 1.5), (0.76, 0.55, 1.1)):
        star(d, fx * W, fy * H, r, WHITE)


def india(d, im):
    bands([SAFFRON, WHITE, DARKGREEN])(d, im)
    r = H * 0.12
    cx, cy = W / 2.0, H / 2.0
    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=NAVY, width=2)
    for k in range(12):
        import math
        a = k * math.pi / 6
        d.line([cx, cy, cx + r * math.cos(a), cy + r * math.sin(a)], fill=NAVY, width=1)


def brazil(d, im):
    d.rectangle([0, 0, W - 1, H - 1], fill=DARKGREEN)
    cx, cy = W / 2.0, H / 2.0
    d.polygon([(cx, cy - H * 0.34), (cx + W * 0.40, cy), (cx, cy + H * 0.34), (cx - W * 0.40, cy)],
              fill=YELLOW)
    r = H * 0.17
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=NAVY)


FLAGS = {
    # The canton is nine rows of stars at this size; drawn as five offset rows of real stars,
    # which reads as the union rather than as a dot grid.
    'us': over(bands([RED, WHITE] * 6 + [RED]),
               canton(0.40, 0.54, NAVY),
               lambda d, im: [star(d, (0.035 + c * 0.077 + (0.038 if r % 2 else 0)) * W,
                                   (0.075 + r * 0.10) * H, 1.6, WHITE)
                              for r in range(5) for c in range(5 if r % 2 == 0 else 4)]),
    'gb': union_jack,
    'jp': over(bands([WHITE]), disc(RED, r=0.30)),
    'kr': taegeuk,
    'cn': china,
    'de': bands([BLACK, RED, GOLD]),
    'it': bands([DARKGREEN, WHITE, RED], horizontal=False),
    'fr': bands([BLUE, WHITE, RED], horizontal=False),
    'nl': bands([RED, WHITE, BLUE]),
    'es': bands([DEEPRED, YELLOW, DEEPRED], weights=[1, 2, 1]),
    'pt': over(bands([DARKGREEN, RED], horizontal=False, weights=[2, 3]), disc(YELLOW, cx=0.40, r=0.17)),
    'ie': bands([DARKGREEN, WHITE, ORANGE], horizontal=False),
    'ro': bands([NAVY, YELLOW, RED], horizontal=False),
    'pl': bands([WHITE, DEEPRED]),
    'se': nordic(BLUE, YELLOW),
    'no': nordic(RED, NAVY, edge=WHITE),
    'dk': nordic(RED, WHITE),
    'fi': nordic(WHITE, BLUE),
    'tr': over(bands([RED]), disc(WHITE, cx=0.34, r=0.22), disc(RED, cx=0.40, r=0.175),
               lambda d, im: star(d, W * 0.57, H * 0.5, H * 0.115, WHITE)),
    'au': aus,
    'in': india,
    'th': bands([RED, WHITE, NAVY, WHITE, RED], weights=[1, 1, 2, 1, 1]),
    'id': bands([RED, WHITE]),
    'ph': over(bands([BLUE, RED]), lambda d, im: d.polygon(
        [(0, 0), (round(W * 0.42), H // 2), (0, H - 1)], fill=WHITE)),
    'lb': over(bands([RED, WHITE, RED], weights=[1, 2, 1]), disc(DARKGREEN, r=0.16)),
    'eg': over(bands([RED, WHITE, BLACK]), disc(GOLD, r=0.09)),
    'ae': over(bands([DARKGREEN, WHITE, BLACK]), canton(0.26, 1.0, RED)),
    'jo': over(bands([BLACK, WHITE, DARKGREEN]),
               lambda d, im: d.polygon([(0, 0), (round(W * 0.36), H // 2), (0, H - 1)], fill=RED)),
    'ma': over(bands([DEEPRED]),
               lambda d, im: star(d, W / 2.0, H / 2.0, H * 0.24, DARKGREEN)),
    'ng': bands([DARKGREEN, WHITE, DARKGREEN], horizontal=False),
    'gh': over(bands([RED, YELLOW, DARKGREEN]),
               lambda d, im: star(d, W / 2.0, H / 2.0, H * 0.15, BLACK)),
    'ke': over(bands([BLACK, WHITE, RED, WHITE, DARKGREEN], weights=[5, 1, 5, 1, 5]),
               lambda d, im: d.polygon([(W // 2, H * 0.18), (W * 0.58, H * 0.5),
                                        (W // 2, H * 0.82), (W * 0.42, H * 0.5)], fill=DEEPRED)),
    'za': over(bands([RED, WHITE, DARKGREEN, WHITE, NAVY], weights=[4, 1, 4, 1, 4]),
               lambda d, im: d.polygon([(0, 0), (round(W * 0.34), H // 2), (0, H - 1)], fill=DARKGREEN)),
    'br': brazil,
    'ca': maple,
}


def draw(iso):
    fn = FLAGS.get(iso)
    if fn is None:
        return False
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    fn(ImageDraw.Draw(im), im)
    im.save(os.path.join(ITEMS, 'fl_%s.png' % iso))
    return True


def needed():
    roster = json.load(io.open(ROSTER, encoding='utf-8'))['people']
    return sorted({r['iso'] for r in roster})


def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'missing'
    if cmd == 'sheet':
        isos = [i for i in needed() if os.path.exists(os.path.join(ITEMS, 'fl_%s.png' % i))]
        k, cols = 3, 8
        rows = (len(isos) + cols - 1) // cols
        sheet = Image.new('RGBA', (cols * (W * k + 8) + 8, rows * (H * k + 22) + 8), (36, 24, 48, 255))
        for i, iso in enumerate(isos):
            im = Image.open(os.path.join(ITEMS, 'fl_%s.png' % iso)).convert('RGBA')
            sheet.alpha_composite(im.resize((W * k, H * k), Image.NEAREST),
                                  (8 + (i % cols) * (W * k + 8), 8 + (i // cols) * (H * k + 22)))
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
