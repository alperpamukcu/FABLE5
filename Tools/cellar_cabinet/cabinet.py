# -*- coding: utf-8 -*-
"""The cellar under the counter as a DISPLAY CABINET, drawn in the palette - one body per level of the counter.

Run:  py -3 -X utf8 Tools/cellar_cabinet/cabinet.py [--out DIR]
      py -3 -X utf8 Tools/cellar_cabinet/cabinet.py --preview DIR --assets "<a checkout with the real PNGs>"

WHY (2026-09-28, the author picked direction C of the cellar mock-ups: "Mahzen tasarımı = C · Art Deco vitrin"). The
cellar stops being the counter drawing's three bays behind two magenta boards and becomes a Miami deco display cabinet:
ten fixed niches, five across and two high, ONE family a niche, mirrored about a wider stepped centre column. Every
niche wears a name plate in its top edge (the word itself is HUD text, never baked), and under the plate the lamp that
lights it. The same layout comes in three materials, one per CounterTier - the niches never move, so the hand's memory
survives an upgrade; only what they are made of changes:

    1  plywood        honey plywood carcass (Malt), painted backs, raw end grain in the arch reveals, magenta trim
                      only as a pinstripe ("magentalık değişmeli" - the level the bar opens with is not solid pink)
    2  lacquer        black lacquer (Graphite), reeded berry backs, neon piping round every arch
    3  mirror + gold  navy lacquer (ClubBlue), smoky mirror backs, gold flutes, gold arches, a gold sunburst

The critic's three fixes (2026-09-28) are part of the drawing: the name plates are cream enamel when lit and Night
enamel when not - never amber, which is the money's and the primary key's colour; level 1 is wood, not magenta; and
the body stops at art row 249, the counter drawing's own last row. The author's slab (rows 0..64 of counter.png) and
the author's door (counter_door / counter_door_worn) are not touched - the body starts under the slab and the door
drops over it exactly as before.

WHAT IT WRITES (Assets/Resources/Scene, PPU 1 world art by the folder's import rule):

    cabinet_L{1,2,3}_pilot.png   638 x 185, counter-art rows 65..249: the whole body, every niche EMPTY - its pilot
                                 glow (one narrow band under the lamp) and a Night enamel plate
    cabinet_L{1,2,3}_lit.png     the same canvas, TRANSPARENT but for the ten openings, each drawn LIT - the wall-wash
                                 scallop from the lamp, the plate in cream enamel. The stage draws the pilot body lit
                                 by the room and lays each stocked niche's rectangle of this one over it UNLIT: it is
                                 the lamp's light, so it draws the colour it was made in.

Only the openings, not the whole rectangle (2026-09-28, second pass): the room washes the counter layer at 0.45, so a
rectangle drawn unlit carried its niche's rounded or stepped top corners - carcass - at twice the brightness of the
carcass beside them, a lit box printed over the wood. The overlay stops at the opening's own edge and the frame
around it stays the room's. Everything it covers lies inside the ten niche rectangles (LastCall.Game.CellarCabinet:
NicheX/NicheW, OpenTop..LipBottom) - the EditMode suite holds the art to that.

REFINISH (CounterFinish.RecolourCabinet): each level draws its CARCASS in a family of its own that appears nowhere else
in that level - Malt[0..3] in 1, Graphite[0..2] in 2, ClubBlue[0..1] in 3 - so the refinish kit can repaint the
carcass by luminance rank without touching a niche. Change a level's carcass colours and CounterFinish.CabinetFamily
must change with them.

Deterministic, palette-only (the 55 of Tools/v4_bottles/palette.py; the run fails on one stray pixel), and nothing is
generated: every pixel is placed here.
"""
import argparse
import hashlib
import math
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'v4_bottles'))
import palette as P  # noqa: E402


def T(ramp, i):
    return P.ramp(ramp, i) + (255,)


N = [T('Night', i) for i in range(5)]
M = [T('Magenta', i) for i in range(5)]
CY = [T('Cyan', i) for i in range(5)]
AM = [T('Amber', i) for i in range(5)]
CB = [T('ClubBlue', i) for i in range(5)]
CR = [T('Cream', i) for i in range(5)]
MA = [T('Malt', i) for i in range(5)]
GR = [T('Graphite', i) for i in range(5)]
CLEAR = (0, 0, 0, 0)

# ── geometry, in counter-art px (1 art px = 1 stage unit = 2 screen px at 720p) ───────────────────────────────────
# MIRRORED IN C#: LastCall.Game.CellarCabinet carries every number below that the game places anything by.
ART_W = 638
TOP, BOTTOM = 65, 249               # the body's rows: under the author's slab, down to the drawing's last row
H = BOTTOM - TOP + 1                # 185
LEFT, RIGHT = 4, 633                # the carcass's outer columns (native-centred, it covers the old body's 6..631)
OUTER = [(4, 19), (615, 19)]        # the two end posts, x0 and width
NICHE_X = [(23, 106), (139, 106), (255, 128), (393, 106), (509, 106)]   # the door covers 23..614 exactly
PILASTERS = [(129, 10), (245, 10), (383, 10), (499, 10)]
ROWS = {
    # open_top: the opening's first row; plate: the name plate's rows; slot: the lamp's row under it (the bottle's
    # top is the row after); feet: the row a bottle's foot stands ON (DiegeticStage's CellarShelfFootPx, unchanged);
    # floor: the board seen in depth, back edge to front; lip: the board's front edge
    0: dict(open_top=66, plate=(67, 77), slot=78, feet=143, floor=(138, 146), lip=(147, 148)),
    1: dict(open_top=156, plate=(157, 167), slot=168, feet=233, floor=(228, 236), lip=(237, 238)),
}
SILL = (149, 155)                   # the carcass's band between the two rows
PLINTH = 239                        # the carcass from here to BOTTOM (behind the door's rail once it has dropped)
PLATE_W = [72, 72, 88, 72, 72]
# the top corners of each opening: the two ends are a Miami hotel's streamline rounds, the inner wings soft, and the
# centre a stepped tower (three 4 x 3 steps a side)
CORNER_R = {0: (14, 4), 1: (4, 4), 3: (4, 4), 4: (4, 14)}
CENTRE = 2


def niche_rect(b, col):
    """(x0, y0, x1, y1) inclusive, counter-art px: everything the lit and the pilot bodies may differ in."""
    x0, w = NICHE_X[col]
    r = ROWS[b]
    return x0, r['open_top'], x0 + w - 1, r['lip'][1]


# ── the three materials ───────────────────────────────────────────────────────────────────────────────────────────
# face/hi/sh: the carcass (ONLY these, plus 'board' and the plate's wood/lacquer where noted, are the level's refinish
# family - see the docstring). back: the niche's back wall by light band 0 dark .. 3 core (+1 spare step up);
# ret: the side returns; floor: the board; lip: (top row, second row) unlit / lit.
LEVELS = {
    1: dict(name='plywood',
            face=MA[1], hi=MA[2], sh=MA[0],
            back=[N[1], N[2], N[3], N[4], CR[0]],
            ret=[N[0], N[1], N[2], N[2]],
            floor=[MA[0], MA[1], MA[2], MA[3]],
            lip=[(MA[2], MA[1]), (MA[3], MA[1])],
            trim=M[2], edge='ply',
            plate_frame=(MA[0], MA[2], MA[0])),
    2: dict(name='lacquer',
            face=GR[1], hi=GR[2], sh=GR[0],
            back=[N[1], N[2], M[0], M[1], M[2]],
            ret=[N[0], N[1], N[2], N[2]],
            floor=[N[1], N[2], N[3], N[3]],
            lip=[(N[3], N[1]), (N[4], M[1])],
            trim=M[2], edge='neon',
            plate_frame=(N[0], M[2], N[0])),
    3: dict(name='mirror',
            face=CB[0], hi=CB[1], sh=N[0],
            back=[GR[1], GR[2], GR[3], GR[4], CR[3]],
            ret=[N[0], N[1], GR[1], GR[2]],
            floor=[N[1], N[2], N[3], N[3]],
            lip=[(AM[2], MA[1]), (AM[3], AM[1])],
            trim=AM[3], edge='gold',
            plate_frame=(MA[1], AM[3], MA[1])),
}

# the refinish families, the one list CounterFinish.CabinetFamily mirrors
FAMILY = {1: [MA[0], MA[1], MA[2], MA[3]], 2: [GR[0], GR[1], GR[2]], 3: [CB[0], CB[1]]}


class Canvas:
    """RGBA canvas in counter-art coordinates: x is the art column, y the art row (TOP is local row 0)."""

    def __init__(self):
        self.a = np.zeros((H, ART_W, 4), np.uint8)

    def rect(self, x0, y0, x1, y1, c):
        x0, x1 = max(0, x0), min(ART_W - 1, x1)
        y0, y1 = max(TOP, y0), min(BOTTOM, y1)
        if x0 <= x1 and y0 <= y1:
            self.a[y0 - TOP:y1 - TOP + 1, x0:x1 + 1] = c

    def px(self, x, y, c):
        if 0 <= x < ART_W and TOP <= y <= BOTTOM:
            self.a[y - TOP, x] = c

    def get(self, x, y):
        return tuple(self.a[y - TOP, x])

    def hline(self, x0, x1, y, c):
        self.rect(x0, y, x1, y, c)

    def vline(self, x, y0, y1, c):
        self.rect(x, y0, x, y1, c)


def in_round(x, y, x0, x1, top, rl, rr):
    if rl and x < x0 + rl and y < top + rl:
        cx, cy = x0 + rl - 0.5, top + rl - 0.5
        return (x - cx) ** 2 + (y - cy) ** 2 <= rl * rl
    if rr and x > x1 - rr and y < top + rr:
        cx, cy = x1 - rr + 0.5, top + rr - 0.5
        return (x - cx) ** 2 + (y - cy) ** 2 <= rr * rr
    return True


def opening_mask(b, col):
    """Bool mask (canvas rows) of one niche's opening, floor and lip included."""
    x0, w = NICHE_X[col]
    r = ROWS[b]
    top, bot = r['open_top'], r['lip'][1]
    m = np.zeros((H, ART_W), bool)
    m[top - TOP:bot - TOP + 1, x0:x0 + w] = True
    if col == CENTRE:
        for k in range(3):
            dx = 4 * (3 - k)
            for yy in range(top + 3 * k, top + 3 * k + 3):
                m[yy - TOP, x0:x0 + dx] = False
                m[yy - TOP, x0 + w - dx:x0 + w] = False
    else:
        rl, rr = CORNER_R[col]
        for yy in range(top, top + max(rl, rr)):
            for xx in range(x0, x0 + w):
                if not in_round(xx, yy, x0, x0 + w - 1, top, rl, rr):
                    m[yy - TOP, xx] = False
    return m


def rings(m, b, col, n=3):
    """Ring index 1..n of the opening's pixels by distance from the frame, over the top and the sides only (the rows
    under the opening count as open, so the rings run down to the board)."""
    x0, w = NICHE_X[col]
    bot = ROWS[b]['lip'][1] - TOP
    cur = m.copy()
    cur[bot + 1:, x0:x0 + w] = True
    ring = np.zeros(m.shape, np.int8)
    for k in range(1, n + 1):
        er = cur.copy()
        er[1:, :] &= cur[:-1, :]
        er[:-1, :] &= cur[1:, :]
        er[:, 1:] &= cur[:, :-1]
        er[:, :-1] &= cur[:, 1:]
        er[1:, 1:] &= cur[:-1, :-1]
        er[1:, :-1] &= cur[:-1, 1:]
        ring[cur & ~er & m] = k
        cur = er
    return ring


def band(dx, dy, depth, hw):
    """The lamp's wall-wash on the back: a scallop widening as the square root of the drop. 0 dark .. 3 core."""
    if dy < 1:
        return 0
    a = hw / math.sqrt(depth)
    s = math.sqrt(dy)
    ax = abs(dx)
    level = 0
    if ax <= a * s:
        level = 1
    if ax <= a * s * 0.70 and dy <= 0.92 * depth:
        level = 2
    if ax <= a * s * 0.42 and dy <= 0.62 * depth:
        level = 3
    return level


# ── the carcass ───────────────────────────────────────────────────────────────────────────────────────────────────
def carcass(cv, L, level):
    cv.rect(LEFT, TOP, RIGHT, BOTTOM, L['face'])
    posts = [OUTER[0]] + PILASTERS + [OUTER[1]]
    y0, y1 = TOP + 1, BOTTOM
    for i, (x, w) in enumerate(posts):
        outer = i in (0, len(posts) - 1)
        if level == 1:
            cv.vline(x + 1, y0, y1, L['hi'])
            cv.vline(x + w - 2, y0, y1, L['sh'])
            if outer:
                # a routed groove down the wide post, and the magenta pinstripe along its inner edge: the one
                # place the old pink survives, as trim
                g = x + w // 2
                cv.vline(g, y0, y1, L['sh'])
                cv.vline(g + 1, y0, y1, L['hi'])
                cv.vline(x + (w - 4 if i == 0 else 3), y0, y1, L['trim'])
            for yy in (72, 139, 162, 229):                         # screw heads: it is plywood
                sx = x + w // 2 - (0 if not outer else 4)
                cv.px(sx, yy, N[0])
                cv.px(sx - 1, yy - 1, L['hi'])
        elif level == 2:
            cv.vline(x + 1, y0, y1, L['hi'])
            cv.vline(x + w - 2, y0, y1, L['sh'])
            cv.vline(x + w // 2 - (0 if outer else 1), y0, y1, L['trim'])   # the pinstripe
            if outer:
                cv.vline(x + w // 2 - 4, y0, y1, L['hi'])
                cv.vline(x + w // 2 + 4, y0, y1, L['hi'])
        else:
            cv.vline(x + 1, y0, y1, AM[3])
            cv.vline(x + w - 2, y0, y1, MA[1])
            flutes = [x + 3, x + w - 4] if not outer else [x + 5, x + 9, x + 13]
            for fx in flutes:
                cv.vline(fx, y0, y1, AM[1])
            # gold capitals and bases per row
            for (ya, yb) in ((TOP + 1, TOP + 3), (144, 148), (156, 158), (234, 238)):
                cv.rect(x + 1, ya, x + w - 2, yb, AM[2])
                cv.hline(x + 1, x + w - 2, ya, AM[4])
                cv.hline(x + 1, x + w - 2, yb, MA[1])
    # the band between the rows: the upper board's shadow, a highlight, the face, and the trim line through it
    s0, s1 = SILL
    cv.hline(LEFT, RIGHT, s0, N[0])
    cv.hline(LEFT + 1, RIGHT - 1, s0 + 1, L['hi'])
    cv.hline(LEFT + 1, RIGHT - 1, s0 + 3, L['trim'])
    cv.hline(LEFT + 1, RIGHT - 1, s1, L['sh'])
    # the plinth under the lower board (behind the door's rail when it has dropped): shadow, rule, trim, base
    cv.hline(LEFT, RIGHT, PLINTH, N[0])
    cv.hline(LEFT + 1, RIGHT - 1, PLINTH + 1, L['hi'])
    cv.hline(LEFT + 1, RIGHT - 1, PLINTH + 3, L['trim'])
    cv.rect(LEFT, BOTTOM - 1, RIGHT, BOTTOM, L['sh'])
    # the slab's shadow on the frieze
    cv.hline(LEFT, RIGHT, TOP, N[0])


def back_pixel(L, level, col, x, y, x0, xc, f0, lvl):
    back = L['back']
    c = back[lvl]
    if col == CENTRE:
        # the feature's sunburst: rays from under the board's centre
        oyf = f0 + 10
        ang = math.atan2(x - xc, oyf - y)                       # 0 straight up
        if level == 3:
            d = math.hypot(x - xc, oyf - y)
            n = 11
            step = math.radians(150) / (n - 1)
            j = round((ang + math.radians(75)) / step)
            if 0 <= j < n and d * abs(math.sin(ang - (-math.radians(75) + j * step))) < 0.72:
                c = (MA[1], AM[1], AM[2], AM[3])[min(lvl, 3)]
            elif (x + y * 2) % 41 in (0, 1, 2):
                c = back[min(lvl + 1, len(back) - 1)]
        else:
            k = int(math.floor((ang + math.pi / 2) / (math.pi / 15)))
            if k % 2 == 0:
                c = back[min(lvl + 1, len(back) - 1)] if lvl > 0 else N[2] if level == 1 else N[1]
    else:
        if level == 2 and (x - x0) % 4 == 1:                    # reeded lacquer
            c = back[max(0, lvl - 1)] if lvl > 0 else N[1]
        if level == 3 and ((x + y * 2) % 41 in (0, 1, 2) or (x + y * 2) % 67 == 0):   # the mirror's streaks
            c = back[min(lvl + 1, len(back) - 1)]
    return c


def niche(cv, L, level, b, col, lit, light):
    """One niche, lit or on its pilot. `light` collects the pixels that carry the lamp's light (the back, the
    returns, the arch's edge) - the refinish may never reach one of them (check)."""
    x0, w = NICHE_X[col]
    r = ROWS[b]
    top = r['open_top']
    f0, f1 = r['floor']
    l0, l1 = r['lip']
    p0, p1 = r['plate']
    pw = PLATE_W[col]
    pl = x0 + (w - pw) // 2
    xc = x0 + (w - 1) / 2.0
    slot = r['slot']
    depth = f0 - slot
    hw = (w - 6) / 2.0 - 1
    m = opening_mask(b, col)
    a = cv.a

    for y in range(top, l1 + 1):
        for x in range(x0, x0 + w):
            if not m[y - TOP, x]:
                continue
            if y >= l0:                                         # the board's front edge
                c = L['lip'][1 if lit else 0][y - l0]
            elif y >= f0:                                       # the board, a trapezoid in depth
                inset = int(round(3 * (f1 - y) / float(f1 - f0 + 1)))
                if x < x0 + inset or x > x0 + w - 1 - inset:
                    c = L['ret'][1 if lit else 0]
                else:
                    lvl = 0
                    if lit:
                        d = abs(x - xc)
                        lvl = 2 if d <= hw * 0.55 else (1 if d <= hw * 0.98 else 0)
                    c = L['floor'][lvl]
            elif x < x0 + 3 or x > x0 + w - 4:                  # the side returns
                light[y - TOP, x] = True
                c = L['ret'][0]
                if lit and y > slot + 6:
                    c = L['ret'][1]
                if lit and y > slot + depth * 0.45:
                    c = L['ret'][2]
            else:                                               # the back
                light[y - TOP, x] = True
                if lit:
                    lvl = band(x - xc, y - slot, depth, hw)
                    if pl - 2 <= x <= pl + pw + 1 and p0 - 1 <= y <= p1 + 1:
                        lvl = max(lvl, 1)                       # the plate's own glow, one band, hugging it
                else:
                    lvl = min(1, band(x - xc, y - slot, depth * 0.55, hw * 0.42))   # the pilot
                c = back_pixel(L, level, col, x, y, x0, xc, f0, lvl)
            a[y - TOP, x] = c

    # the arch's edge, per material
    ring = rings(m, b, col)
    ys, xs = np.nonzero(ring)
    for yy, x in zip(ys, xs):
        y = yy + TOP
        k = ring[yy, x]
        if y >= l0:
            continue
        on_floor = y >= f0
        light[yy, x] = light[yy, x] or not on_floor
        if L['edge'] == 'ply':
            # the plywood's end grain in the reveal: lighter where the lamp reaches it
            if k == 1 and not on_floor:
                a[yy, x] = CR[2] if lit else CR[0]
            elif k == 2 and not on_floor and lit and (y + x) % 5 == 0:
                a[yy, x] = CR[1]
        elif L['edge'] == 'neon':
            # ONE tube and its glow, thinner than the mock's three rings ("the neon on level 2 may be too loud"); the
            # centre column's in the door's cyan, so the feature reads from across the room
            tube, glow, off = (CY[3], CY[1], CY[2]) if col == CENTRE else (M[3], M[1], M[2])
            if k == 1:
                a[yy, x] = glow if lit else N[0]
            elif k == 2:
                a[yy, x] = tube if lit else off
        else:
            if k == 1 and not on_floor:
                a[yy, x] = AM[4] if y <= top + 7 else AM[3]
            elif k == 2 and not on_floor:
                a[yy, x] = MA[1]

    # the name plate: frame, rim, enamel. CREAM when the niche is lit, NIGHT when it is not - never amber, which is
    # the money's colour and the primary key's (the critic, 2026-09-28: ten amber plates read as ten primary keys)
    fr_edge, fr_mid, fr_bot = L['plate_frame']
    glass, rim = (CR[4], CR[3]) if lit else (N[1], N[2])
    for y in range(p0, p1 + 1):
        for x in range(pl, pl + pw):
            edge_x = x in (pl, pl + pw - 1)
            if y == p0:
                c = fr_mid
            elif y == p1:
                c = fr_bot
            elif edge_x:
                c = fr_edge
            elif y in (p0 + 1, p1 - 1) or x in (pl + 1, pl + pw - 2):
                c = rim
            else:
                c = glass
            a[y - TOP, x] = c
            light[y - TOP, x] = False                           # the plate is the fitting, not the light
    # deco ears: a stepped tab each side of the plate
    for side in (-1, 1):
        ex = pl - 1 if side < 0 else pl + pw
        yc = (p0 + p1) // 2
        for k, hh in enumerate((5, 3)):
            xx = ex + side * k
            for y in range(yc - hh // 2, yc + hh // 2 + 1):
                a[y - TOP, xx] = fr_mid if k == 0 else fr_bot
                light[y - TOP, xx] = False
    # the lamp's slot under the plate: the housing's own dark lip, lit or not (the stage hangs the lamp's housing
    # sprite over it - DiegeticStage's CellarLamp - and the light comes out from under it)
    cv.hline(pl + 4, pl + pw - 5, slot, N[0])
    light[slot - TOP, pl + 4:pl + pw - 4] = False


def openings():
    """Bool mask (canvas rows) of all ten openings, floors and lips included: what the lit overlay covers."""
    m = np.zeros((H, ART_W), bool)
    for b in (0, 1):
        for col in range(5):
            m |= opening_mask(b, col)
    return m


def overlay(lit):
    """The lit body cut down to its openings: transparent everywhere the pilot body's carcass shows through."""
    out = lit.copy()
    out[~openings()] = CLEAR
    return out


def body(level, lit, light=None):
    """The whole body at one level, every niche lit or every niche on its pilot."""
    L = LEVELS[level]
    cv = Canvas()
    carcass(cv, L, level)
    if light is None:
        light = np.zeros((H, ART_W), bool)
    allopen = openings()
    for b in (0, 1):
        for col in range(5):
            niche(cv, L, level, b, col, lit, light)
    # the outline: carcass pixels touching an opening (not across the board's lip, which is the shelf's own edge)
    a = cv.a
    fr = ~allopen
    fr[:, :LEFT] = False
    fr[:, RIGHT + 1:] = False
    touch = np.zeros_like(fr)
    touch[1:, :] |= allopen[:-1, :]
    touch[:-1, :] |= allopen[1:, :]
    touch[:, 1:] |= allopen[:, :-1]
    touch[:, :-1] |= allopen[:, 1:]
    a[fr & touch] = N[0]
    # the silhouette
    cv.vline(LEFT, TOP, BOTTOM, N[0])
    cv.vline(RIGHT, TOP, BOTTOM, N[0])
    return a


# ── checks ────────────────────────────────────────────────────────────────────────────────────────────────────────
def off_palette(a):
    pal = set(P.COLOURS)
    op = a[..., 3] > 0
    return sum(1 for c in map(tuple, a[..., :3][op]) if c not in pal)


def check(level, lit, pilot, light):
    """`lit` is the whole lit body (before overlay() cuts it to the openings)."""
    assert lit.shape == (H, ART_W, 4) and pilot.shape == (H, ART_W, 4)
    assert off_palette(lit) == 0 and off_palette(pilot) == 0, 'off-palette pixels at level %d' % level
    # they differ only inside the openings - so the overlay, which is the openings, carries every difference - and
    # the openings lie inside the niche rectangles the stage cuts the overlay by
    diff = (lit != pilot).any(-1)
    allopen = openings()
    assert not (diff & ~allopen).any(), 'lit and pilot differ outside the openings at level %d' % level
    inside = np.zeros_like(diff)
    for b in (0, 1):
        for col in range(5):
            x0, y0, x1, y1 = niche_rect(b, col)
            inside[y0 - TOP:y1 - TOP + 1, x0:x1 + 1] = True
    assert not (allopen & ~inside).any(), 'an opening runs outside its niche rectangle'
    # the carcass family never lands on a pixel that carries the lamp's light, so the refinish repaints the wood,
    # the lacquer or the navy and never the light (the boards, lips and plate frames are carcass: allowed)
    fam = np.array([c[:3] for c in FAMILY[level]])
    for arr in (lit, pilot):
        cols = arr[..., :3][light]
        hit = (cols[:, None, :] == fam[None, :, :]).all(-1).any(-1)
        assert not hit.any(), 'level %d: a carcass colour on %d lit pixels' % (level, int(hit.sum()))
    # the body is opaque from LEFT to RIGHT on every row
    assert (lit[:, LEFT:RIGHT + 1, 3] == 255).all() and (pilot[:, LEFT:RIGHT + 1, 3] == 255).all()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=os.path.join(ROOT, 'Assets', 'Resources', 'Scene'))
    ap.add_argument('--preview', help='also compose 2x mock-ups into this folder (never into Assets)')
    ap.add_argument('--assets', default=ROOT, help='a checkout whose PNGs are real (not LFS pointers), for --preview')
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    bodies = {}
    for level in (1, 2, 3):
        light = np.zeros((H, ART_W), bool)
        lit, pilot = body(level, True, light), body(level, False, light)
        check(level, lit, pilot, light)
        lit = overlay(lit)
        bodies[level] = (lit, pilot)
        for name, arr in (('lit', lit), ('pilot', pilot)):
            p = os.path.join(args.out, 'cabinet_L%d_%s.png' % (level, name))
            Image.fromarray(arr).save(p, optimize=True)
            print('%-24s %dx%d  sha1 %s' % (os.path.basename(p), ART_W, H, hashlib.sha1(arr.tobytes()).hexdigest()[:12]))
    if args.preview:
        import preview
        preview.run(bodies, args.preview, args.assets)


if __name__ == '__main__':
    main()
