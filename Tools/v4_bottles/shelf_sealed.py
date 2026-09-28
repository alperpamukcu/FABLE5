# -*- coding: utf-8 -*-
"""THE SHELF STYLE for the sealed vessels (2026-09-27): cans and cartons at the cellar's 32x64,
drawn by the same hand as the glass bottles (shelf_style.py).

The author, round three: "geri kalan tüm içeceklerin kutularını/şişelerini üret, sadece küçük
raf boylarını". Round four: the cans were "çok ince uzun ve gerçekçi boyutlarda değiller", the
boxes must stand "şişelerle aynı perspektifte aynı açıda", and the kegs keep their old art (beer
never stands in the cellar, so a keg needs no shelf size). So:

  * the SAME camera as the bottles: straight on, left-right symmetric, 17 degrees from above —
    a can's lid is an ellipse 30% of its width tall and its foot bows 15%; a carton is a box
    seen square: a true rectangle in front, NO side face, its top seen from above as a band
  * REAL proportions at the bottles' scale (a 70 cl bottle ~ 60 rows, so one row ~ half a
    centimetre): a 330 ml can 16 x 30, a 250 ml slim energy can 12 x 28, a 1 l gable carton
    16 x 44, a 1 l brick 18 x 36
  * one light from the upper left, metal stepped harder than card; one pixel of Night[0] round
    everything; every colour one of the 55
  * the brand's mark kept from the shipped art: LOCA and its wave, Blue Ox, KAPPY's green band,
    the Simple S, DOLA, the Sea Spray wave

A sealed vessel is one sprite (v4_<id>_c); the room draws no drink in it. Writes to
staging/shelf_style/<id>/ only.
"""
import json
import math
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import fontpx                                   # noqa: E402
import palette                                  # noqa: E402
from shelf_style import OUT, bands, rp, W0, H0  # noqa: E402

INK = palette.INK
FOOT = H0 - 2                                   # the lowest drawn row; the ring takes the last
XC = W0 / 2.0


def ramp_at(ramp, band, lift=0, table=None):
    steps = table or {'rim': 1, 'hi': 4, 'hi2': 3, 'lite': 3, 'mid': 2, 'shade': 1, 'rrim': 1, 'refl': 3}
    return rp(ramp, max(0, min(4, steps.get(band, 2) + lift)))


METAL = {'rim': 1, 'hi': 4, 'hi2': 4, 'lite': 3, 'mid': 2, 'shade': 1, 'rrim': 2, 'refl': 3}
PRINT = {'rim': 1, 'hi': 4, 'hi2': 3, 'lite': 3, 'mid': 2, 'shade': 1, 'rrim': 1, 'refl': 2}


def metal_bands(n):
    """A cylinder of metal: the glass bands plus a cool reflection three quarters across."""
    bl = bands(n)
    if n >= 10:
        bl[int(round(n * 0.78))] = 'refl'
    return bl


def ramp_of(c):
    for name, hexes in palette.RAMPS.items():
        for i, h in enumerate(hexes):
            if palette.hex_rgb(h) == tuple(c[:3]):
                return name, i
    return ramp_of(palette.nearest(c[:3]))


class Canvas:
    def __init__(self, S=1):
        self.W, self.H = W0 * S, H0 * S
        self.im = Image.new('RGBA', (self.W, self.H), (0, 0, 0, 0))
        self.px = self.im.load()

    def put(self, x, y, c):
        if 0 <= x < self.W and 0 <= y < self.H:
            self.px[x, y] = tuple(c[:3]) + (255,)

    def ring(self):
        W_, H_ = self.W, self.H
        pts = [(x, y) for y in range(H_) for x in range(W_) if not self.px[x, y][3] and any(
            0 <= x + dx < W_ and 0 <= y + dy < H_ and self.px[x + dx, y + dy][3]
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))]
        for x, y in pts:
            self.px[x, y] = INK + (255,)
        return self.im


def ball(cv, cx, cy, r, ramp, lift=0, outline=True):
    """A sphere lit from the upper left, in one ramp — the fruit."""
    for y in range(int(cy - r) - 1, int(cy + r) + 2):
        for x in range(int(cx - r) - 1, int(cx + r) + 2):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            d = math.hypot(dx, dy)
            if d > r:
                continue
            if outline and d > r - 1.0:
                cv.put(x, y, rp(ramp, max(0, lift)))
                continue
            k = (dx + dy) / (r * 1.4)
            step = 4 if k < -0.55 else 3 if k < -0.05 else 2 if k < 0.5 else 1
            cv.put(x, y, rp(ramp, max(0, min(4, step + lift))))


def leaf(cv, x, y, ramp):
    for i, (dx, dy) in enumerate([(0, 0), (1, 0), (1, -1), (2, -1)]):
        cv.put(x + dx, y + dy, rp(ramp, 3 if i < 2 else 2))


def word(cv, text, cx, y, colour, scale=1):
    g = fontpx.render(text, colour, scale).load()
    w = fontpx.width(text, scale)
    x0 = int(round(cx - w / 2.0))
    for gy in range(fontpx.GH * scale):
        for gx in range(w):
            if g[gx, gy][3]:
                cv.put(x0 + gx, y + gy, colour)


def grid(cv, rows, x0, y0, cols, S=1):
    for r, line in enumerate(rows):
        for c, ch in enumerate(line):
            if ch != '.':
                for a in range(S):
                    for b in range(S):
                        cv.put(x0 + c * S + a, y0 + r * S + b, cols[ch])


# ── the cans ─────────────────────────────────────────────────────────────────
CANS = {
    'cola_marlow': dict(w=16, ht=30),    # a 330 ml can
    'energy_volt': dict(w=12, ht=28),    # a 250 ml slim can
}


def can(cid, S=1, open=False):
    """A can at scale S; open=True is the hand can being poured: the tab lifted, the mouth dark."""
    cv = Canvas(S)
    xc = cv.W / 2.0
    foot = cv.H - 2
    w, ht = CANS[cid]['w'] * S, CANS[cid]['ht'] * S
    top = foot - ht + 1
    lid_w = w - 2 * S
    eh = 0.15 * lid_w                                     # half the lid's ellipse
    bow = max(2, int(round(0.15 * w)))
    neck_bot = top + int(round(2 * eh)) + S               # the lid, then the neck's taper
    body_top = neck_bot + S
    body_bot = foot - bow - S                             # then the foot's taper and its bow
    print_top, print_bot = body_top, body_bot

    def body_colour(y, i, width):
        b = metal_bands(width)[i]
        if cid == 'cola_marlow':
            u = i / float(max(1, width - 1))
            wy = print_top + int((print_bot - print_top) * 0.60) - int(round(1.6 * S * math.sin(u * math.pi * 1.3)))
            if wy <= y <= wy + 2 * S - 1:
                return rp('Cream', 4 if b in ('hi', 'lite', 'hi2') else 3 if b not in ('shade', 'rrim') else 2)
            return ramp_at('ViceRed', b, 0, PRINT)
        # Blue Ox: blue and silver in diamonds, as its model's checks
        yy = (y - print_top) // S
        ii = i // S
        k = ((ii + yy) // 3 + (ii - yy) // 3) % 2
        if y < print_top + (print_bot - print_top) * 0.5 or k == 0:
            return ramp_at('ClubBlue', b, 0, PRINT)
        return ramp_at('Graphite', b, 1, METAL)

    for y in range(neck_bot, foot + 1):
        width = w
        if y < neck_bot + S:
            width = w - 2 * S                             # the neck's taper up to the lid
        elif y > foot - bow:
            k = (y - (foot - bow)) / float(bow + 0.5)
            width = 2 * int(round((w / 2.0 - S) * math.sqrt(max(0.0, 1 - k * k))))
        elif y > body_bot:
            width = w - 2 * S                             # the foot's taper
        if width < 2:
            continue
        xs = int(xc - width / 2.0)
        for i in range(width):
            if width == w and print_top <= y <= print_bot:
                c = body_colour(y, i, width)
            else:
                c = ramp_at('Graphite', metal_bands(width)[i], 1, METAL)
            cv.put(xs + i, y, c)
    # the lid: the rim's top an ellipse, inside it the recessed lid, the tab (or, open, the mouth)
    yc = top + eh
    for y in range(top, neck_bot):
        ry = y + 0.5 - yc
        for x in range(int(xc - lid_w / 2.0), int(xc + lid_w / 2.0)):
            dx = (x + 0.5 - xc) / (lid_w / 2.0)
            dy = ry / eh
            if ry < 0 and dx * dx + dy * dy > 1:
                continue
            inner = dx * dx / 0.6 + dy * dy / 0.4 <= 1
            if y < yc + eh:
                c = rp('Graphite', 2 if inner else (4 if x + 0.5 < xc else 3))
                if open and inner:
                    # the opening, a dark keyhole toward the near rim
                    ox, oy = (x + 0.5 - xc) / (lid_w * 0.16), (y + 0.5 - (yc + eh * 0.35)) / (eh * 0.35)
                    if ox * ox + oy * oy <= 1:
                        c = rp('Night', 0)
                cv.put(x, y, c)
            else:
                cv.put(x, y, ramp_at('Graphite', metal_bands(lid_w)[x - int(xc - lid_w / 2.0)], 1, METAL))
    tx, ty = int(xc) - S, int(yc) - (int(eh * 0.6) if open else 0)
    for a in range(2 * S):
        for b in range(S + (S if open else 0)):
            cv.put(tx + a, ty - b, rp('Graphite', 4 if b == 0 else 3))
    # the print
    if cid == 'cola_marlow':
        word(cv, 'LOCA', xc, print_top + 3 * S, rp('Cream', 4), 1 if S == 1 else 2 if fontpx.width('LOCA', 2) <= w - 4 * S else 1)
    else:
        cy = print_top + int((print_bot - print_top) * 0.36)
        ball(cv, xc, cy, 3.0 * S, 'Amber', 0, outline=False)
        grid(cv, ['k...k', '.kkk.', '.kak.', '..k..'], int(xc - 2.5 * S), cy - 2 * S,
             {'k': rp('ViceRed', 2), 'a': rp('Cream', 4)}, S)
    return cv.ring()


# ── the cartons ──────────────────────────────────────────────────────────────
# id: (front, lower, top band, mark, mark colour, fruit, gable)
CARTONS = {
    'orange_grove':    (('Amber', 3), ('Amber', 2), ('Lime', 1),    'K',    ('Cream', 4), 'orange', True),
    'lemon_fresh':     (('Cream', 4), ('Amber', 4), None,           'S',    ('Night', 1), 'lemon', True),
    'lime_fresh':      (('Cream', 4), ('Lime', 3),  None,           'S',    ('Night', 1), 'lime', True),
    'pineapple_isla':  (('Amber', 4), ('Amber', 3), ('ViceRed', 2), 'DOLA', ('Cream', 4), 'pineapple', True),
    'cranberry_north': (('ViceRed', 2), ('ViceRed', 1), None,       None,   None,         'cranberry', False),
}
MARKS = {
    'S': ['.kkkk', 'kk...', 'kk...', '.kkk.', '...kk', '...kk', 'kkkk.'],
    'K': ['kk..kk', 'kk.kk.', 'kkkk..', 'kkkk..', 'kk.kk.', 'kk..kk'],
}


def carton(cid):
    front_rs, low_rs, band_rs, mark, mark_rs, fruit, gable = CARTONS[cid]
    cv = Canvas()
    fw = 16 if gable else 18
    x0 = int(XC - fw / 2.0)
    x1 = x0 + fw - 1
    body_h = 36 if gable else 32
    y_bot = FOOT
    y_top = y_bot - body_h + 1
    fr, lo = rp(*front_rs), rp(*low_rs)
    fr_r, fr_i = ramp_of(fr)
    split = y_top + int(body_h * 0.62)
    # the front: a flat face lit evenly, a lit crease down the left edge, a shaded one down the right
    for y in range(y_top, y_bot + 1):
        for x in range(x0, x1 + 1):
            c = fr if y < split else lo
            r_, i_ = ramp_of(c)
            if x == x0:
                c = rp(r_, min(4, i_ + 1))
            elif x == x1:
                c = rp(r_, max(0, i_ - 1))
            cv.put(x, y, c)
    if band_rs:
        br, bi = band_rs
        for y in range(y_top, y_top + 8):
            for x in range(x0, x1 + 1):
                cv.put(x, y, rp(br, min(4, bi + 1)) if x == x0 else rp(br, max(0, bi - 1)) if x == x1 else rp(br, bi))
    if gable:
        # the roof panel, tilted back and seen from above: lit, a darker crease where it folds
        # off the front; over it the sealed fin, the full width, in the card's own white
        roof_h = 5
        for k in range(roof_h):
            y = y_top - 1 - k
            for x in range(x0, x1 + 1):
                c = rp(fr_r, max(0, fr_i - 1)) if k == 0 else rp(fr_r, min(4, fr_i + 1))
                if x == x1 and k > 0:
                    c = rp(fr_r, fr_i)
                cv.put(x, y, c)
        yf = y_top - 1 - roof_h
        for y, st in ((yf, 2), (yf - 1, 4)):
            for x in range(x0, x1 + 1):
                cv.put(x, y, rp('Cream', st if x < x1 - 1 else st - 1))
        cap_c = (int(XC), y_top - 3)
    else:
        # a brick's top face, seen from above: a lit band the full width
        for k in range(3):
            y = y_top - 1 - k
            for x in range(x0, x1 + 1):
                cv.put(x, y, rp(fr_r, min(4, fr_i + 1)) if k < 2 else rp(fr_r, fr_i))
        cap_c = (int(XC), y_top - 2)
    # the screw cap from above: a white disc on a short side
    cx, cy = cap_c
    for y in range(cy - 2, cy + 2):
        for x in range(cx - 2, cx + 2):
            dx, dy = (x + 0.5 - cx) / 2.2, (y + 0.5 - (cy - 0.5)) / 1.3
            if y <= cy - 1 and dx * dx + dy * dy <= 1:
                cv.put(x, y, rp('Cream', 4 if x < cx else 3))
            elif y > cy - 1:
                cv.put(x, y, rp('Cream', 3 if x < cx else 2))
    # the mark
    if mark == 'DOLA':
        word(cv, 'DOLA', XC, y_top + 2, rp(*mark_rs))
    elif mark in MARKS:
        m = MARKS[mark]
        grid(cv, m, int(XC - len(m[0]) / 2.0), y_top + (1 if band_rs else 3), {'k': rp(*mark_rs)})
    # the fruit
    fy = y_top + int(body_h * 0.5)
    if fruit == 'orange':
        ball(cv, XC, fy, 4.6, 'Amber')
        leaf(cv, int(XC) + 1, int(fy - 4.6), 'Lime')
    elif fruit == 'lemon':
        for y in range(int(fy - 3), int(fy + 4)):
            for x in range(int(XC - 5), int(XC + 5)):
                dx, dy = (x + 0.5 - XC) / 4.8, (y + 0.5 - fy) / 3.2
                if dx * dx + dy * dy <= 1:
                    edge = dx * dx + dy * dy > 0.66
                    cv.put(x, y, rp('Amber', 2) if edge else rp('Amber', 4) if dx + dy < -0.3 else rp('Amber', 3))
        leaf(cv, int(XC) + 1, int(fy - 3), 'Lime')
    elif fruit == 'lime':
        r = 4.6
        for y in range(int(fy - r) - 1, int(fy + r) + 1):
            for x in range(int(XC - r) - 1, int(XC + r) + 1):
                dx, dy = x + 0.5 - XC, y + 0.5 - fy
                d = math.hypot(dx, dy)
                if d > r:
                    continue
                if d > r - 1.2:
                    c = rp('Lime', 1)
                elif d < 0.9 or abs(dx) < 0.5 or abs(dy) < 0.5:
                    c = rp('Lime', 4)
                else:
                    c = rp('Lime', 3)
                cv.put(x, y, c)
    elif fruit == 'pineapple':
        for y in range(int(fy - 4), int(fy + 5)):
            for x in range(int(XC - 4), int(XC + 4)):
                dx, dy = (x + 0.5 - XC) / 3.6, (y + 0.5 - fy) / 4.8
                if dx * dx + dy * dy <= 1:
                    edge = dx * dx + dy * dy > 0.7
                    hatch = (x + y) % 3 == 0 or (x - y) % 3 == 0
                    cv.put(x, y, rp('Malt', 1) if edge else rp('Malt', 2) if hatch else rp('Amber', 3))
        for i, (dx, dy) in enumerate([(-2, -5), (-1, -6), (0, -7), (1, -6), (0, -5), (-1, -5)]):
            cv.put(int(XC) + dx, int(fy) + dy, rp('Lime', 3 if i % 2 else 2))
    elif fruit == 'cranberry':
        wy = y_top + 5
        for x in range(x0, x1 + 1):
            u = (x - x0) / float(fw)
            yy = wy + int(round(1.4 * math.sin(u * math.pi * 2)))
            cv.put(x, yy, rp('Cream', 4)); cv.put(x, yy + 1, rp('Cream', 3))
        for bx, by in ((-3.0, 1.5), (2.5, 2.0), (-0.3, -2.3)):
            ball(cv, XC + bx, fy + by, 2.9, 'ViceRed', 1)
        leaf(cv, int(XC) + 2, int(fy) - 5, 'Lime')
    for x in range(x0, x1 + 1):
        r_, i_ = ramp_of(lo)
        cv.put(x, y_bot - 3, rp(r_, max(0, i_ - 1)))
    return cv.ring()


SEALED_IDS = ['cola_marlow', 'energy_volt', 'orange_grove', 'lemon_fresh', 'lime_fresh', 'pineapple_isla',
              'cranberry_north']


def draw(cid, S=1, open=False):
    return can(cid, S, open) if cid in CANS else carton(cid)


def run():
    made = {}
    for cid in CANS:
        d = os.path.join(OUT, cid)
        os.makedirs(d, exist_ok=True)
        for S, name, op in ((1, 'v4_%s_c.png', False), (3, 'v4_%s.png', False), (3, 'v4_%s_open.png', True)):
            im = can(cid, S, op)
            im.save(os.path.join(d, name % cid))
            px = im.load()
            off = sum(1 for y in range(im.height) for x in range(im.width)
                      if px[x, y][3] and palette.nearest(px[x, y][:3]) != px[x, y][:3])
            made['%s@%d%s' % (cid, S, '_open' if op else '')] = {'off_palette': off, 'bbox': im.getbbox()}
    with open(os.path.join(OUT, 'made_cans.json'), 'w') as f:
        json.dump(made, f, indent=1)
    return made


if __name__ == '__main__':
    m = run()
    print(len(m), 'drawn;', sum(v['off_palette'] for v in m.values()), 'off-palette pixels')
