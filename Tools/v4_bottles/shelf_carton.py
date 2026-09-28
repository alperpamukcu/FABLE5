# -*- coding: utf-8 -*-
"""THE JUICE CARTONS as boxes (2026-09-27, round five): "2.5 d olmalı, hafif sola dönük olabilir
kutu" — a real box, turned a little to the left, seen from the game's 17 degrees.

Geometry, not guesswork: a box W wide, D deep, H tall, yawed THETA to the left and pitched PHI,
projected orthographically. Turned left, the front faces left of the camera, so its right end
comes nearer and drops a pixel every ~9 across, and the RIGHT side face shows as a narrow strip
(D sin THETA wide) whose top edge climbs steeply away. The top is seen from above (D cos THETA
sin PHI tall): a gable carton shows its front roof panel, the gable's triangle on the right side
and the sealed fin along the ridge; a brick shows a flat lit top.

The light is the bottles' (upper left): the front, facing left, takes it full; the roof, facing
up, a step brighter; the right side, turned away, two steps darker. The print is drawn flat at
the front face's own screen width and SHEARED onto it column by column, never resampled, so
every pixel stays a pixel. One function draws the cellar copy (S=1, 32x64) and the hand carton
(S=3, 96x192); open=True takes the cap off (the pouring state: a white spout ring, a dark mouth).

Sizes are real at the bottles' scale (one cellar row ~ half a centimetre): a 1 l gable carton is
7 x 7 x 19.5 cm + its gable, a 1 l brick 9.4 x 6.4 x 16.5 cm.
"""
import json
import math
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import fontpx                              # noqa: E402
import palette                             # noqa: E402
from shelf_style import OUT, rp, W0, H0    # noqa: E402

INK = palette.INK
THETA = math.radians(20)                   # turned left
PHI = math.radians(17)                     # the game's pitch

# id: dict(kind, front, lower, band, mark, mark colour, fruit)
CARTONS = {
    'orange_grove':    dict(kind='prisma', front=('Amber', 3), lower=('Amber', 2), band=('Lime', 1),
                            mark='KAPPY', ink=('Cream', 4), fruit='orange'),
    'lemon_fresh':     dict(kind='prisma', front=('Cream', 4), lower=('Amber', 4), band=None,
                            mark='S', ink=('Night', 1), fruit='lemon'),
    'lime_fresh':      dict(kind='prisma', front=('Cream', 4), lower=('Lime', 3), band=None,
                            mark='S', ink=('Night', 1), fruit='lime'),
    'pineapple_isla':  dict(kind='prisma', front=('Amber', 4), lower=('Amber', 3), band=('ViceRed', 2),
                            mark='DOLA', ink=('Cream', 4), fruit='pineapple'),
    'cranberry_north': dict(kind='brick', front=('ViceRed', 2), lower=('ViceRed', 1), band=None,
                            mark='wave', ink=('Cream', 4), fruit='cranberry'),
}
# a 1 l Tetra Prisma is 7 x 7 x 20 cm, a 1 l brick 9.4 x 6.4 x 16.5 cm (one cellar unit = 0.5 cm)
DIMS = {'gable': dict(W=14, D=14, H=38, G=6, F=2), 'prisma': dict(W=14, D=14, H=40, G=0, F=0),
        'brick': dict(W=18, D=12, H=35, G=0, F=0)}   # 35: the market draws a drawing over 39 rows at 2x, like glass
CAP_AT = (0.30, 0.70)                      # the cap near the top's back-left corner, its highest part
CAP_R, CAP_H, SPOUT_H = 2.2, 2.0, 1.6      # cap radius and height, the bare spout's height (units)
SIMPLE_S = ['.kkkk', 'kk...', 'kk...', '.kkk.', '...kk', '...kk', 'kkkk.']
BOLD = {
    'K': ['kk..kk', 'kk.kk.', 'kkkk..', 'kkkk..', 'kk.kk.', 'kk..kk'],
    'D': ['kkkk.', 'kk.kk', 'kk..k', 'kk..k', 'kk.kk', 'kkkk.'],
}


def ramp_of(c):
    for name, hexes in palette.RAMPS.items():
        for i, h in enumerate(hexes):
            if palette.hex_rgb(h) == tuple(c[:3]):
                return name, i
    return ramp_of(palette.nearest(c[:3]))


def step(c, k):
    r, i = ramp_of(c)
    return rp(r, max(0, min(4, i + k)))


class Box:
    """The projection: box coordinates (x along the front, y up, z back from the front) to screen
    pixels (u right, v down), anchored so the nearest bottom corner stands on the canvas foot."""

    def __init__(self, W, D, H, S, cx, foot):
        self.W, self.D, self.H, self.S = W, D, H, S
        c, s = math.cos(THETA), math.sin(THETA)
        self.c, self.s = c, s
        # the silhouette's width is W cos + D sin; centre it
        width = (W * c + D * s) * S
        self.u0 = cx - width / 2.0
        # the lowest point is the front face's right-bottom corner (nearest): v = W s sinPHI
        self.v0 = foot + 1 - W * s * math.sin(PHI) * S

    def p(self, x, y, z):
        X = x * self.c + z * self.s
        Zd = -x * self.s + z * self.c
        u = self.u0 + X * self.S
        v = self.v0 + (-y * math.cos(PHI) - Zd * math.sin(PHI)) * self.S
        return u, v


def fill_poly(px, pts, colour_at, Wd, Ht):
    """Every pixel whose centre is inside the convex polygon gets colour_at(u, v)."""
    us = [p[0] for p in pts]; vs = [p[1] for p in pts]
    n = len(pts)
    area = sum(pts[i][0] * pts[(i + 1) % n][1] - pts[(i + 1) % n][0] * pts[i][1] for i in range(n))
    sgn = 1 if area > 0 else -1
    for y in range(max(0, int(min(vs)) - 1), min(Ht, int(max(vs)) + 2)):
        for x in range(max(0, int(min(us)) - 1), min(Wd, int(max(us)) + 2)):
            cx_, cy_ = x + 0.5, y + 0.5
            inside = True
            for i in range(n):
                ax, ay = pts[i]; bx, by = pts[(i + 1) % n]
                if sgn * ((bx - ax) * (cy_ - ay) - (by - ay) * (cx_ - ax)) < -1e-9:
                    inside = False
                    break
            if inside:
                c = colour_at(x, y)
                if c is not None:
                    px[x, y] = tuple(c[:3]) + (255,)


def ball(put, cx, cy, r, ramp, lift=0):
    for y in range(int(cy - r) - 1, int(cy + r) + 2):
        for x in range(int(cx - r) - 1, int(cx + r) + 2):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            d = math.hypot(dx, dy)
            if d > r:
                continue
            if d > r - 1.0:
                put(x, y, rp(ramp, max(0, lift)))
                continue
            k = (dx + dy) / (r * 1.4)
            st = 4 if k < -0.55 else 3 if k < -0.05 else 2 if k < 0.5 else 1
            put(x, y, rp(ramp, max(0, min(4, st + lift))))


def print_plate(spec, fw, fh, S):
    """The front's print, flat, at the face's own screen size: a dict (x, y) -> colour."""
    P = {}

    def put(x, y, c):
        if 0 <= x < fw and 0 <= y < fh:
            P[(x, y)] = tuple(c[:3])
    fr, lo = rp(*spec['front']), rp(*spec['lower'])
    split = int(fh * 0.62)
    for y in range(fh):
        for x in range(fw):
            put(x, y, fr if y < split else lo)
    band_h = 0
    if spec['band']:
        band_h = 8 * S
        for y in range(band_h):
            for x in range(fw):
                put(x, y, rp(*spec['band']))
    ink = rp(*spec['ink'])
    xc = fw / 2.0
    mark = spec['mark']
    P['_rigid'] = (0, fw - 1)                              # lettering is sheared as ONE piece (see front_colour)
    if mark in ('KAPPY', 'DOLA'):
        if S == 1:
            # the cellar copy: the brand's bold initial, drawn as a mark (a 3x5 word does not fit)
            m = BOLD[mark[0]]
            for r_, line in enumerate(m):
                for c_, ch in enumerate(line):
                    if ch != '.':
                        put(int(xc - len(line) / 2.0) + c_, 2 + r_, ink)     # two rows down: the face's top edge slopes
        else:
            # the hand carton: the whole word, as big as it fits
            scale = 2 if fontpx.width(mark, 2) <= fw - 4 * S else 1
            w = fontpx.width(mark, scale)
            g = fontpx.render(mark, ink, scale).load()
            gh = fontpx.GH * scale
            x0 = int(round(xc - w / 2.0)); y0 = (band_h - gh) // 2
            for gy in range(gh):
                for gx in range(w):
                    if g[gx, gy][3]:
                        put(x0 + gx, y0 + gy, ink)
    elif mark == 'S':
        for r_, line in enumerate(SIMPLE_S):
            for c_, ch in enumerate(line):
                if ch == '.':
                    continue
                for dy in range(S):
                    for dx in range(S):
                        put(int(xc - 2.5 * S) + c_ * S + dx, 3 * S + r_ * S + dy, ink)
    elif mark == 'wave':
        wy = 5 * S
        for x in range(fw):
            u = x / float(fw)
            yy = wy + int(round(1.4 * S * math.sin(u * math.pi * 2)))
            for t in range(S):
                put(x, yy + t, rp('Cream', 4))
                put(x, yy + S + t, rp('Cream', 3))
    fy = int(fh * 0.50)
    fruit = spec['fruit']
    if fruit == 'orange':
        ball(put, xc, fy, 4.4 * S, 'Amber')
        for i, (dx, dy) in enumerate([(0, 0), (1, 0), (1, -1), (2, -1)]):
            for a in range(S):
                for b in range(S):
                    put(int(xc) + dx * S + a, int(fy - 4.4 * S) + dy * S + b, rp('Lime', 3 if i < 2 else 2))
    elif fruit == 'lemon':
        for y in range(int(fy - 3.4 * S), int(fy + 3.6 * S)):
            for x in range(int(xc - 5 * S), int(xc + 5 * S)):
                dx, dy = (x + 0.5 - xc) / (4.6 * S), (y + 0.5 - fy) / (3.1 * S)
                if dx * dx + dy * dy <= 1:
                    edge = dx * dx + dy * dy > (0.66 if S == 1 else 0.86)
                    put(x, y, rp('Amber', 2) if edge else rp('Amber', 4) if dx + dy < -0.3 else rp('Amber', 3))
    elif fruit == 'lime':
        r = 4.4 * S
        for y in range(int(fy - r) - 1, int(fy + r) + 1):
            for x in range(int(xc - r) - 1, int(xc + r) + 1):
                dx, dy = x + 0.5 - xc, y + 0.5 - fy
                d = math.hypot(dx, dy)
                if d > r:
                    continue
                if d > r - 1.2 * S:
                    c = rp('Lime', 1)
                elif d < 0.9 * S or abs(dx) < 0.5 * S or abs(dy) < 0.5 * S:
                    c = rp('Lime', 4)
                else:
                    c = rp('Lime', 3)
                put(x, y, c)
    elif fruit == 'pineapple':
        for y in range(int(fy - 4 * S), int(fy + 5 * S)):
            for x in range(int(xc - 4 * S), int(xc + 4 * S)):
                dx, dy = (x + 0.5 - xc) / (3.5 * S), (y + 0.5 - fy) / (4.6 * S)
                if dx * dx + dy * dy <= 1:
                    edge = dx * dx + dy * dy > (0.7 if S == 1 else 0.88)
                    hatch = ((x // S) + (y // S)) % 3 == 0 or ((x // S) - (y // S)) % 3 == 0
                    put(x, y, rp('Malt', 1) if edge else rp('Malt', 2) if hatch else rp('Amber', 3))
        for i, (dx, dy) in enumerate([(-2, -5), (-1, -6), (0, -7), (1, -6), (0, -5), (-1, -5)]):
            for a in range(S):
                for b in range(S):
                    put(int(xc) + dx * S + a, int(fy) + dy * S + b, rp('Lime', 3 if i % 2 else 2))
    elif fruit == 'cranberry':
        for bx, by in ((-3.0, 1.5), (2.5, 2.0), (-0.3, -2.3)):
            ball(put, xc + bx * S, fy + by * S, 2.9 * S, 'ViceRed', 1)
    for x in range(fw):
        for t in range(S):
            put(x, fh - 3 * S + t, step(lo, -1))
    return P


# ── THE HAND CARTON'S PRINT (round seven) ─────────────────────────────────────
# The author: "meyve suyu görsellerinin büyük halinde biraz daha yaratıcılık katabilirsin, küçüğünün
# bire bir aynısı olmak zorunda değil". The cellar copy stays the brand's colours, initial and fruit;
# the hand carton is the pack itself: a sunburst behind the fruit, the fruit whole and cut, drops of
# juice, a badge, a wave between the halves, the kind and the size in words, a nutrition panel on the
# side. Drawn flat on hand_label's Plate: the ground is sheared onto the face column by column, the
# words and the fruit move as one piece.
def _orange(P, cx, cy, r, ramp='Amber', leaf=True):
    g = P.group(cx)
    for y in range(int(cy - r) - 1, int(cy + r) + 2):
        for x in range(int(cx - r) - 1, int(cx + r) + 2):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            d = math.hypot(dx, dy)
            if d > r:
                continue
            k = (dx + dy) / (r * 1.4)
            st = 1 if d > r - 1 else (4 if k < -0.5 else 3 if k < 0.1 else 2)
            if st > 1 and ((x * 7 + y * 5) % 11) == 0:
                st = max(1, st - 1)                        # the peel's pores
            P.put(x, y, rp(ramp, st), g)
    if leaf:
        for i, (dx, dy) in enumerate(((0, 0), (1, -1), (2, -1), (3, -2), (4, -2), (2, -2), (3, -3))):
            P.put(int(cx) + dx, int(cy - r) + dy, rp('Lime', 3 if i % 2 else 2), g)
        P.put(int(cx), int(cy - r) + 1, rp('Malt', 1), g)


def _wheel(P, cx, cy, r, ramp='Amber', seg=8):
    """A fruit cut across: rind, pith, segments round a white heart."""
    g = P.group(cx)
    for y in range(int(cy - r) - 1, int(cy + r) + 2):
        for x in range(int(cx - r) - 1, int(cx + r) + 2):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            d = math.hypot(dx, dy)
            if d > r:
                continue
            ang = (math.degrees(math.atan2(dy, dx)) + 360) % (360.0 / seg)
            if d > r - 1:
                c = rp(ramp, 1)
            elif d > r - 2.2:
                c = rp('Cream', 4)
            elif d < 1.5 or ang < 360.0 / seg * 0.16:
                c = rp('Cream', 4)
            else:
                c = rp(ramp, 4 if dx + dy < 0 else 3)
            P.put(x, y, c, g)


def _lemon(P, cx, cy, rx, ry, ramp='Amber'):
    g = P.group(cx)
    for y in range(int(cy - ry) - 1, int(cy + ry) + 2):
        for x in range(int(cx - rx) - 3, int(cx + rx) + 4):
            dx, dy = (x + 0.5 - cx) / rx, (y + 0.5 - cy) / ry
            d = dx * dx + dy * dy
            nub = d > 1 and abs(y + 0.5 - cy) < 1.2 and abs(x + 0.5 - cx) < rx + 2
            if d > 1 and not nub:
                continue
            st = 2 if (d > 0.8 or nub) else (4 if dx + dy < -0.3 else 3)
            P.put(x, y, rp(ramp, st), g)


def _drops(P, pts, c1, c2):
    for (x, y) in pts:
        g = P.group(x)
        for (dx, dy, c) in ((0, 0, c1), (0, 1, c2), (-1, 1, c2), (1, 1, c2), (0, 2, c2)):
            P.put(x + dx, y + dy, c, g)


def _badge(P, cx, cy, r, ramp, word):
    g = P.group(cx)
    for y in range(int(cy - r) - 1, int(cy + r) + 2):
        for x in range(int(cx - r) - 1, int(cx + r) + 2):
            d = math.hypot(x + 0.5 - cx, y + 0.5 - cy)
            if d <= r:
                P.put(x, y, rp(ramp, 1) if d > r - 1 else rp(ramp, 2), g)
    P.text(word, cx, int(cy - 2), rp('Cream', 4))


def _wave(P, y, h, c1, c2):
    for x in range(P.w):
        wy = int(round(y - 2 * math.sin(x / float(P.w) * math.pi * 2)))
        for t in range(h):
            P.put(x, wy + t, c1 if t < h // 2 + (h % 2) else c2)


def print_hand(cid, spec, fw, fh):
    import hand_label as hl
    P = hl.Plate(fw, fh)
    P.form('full')
    fr, lo = rp(*spec['front']), rp(*spec['lower'])
    split = int(fh * 0.62)
    P.where(lambda x, y: y < split, fr)
    P.where(lambda x, y: y >= split, lo)
    cx = fw / 2.0
    fruit = spec['fruit']
    word = {'orange': 'ORANGE', 'lemon': 'LEMONADE', 'lime': 'LIMEADE', 'pineapple': 'PINEAPPLE',
            'cranberry': 'CRANBERRY'}[fruit]
    if fruit == 'orange':
        P.pat('rays', rp('Amber', 4), cx=cx, cy=46, n=16)
        P.hband(0, 17, rp('Lime', 1)); P.hline(17, rp('Cream', 4)); P.hline(18, rp('Lime', 0))
        P.text('KAPPY', cx, 4, rp('Cream', 4), 2, gap=1)
        _orange(P, cx - 5, 44, 11)
        _wheel(P, cx + 9, 55, 8)
        _drops(P, [(5, 30), (fw - 6, 40), (6, 58), (fw - 8, 64)], rp('Cream', 4), rp('Amber', 4))
        _badge(P, fw - 9, 27, 7, 'Lime', '100')
    elif fruit in ('lemon', 'lime'):
        ramp = 'Amber' if fruit == 'lemon' else 'Lime'
        P.where(lambda x, y: 18 < y < split and (x + 2 * y) % 9 == 0 and y % 3 == 0,
                rp(ramp, 4) if fruit == 'lemon' else rp('Lime', 4))
        P.hband(0, 17, rp('Cream', 4)); P.hline(17, rp(ramp, 2))
        g = P.group(10)
        for y in range(2, 17):
            for x in range(3, 18):
                d = math.hypot(x + 0.5 - 10, y + 0.5 - 9.5)
                if d <= 7:
                    P.put(x, y, rp(ramp, 1) if d > 6 else rp(ramp, 2), g)
        for r_, line in enumerate(SIMPLE_S):
            for c_, ch in enumerate(line):
                if ch != '.':
                    P.put(8 + c_, 6 + r_, rp('Cream', 4), g)
        P.text('FRESH', cx + 6, 7, rp('Night', 1))
        if fruit == 'lemon':
            _lemon(P, cx - 4, 42, 10, 7)
            _wheel(P, cx + 8, 55, 8, 'Amber')
        else:
            _orange(P, cx - 4, 42, 9, ramp='Lime', leaf=True)
            _wheel(P, cx + 8, 55, 8, 'Lime')
        _drops(P, [(5, 28), (fw - 6, 36), (6, 58)], rp('Cream', 4), rp(ramp, 3))
    elif fruit == 'pineapple':
        P.pat('rdiag', rp('Amber', 3), p=6)
        P.hband(0, 17, rp('ViceRed', 2)); P.hline(17, rp('Cream', 4)); P.hline(18, rp('ViceRed', 1))
        P.say('DOLA', cx, 4, rp('Cream', 4), 2, inset=0)
        pcx, pcy, rx, ry = cx, 50, 9, 12
        g = P.group(pcx)
        for y in range(int(pcy - ry), int(pcy + ry) + 1):
            for x in range(int(pcx - rx), int(pcx + rx) + 1):
                dx, dy = (x + 0.5 - pcx) / rx, (y + 0.5 - pcy) / ry
                if dx * dx + dy * dy > 1:
                    continue
                edge = dx * dx + dy * dy > 0.82
                hatch = (x + y) % 4 == 0 or (x - y) % 4 == 0
                P.put(x, y, rp('Malt', 1) if edge else rp('Malt', 2) if hatch else
                      rp('Amber', 4) if dx + dy < -0.4 else rp('Amber', 3), g)
        for dx, h_ in ((-6, 7), (-4, 10), (-2, 13), (0, 15), (2, 13), (4, 10), (6, 7)):
            for t in range(h_):
                x = int(pcx + dx + (dx * t) // 12)
                y = int(pcy - ry - t)
                P.put(x, y, rp('Lime', 3 if t < h_ - 2 else 2), g)
                P.put(x + (1 if dx >= 0 else -1), y, rp('Lime', 2), g)
        _drops(P, [(5, 30), (fw - 6, 32), (6, 62)], rp('Cream', 4), rp('Amber', 4))
    elif fruit == 'cranberry':
        P.where(lambda x, y: y < 18 and (x * 3 + y * 5) % 17 == 0, rp('ViceRed', 3))
        for sx, sy in ((4, 16), (fw - 5, 17), (fw // 2, 25)):
            g = P.group(sx)
            for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1), (2, 0), (-2, 0), (0, 2), (0, -2)):
                P.put(sx + dx, sy + dy, rp('Cream', 4), g)
        P.say('NORTH', cx, 5, rp('Cream', 4), 2, inset=2, shadow=rp('ViceRed', 0))
        _wave(P, 20, 4, rp('Cream', 4), rp('Cream', 3))
        g = P.group(cx)
        for bx, by in ((-7, 44), (5, 46), (-1, 38), (-2, 52), (9, 38)):
            for y in range(by - 6, by + 7):
                for x in range(int(cx) + bx - 6, int(cx) + bx + 7):
                    dx_, dy_ = x + 0.5 - (cx + bx), y + 0.5 - by
                    d = math.hypot(dx_, dy_)
                    if d <= 5.5:
                        st = 0 if d > 4.6 else (4 if (dx_ + dy_) < -4 else 3 if dx_ + dy_ < 0 else 2)
                        P.put(x, y, rp('ViceRed', st), g)
        for i in range(8):
            P.put(int(cx) + 3 + i, 30 - i // 2, rp('Lime', 2), g)
            P.put(int(cx) + 3 + i, 31 - i // 2, rp('Lime', 3), g)
        P.put(int(cx) + 2, 32, rp('Malt', 1), g)
    _wave(P, split, 3, rp('Cream', 4), rp('Cream', 3))
    ink = rp('Cream', 4) if fruit in ('orange', 'pineapple', 'cranberry') else rp('Night', 1)
    y = split + 7
    y += P.say(word, cx, y, ink, 2, inset=1) + 3
    P.text('JUICE' if fruit not in ('lemon', 'lime') else 'DRINK', cx, y, ink)
    P.text('1L', fw - 8, fh - 12, ink)
    P.hband(fh - 4, 3, step(lo, -1))
    return P


def side_panel(bx, W, D, H, S, x, y):
    """(z, height) on the right side face under screen pixel (x, y): for the nutrition panel."""
    c, s = bx.c, bx.s
    z = ((x + 0.5 - bx.u0) / S - W * c) / s
    Zd = -W * s + z * c
    yy = (-(y + 0.5 - bx.v0) / S - Zd * math.sin(PHI)) / math.cos(PHI)
    return z, yy


def draw(cid, S=1, open=False):
    spec = CARTONS[cid]
    dm = DIMS[spec['kind']]
    W, D, H, G, F = dm['W'], dm['D'], dm['H'], dm['G'], dm['F']
    Wd, Ht = W0 * S, H0 * S
    im = Image.new('RGBA', (Wd, Ht), (0, 0, 0, 0))
    px = im.load()
    bx = Box(W, D, H, S, Wd / 2.0, Ht - 2)
    fr = rp(*spec['front'])
    top_c = rp(*spec['band']) if spec['band'] else fr
    side_split = H * (1 - 0.62)                           # the colour split, carried round the side

    def flat(c):
        return lambda x, y: c

    # the right side face: two steps darker, the split carried round
    side = [bx.p(W, 0, 0), bx.p(W, 0, D), bx.p(W, H, D), bx.p(W, H, 0)]

    def side_colour(x, y):
        # which height is this pixel on the side? invert v along the side's vertical
        u0, v0 = bx.p(W, 0, 0)
        return step(rp(*spec['lower']), -2) if (v0 - (y + 0.5)) < side_split * math.cos(PHI) * S else step(fr, -2)
    if S >= 3:
        plain = side_colour

        def side_colour(x, y):
            z, yy = side_panel(bx, W, D, H, S, x, y)
            if D * 0.18 <= z <= D * 0.86 and H * 0.2 <= yy <= H * 0.62:
                row = int((H * 0.62 - yy) * math.cos(PHI) * S)
                if row < 3:
                    return rp('Night', 1)
                return rp('Cream', 2) if row % 3 == 0 else rp('Cream', 3)
            return plain(x, y)
    fill_poly(px, side, side_colour, Wd, Ht)
    if spec['kind'] == 'gable':
        # the gable's triangle on the right, the fin along the ridge, the front roof panel
        tri = [bx.p(W, H, 0), bx.p(W, H, D), bx.p(W, H + G, D / 2.0)]
        fill_poly(px, tri, flat(step(top_c, -2)), Wd, Ht)
        fin = [bx.p(0, H + G, D / 2.0), bx.p(W, H + G, D / 2.0), bx.p(W, H + G + F, D / 2.0), bx.p(0, H + G + F, D / 2.0)]
        fill_poly(px, fin, lambda x, y: rp('Cream', 4 if x < bx.p(W * 0.6, 0, 0)[0] else 3), Wd, Ht)
        roof = [bx.p(0, H, 0), bx.p(W, H, 0), bx.p(W, H + G, D / 2.0), bx.p(0, H + G, D / 2.0)]
        fill_poly(px, roof, flat(step(top_c, 1)), Wd, Ht)
    else:
        top = [bx.p(0, H, 0), bx.p(W, H, 0), bx.p(W, H, D), bx.p(0, H, D)]
        fill_poly(px, top, flat(step(fr, 1)), Wd, Ht)
    # the front face, with its print sheared on
    front = [bx.p(0, 0, 0), bx.p(W, 0, 0), bx.p(W, H, 0), bx.p(0, H, 0)]
    ul, vt = bx.p(0, H, 0)                                 # the front's top-left corner
    fw = int(round(W * math.cos(THETA) * S))
    fh = int(round(H * math.cos(PHI) * S))
    slope = math.tan(THETA) * math.sin(PHI)                # the right end drops this much per pixel
    HP = print_hand(cid, spec, fw, fh) if S >= 3 else None
    plate = HP.px if HP else print_plate(spec, fw, fh, S)

    rigid_rows = 0 if HP else int(10 * S)                  # the band the lettering lives in
    mid_off = int(round(slope * (fw / 2.0)))

    def front_colour(x, y):
        fx = x - int(math.floor(ul))
        off = int(round(slope * (x + 0.5 - ul)))
        fy = y - int(math.floor(vt)) - off
        if 0 <= fy < rigid_rows:
            # A LETTER IS NOT SHEARED: a glyph bent a row across its own width stops being the
            # letter ("D" read as "b"), so the lettering band takes the face's middle offset
            fy = y - int(math.floor(vt)) - mid_off
        c = plate.get((max(0, min(fw - 1, fx)), max(0, min(fh - 1, fy))))
        if c is None:
            return fr
        if fx <= 0:
            return step(c, 1)                              # the lit crease down the left edge
        return c
    fill_poly(px, front, front_colour, Wd, Ht)
    if HP:
        on_front = {}
        fill_poly(on_front, front, lambda x, y: (0, 0, 0), Wd, Ht)
        fx0 = int(math.floor(ul)); fy0 = int(math.floor(vt))
        for (x, y), (c, g) in HP.fg.items():
            gx = fx0 + HP.gc[g]
            sx = fx0 + x
            sy = fy0 + y + int(round(slope * (gx + 0.5 - ul)))
            if (sx, sy) in on_front:
                px[sx, sy] = tuple(c[:3]) + (255,)
    # THE CLOSURE IS THE HIGHEST THING (the game pours from the widest run of the top opaque row).
    # The cap stands on the top face near its back-left corner - the top's highest part - and rises
    # above it; open (the hand carton), the cap is off: a short white spout neck, a dark mouth,
    # its far inner wall lit, as process.py's open_carton draws it.
    if spec['kind'] == 'gable':
        base = (W * 0.34, H + G * 0.55, D * 0.275)
    else:
        base = (W * CAP_AT[0], H, D * CAP_AT[1])
    ccx, ccy = bx.p(*base)                                 # the cap's foot on the top
    r = CAP_R * S
    eh = r * math.sin(PHI) * 1.9                           # an ellipse 30% of its width tall
    ch = (SPOUT_H if open else CAP_H) * math.cos(PHI) * S  # how far it rises
    tyc = ccy - ch                                         # the top ellipse's centre
    for y in range(int(tyc - eh) - 1, int(ccy + eh) + 2):
        for x in range(int(ccx - r) - 1, int(ccx + r) + 2):
            dx = (x + 0.5 - ccx) / r
            if abs(dx) > 1:
                continue
            dyt = (y + 0.5 - tyc) / eh
            in_top = dx * dx + dyt * dyt <= 1
            in_side = tyc <= y + 0.5 <= ccy + eh * math.sqrt(max(0.0, 1 - dx * dx))
            if not (in_top or in_side):
                continue
            if in_top:
                if open:
                    inner = dx * dx / 0.5 + dyt * dyt / 0.5 <= 1
                    if inner:
                        # the mouth: dark, its far wall a lit rim of Graphite
                        c = rp('Graphite', 2) if dyt < -0.35 else rp('Night', 1)
                    else:
                        c = rp('Cream', 4 if dx < 0.2 else 3)
                else:
                    c = rp('Cream', 4 if dx < 0.1 else 3)
            else:
                c = rp('Cream', 3 if dx < -0.2 else 2)
                if open and abs(dx) < 1 and ((x // max(1, S)) % 2 == 0):
                    c = rp('Cream', 3 if dx < 0 else 2)    # the spout's thread
            px[x, y] = c + (255,)
    # the ring
    pts = [(x, y) for y in range(Ht) for x in range(Wd) if not px[x, y][3] and any(
        0 <= x + dx < Wd and 0 <= y + dy < Ht and px[x + dx, y + dy][3]
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))]
    for x, y in pts:
        px[x, y] = INK + (255,)
    return im


def run():
    made = {}
    for cid in CARTONS:
        d = os.path.join(OUT, cid)
        os.makedirs(d, exist_ok=True)
        # THE HAND CARTON IS THE OPEN ONE: the bench shows v4_<id>.png for the whole pour and nothing
        # loads an _open file (swept 2026-09-05; one would also fail the plate-set test). The cellar
        # copy keeps its cap.
        for S, name, op in ((1, 'v4_%s_c.png', False), (3, 'v4_%s.png', True)):
            im = draw(cid, S, op)
            im.save(os.path.join(d, name % cid))
            p = im.load()
            off = sum(1 for y in range(im.height) for x in range(im.width)
                      if p[x, y][3] and palette.nearest(p[x, y][:3]) != p[x, y][:3])
            made['%s@%d%s' % (cid, S, '_open' if op else '')] = {'off_palette': off, 'bbox': im.getbbox()}
    with open(os.path.join(OUT, 'made_cartons.json'), 'w') as f:
        json.dump(made, f, indent=1)
    return made


if __name__ == '__main__':
    m = run()
    print(len(m), 'drawn;', sum(v['off_palette'] for v in m.values()), 'off-palette pixels')
