# -*- coding: utf-8 -*-
"""THE SQUARE BOTTLES AS BOXES (round seven, 2026-09-27): "dörtgen alkol şişelerini kutu meyve suları
açısında ve perspektifinde tekrar üret".

A square bottle was drawn as the round ones are — a silhouette seen straight on, a flat face between two
bevels — so beside the cartons (a box turned 20 degrees to the left, seen from 17 above: shelf_carton.Box)
it read as a flat card. Here it is the same box in glass: the front face turned left and lit, the right
side face a strip in shade, the shoulder seen from above (a flat top with rounded edges, or a slope for
the Tennessee bottle), the neck rising from the middle of the top. The glass is see-through, so the
drink shows through both faces (the mask is the whole cavity) and an empty bottle shows its far edges
and its floor on the back plate. The label is printed on the front face and sheared with it; its words
and figure keep their rows (hand_label).

Writes the same three plates at both sizes as shelf_style does, into staging/shelf_style/<id>/.
"""
import math
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import fontpx          # noqa: E402
import hand_label as hl  # noqa: E402
import palette         # noqa: E402
import shelf_style as ss  # noqa: E402
from shelf_carton import THETA, PHI, fill_poly  # noqa: E402

INK = palette.INK
rp = ss.rp

# W along the front, D back, in cellar units; the body's height is solved from the card's ht.
#   shoulder: round (a flat top with a rounded edge sh deep) | slope (a straight pyramid to the neck)
BOX = {
    'vodka_okhta':        dict(W=13, D=9,  neck=7,  sh=2, shoulder='round', nw=6),     # Belvedere's block
    'bourbon_redline':    dict(W=16, D=16, neck=9,  sh=3, shoulder='round', nw=8),     # the walker's square
    'bourbon_old_harrow': dict(W=15, D=15, neck=13, sh=6, shoulder='slope', nw=8,
                               flutes=True, necklabel=('Night1', 'Cream3')),          # the Tennessee square
    'bourbon_ashfall':    dict(W=16, D=13, neck=8,  sh=5, shoulder='round', nw=8),     # Maker's
    'tequila_sol_viejo':  dict(W=18, D=12, neck=10, sh=3, shoulder='round', nw=8),     # Don Julio's squat
    'syrup_house':        dict(W=11, D=9,  neck=8,  sh=3, shoulder='round', nw=6),     # a Monin
}


class Proj:
    """shelf_carton.Box's projection, with the whole bottle (body, shoulder, neck) centred."""

    def __init__(self, W, D, S, cx, foot):
        c, s = math.cos(THETA), math.sin(THETA)
        self.c, self.s, self.S = c, s, S
        self.u0 = cx - (W * c + D * s) * S / 2.0
        self.v0 = foot + 1 - W * s * math.sin(PHI) * S

    def p(self, x, y, z):
        X = x * self.c + z * self.s
        Zd = -x * self.s + z * self.c
        return self.u0 + X * self.S, self.v0 + (-y * math.cos(PHI) - Zd * math.sin(PHI)) * self.S


class BoxShape:
    """What shelf_style's closures and mouth read off a Shape: spans, the top row, the canvas."""

    def __init__(self, W_, H_, S, spans, top):
        self.W, self.H, self.S = W_, H_, S
        self.cx2 = W_
        self.spans = spans
        self.top = top
        self.bot = max(spans)
        self.colbot = {}
        for y, (a, b) in spans.items():
            for x in range(a, b + 1):
                self.colbot[x] = max(self.colbot.get(x, y), y)

    def inside(self, x, y):
        s = self.spans.get(y)
        return bool(s) and s[0] <= x <= s[1]

    def width(self, y):
        s = self.spans.get(y)
        return s[1] - s[0] + 1 if s else 0


def geometry(cid, S, k=1.0):
    """The box's dimensions at scale k (the hand's), its body height solved so the drawn bottle, cap
    included, stands the card's ht (x k) rows."""
    b = dict(BOX[cid])
    for key in ('W', 'D', 'neck', 'sh', 'nw'):
        b[key] = b[key] * k
    ht = ss.SPEC[cid]['ht'] * k                            # the cap stands inside it, as in shelf_style
    c, s = math.cos(THETA), math.sin(THETA)
    # height = the nearest bottom corner's drop + the body/shoulder/neck risen at the neck's depth + the rim
    zc = -b['W'] / 2.0 * s + b['D'] / 2.0 * c
    extra = b['W'] * s * math.sin(PHI) + zc * math.sin(PHI) + 0.15 * b['nw']
    b['Hb'] = max(8.0, (ht - extra) / math.cos(PHI) - b['neck'] - b['sh'])
    return b


def draw(cid, S=1, open_mouth=False, k=1.0):
    b = geometry(cid, S, k)
    W, D, Hb, Sh, neck, nw = b['W'], b['D'], b['Hb'], b['sh'], b['neck'], b['nw']
    g = ss.GLASS[ss.GLASS_OF[cid]]
    kind, cramp = ss.CLOSURE[cid]
    Wd, Ht = ss.W0 * S, ss.H0 * S
    back = Image.new('RGBA', (Wd, Ht), (0, 0, 0, 0)); bp = back.load()
    mask = Image.new('RGBA', (Wd, Ht), (0, 0, 0, 0)); mp = mask.load()
    front = Image.new('RGBA', (Wd, Ht), (0, 0, 0, 0)); fp = front.load()
    pj = Proj(W, D, S, Wd / 2.0, Ht - 2)
    P = pj.p
    face = {}

    def paint(poly, name):
        tmp = {}
        fill_poly(tmp, poly, lambda x, y: (0, 0, 0), Wd, Ht)
        for q in tmp:
            face[q] = name

    # the body: the right side, then the front
    paint([P(W, 0, 0), P(W, 0, D), P(W, Hb, D), P(W, Hb, 0)], 'side')
    paint([P(0, 0, 0), P(W, 0, 0), P(W, Hb, 0), P(0, Hb, 0)], 'front')
    # the shoulder, level by level: each cross-section filled as top, the front and right bands over it
    cx, cz, n = W / 2.0, D / 2.0, nw / 2.0
    levels = []
    steps = max(2, int(round(Sh * S)))
    for i in range(steps + 1):
        t = i / float(steps)
        if b['shoulder'] == 'slope':
            levels.append((Hb + Sh * t, cx - (cx - n) * t, cz - (cz - n) * t))
        else:
            a = t * math.pi / 2
            levels.append((Hb + Sh * math.sin(a), cx - Sh * (1 - math.cos(a)), cz - Sh * (1 - math.cos(a))))
    for (y0, hx0, hz0), (y1, hx1, hz1) in zip(levels, levels[1:]):
        paint([P(cx - hx1, y1, cz - hz1), P(cx + hx1, y1, cz - hz1), P(cx + hx1, y1, cz + hz1),
               P(cx - hx1, y1, cz + hz1)], 'top')
        paint([P(cx + hx0, y0, cz - hz0), P(cx + hx0, y0, cz + hz0), P(cx + hx1, y1, cz + hz1),
               P(cx + hx1, y1, cz - hz1)], 'sside')
        paint([P(cx - hx0, y0, cz - hz0), P(cx + hx0, y0, cz - hz0), P(cx + hx1, y1, cz - hz1),
               P(cx - hx1, y1, cz - hz1)], 'sfront')
    yt = Hb + Sh
    paint([P(cx - levels[-1][1], yt, cz - levels[-1][2]), P(cx + levels[-1][1], yt, cz - levels[-1][2]),
           P(cx + levels[-1][1], yt, cz + levels[-1][2]), P(cx - levels[-1][1], yt, cz + levels[-1][2])], 'top')
    # the neck: a cylinder from the top's middle, as wide as it is round, its far rim the highest row
    ncu, nbv = P(cx, yt, cz)
    _, ntv = P(cx, yt + neck, cz)
    nwp = 2 * int(round(nw * S / 2.0))
    eh = max(1.0, 0.15 * nwp)
    x0n = int(round(Wd / 2.0 - nwp / 2.0))
    top = int(round(ntv - eh))
    for y in range(top, int(round(nbv)) + 1):
        for x in range(x0n, x0n + nwp):
            face[(x, y)] = 'neck'

    neck_bands = ss.bands(nwp, False, S)

    # spans and feet
    spans = {}
    for (x, y) in face:
        a, bb = spans.get(y, (x, x))
        spans[y] = (min(a, x), max(bb, x))
    sh = BoxShape(Wd, Ht, S, spans, top)
    foot_t = 2 * S
    left_u = int(math.floor(P(0, 0, 0)[0]))
    corner_u = P(W, 0, 0)[0]
    fw = corner_u - left_u
    hi_band = (left_u + fw * 0.10, left_u + fw * 0.10 + max(1, int(round(fw * 0.06))))

    # 1. the glass
    for (x, y), f in face.items():
        s_ = spans[y]
        bnd = neck_bands[x - x0n] if f == 'neck' else None
        # the foot: two units of thick glass under the cavity
        if f in ('front', 'side') and y > sh.colbot[x] - foot_t:
            d_, m_, l_ = g['foot']
            fp[x, y] = (l_ if (f == 'front' and hi_band[0] <= x < hi_band[1]) else
                        d_ if (y == sh.colbot[x] or f == 'side') else m_) + (255,)
            continue
        if x < s_[0] + S or x > s_[1] - S:
            fp[x, y] = (g['rim'][0] if x < s_[0] + S else g['rrim'][0]) + (255,)
            continue
        if f == 'neck' and bnd in ('rim', 'rrim') and y < int(round(nbv - eh)):
            fp[x, y] = g[bnd][0] + (255,)
            continue
        # the cavity: the back plate is the empty inside, darker at the side and the walls
        mp[x, y] = (255, 255, 255, 255)
        bc = {'front': g['back'][2], 'side': g['back'][0], 'sfront': g['back'][2], 'sside': g['back'][1],
              'top': g['back'][2], 'neck': g['back'][1]}[f]
        bp[x, y] = bc + (255,)
        if f == 'front':
            if hi_band[0] <= x < hi_band[1]:
                colr, a = g['hi']
            elif hi_band[1] <= x < hi_band[1] + S:
                colr, a = g['hi'][0], 150
            else:
                colr, a = g['lite']
        elif f == 'side':
            colr, a = g['shade']
        elif f == 'sfront':
            # the shoulder in the glass's own light edge, not the highlight's cream (round seven b: the top
            # of a clear box read white under a grey neck, a blue one cream)
            # the lit shoulder: lighter than the walls under it, not white (hand); the cellar as the author saw it
            colr, a = ((g['hi'][0], 200) if hi_band[0] <= x < hi_band[1] else (g['hi'][0], 120)) if S >= 3 \
                else (g['hi'][0], 200)
        elif f == 'sside':
            colr, a = g['mid'][0], max(110, g['mid'][1])
        elif f == 'top':
            colr, a = (g['hi'][0], 100) if S >= 3 else (g['hi'][0], 150)
        else:
            colr, a = (g['hi'] if bnd == 'hi' else (g['hi'][0], 150) if bnd == 'hi2' else
                       (g['rim'][0], 150) if bnd == 'wall' else g.get(bnd, g['mid']))
        fp[x, y] = colr + (a,)

    # 2. the edges: the near corner catches the light; the body's top edges are lit; far ones behind
    def line(a, b_, col, alpha=255, plate=None, width=1):
        (ax, ay), (bx, by) = a, b_
        nn = int(max(abs(bx - ax), abs(by - ay))) + 1
        for i in range(nn + 1):
            t = i / float(nn)
            x = int(math.floor(ax + (bx - ax) * t)); y = int(math.floor(ay + (by - ay) * t))
            for w_ in range(width):
                q = (x + w_, y)
                if q in face:
                    if plate is None:
                        if not (face[q] in ('front', 'side') and y > sh.colbot[q[0]] - foot_t):
                            fp[q] = col + (alpha,)
                    else:
                        if bp[q][3]:
                            plate[q] = col + (255,)
    ew = max(1, S // 2 + (1 if S > 1 else 0))
    line(P(W, 0, 0), P(W, Hb, 0), g['hi'][0], 235, width=ew)                  # the near corner
    line(P(0, Hb, 0), P(W, Hb, 0), g['hi'][0], 220)                            # the top's near edges
    line(P(W, Hb, 0), P(W, Hb, D), g['hi'][0], 180)
    # seen through the glass (back plate): the far vertical edge and the floor's far edges
    far = ss.ramp_of(g['back'][2]); farc = rp(far[0], min(4, far[1] + 1))
    line(P(0, 0, D), P(0, Hb, D), farc, plate=bp)
    line(P(0, 0, D), P(W, 0, D), farc, plate=bp)
    line(P(0, 0, 0), P(0, 0, D), farc, plate=bp)

    # 3. what the glass is made with
    if b.get('flutes'):
        for (x, y), f in face.items():
            if f in ('sfront', 'sside') and mp[x, y][3]:
                k_ = int((x - Wd / 2.0) // (2 * S))
                fp[x, y] = (g['hi'][0] + (190,)) if k_ % 2 == 0 else (g['shade'][0] + (170,))
    if b.get('necklabel'):
        paper_c, bar_c = (ss.col(c_) for c_ in b['necklabel'])
        pr_, pi_ = ss.ramp_of(paper_c)
        crow_ = int(round(ss.cap_rows(cid) * k * S))       # where the capped copy puts it, open or not
        yl0 = top + crow_ + max(S, int((nbv - top - crow_) // 3))
        for y in range(yl0, yl0 + 3 * S):
            s_ = spans.get(y)
            if not s_:
                continue
            bl = ss.bands(s_[1] - s_[0] + 1, False, S)
            for i, x in enumerate(range(s_[0], s_[1] + 1)):
                if face.get((x, y)) != 'neck':
                    continue
                if yl0 + S <= y < yl0 + 2 * S and s_[0] < x < s_[1]:
                    c_ = bar_c
                else:
                    c_ = rp(pr_, max(1 if pr_ == 'Night' else 0, min(4, pi_ + (0 if bl[i] in ('hi', 'lite', 'hi2') else -1))))
                fp[x, y] = c_ + (255,)

    # 4. the closure or the open mouth
    crow = 0 if (open_mouth or kind in ('codd', 'open')) else int(round(ss.cap_rows(cid) * k * S))
    for y in range(top, top + crow):
        s_ = spans.get(y)
        if s_:
            for x in range(s_[0], s_[1] + 1):
                fp[x, y] = (0, 0, 0, 0); bp[x, y] = (0, 0, 0, 0); mp[x, y] = (0, 0, 0, 0)
    if open_mouth:
        ss.draw_finish(sh, cid, g, fp, bp, mp, int(round(ss.cap_rows(cid) * k * S)))
    elif kind in ('codd', 'open'):
        ss.draw_mouth(sh, g, fp, bp, mp)
    else:
        ss.draw_closure(sh, cid, kind, cramp, crow, fp, bp, mp)

    # 5. the label on the front face
    slope = math.tan(THETA) * math.sin(PHI)
    if S >= 3 and hl.has(cid):
        c = FaceCtx(cid, sh, fp, bp, mp, g, face, left_u, fw, P(0, Hb, 0)[1], P(0, 0, 0)[1], slope, foot_t)
        hl.HAND[cid](c)
        c.press()
    else:
        cellar_label(cid, sh, fp, face, left_u, fw, P(0, Hb, 0)[1], P(0, 0, 0)[1], slope, S)

    # 6. the game's contract and the ring (as shelf_style.draw)
    for x in range(Wd):
        ys = [y for y in range(Ht) if mp[x, y][3]]
        if not ys:
            continue
        y = ys[-1] + 1
        if y < Ht and fp[x, y][3] and 0.299 * fp[x, y][0] + 0.587 * fp[x, y][1] + 0.114 * fp[x, y][2] >= 40:
            fp[x, y] = rp('Night', 1) + (255,)
    floor = 30 if S > 1 else 72
    for y in range(Ht):
        for x in range(Wd):
            if 0 < fp[x, y][3] < floor:
                fp[x, y] = fp[x, y][:3] + (floor,)
    ring = [(x, y) for y in range(Ht) for x in range(Wd) if not fp[x, y][3] and any(
        0 <= x + dx < Wd and 0 <= y + dy < Ht and fp[x + dx, y + dy][3]
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))]
    for x, y in ring:
        fp[x, y] = INK + (255,)
    for y in range(Ht):
        for x in range(Wd):
            if not fp[x, y][3]:
                bp[x, y] = (0, 0, 0, 0); mp[x, y] = (0, 0, 0, 0)
    return back, mask, front, sh


class FaceCtx(hl.Ctx):
    """hand_label's Ctx for a flat face: the plate is sheared with the face, not bowed; no shading."""

    def __init__(self, cid, sh, fp, bp, mp, glass, face, left, fw, top_v, bot_v, slope, foot_t):
        self.cid, self.sh, self.fp, self.bp, self.mp, self.g = cid, sh, fp, bp, mp, glass
        self.face, self.left, self.fw, self.slope = face, left, fw, slope
        S = sh.S
        self.xc = left + fw / 2.0
        self.y_lo = int(math.ceil(top_v)) + 3 * S
        self.y_hi = int(math.floor(bot_v)) - foot_t - 3 * S
        self.plates = []

    def half(self, y):
        return self.fw / 2.0

    def label(self, kind='rect', wf=0.8, h=None, hf=None, at=0.5, y0=None, r=0, follow=True, bow=True,
              mode='paper', shade=True, w=None, slant=0.0, **kw):
        span = self.y_hi - self.y_lo + 1
        if h is None:
            h = int(round((hf or 0.5) * span))
        h = max(8, min(h, span))
        if y0 is None:
            c = self.y_lo + (span - 1) * at
            y0 = max(self.y_lo, min(self.y_hi - h + 1, int(round(c - h / 2.0))))
        Wl = 2 * int(round((w if w else self.fw * wf) / 2.0))
        x0 = int(round(self.xc - Wl / 2.0))
        P = hl.Plate(Wl, h, None, mode)
        P.form(kind, r, **kw)
        self.plates.append((P, x0, y0, slant, None, False))
        return P

    def press(self):
        sh, fp = self.sh, self.fp
        for P, x0, y0, slant, _, _ in self.plates:
            def off(bx):
                return int(round(self.slope * (bx + 0.5 - self.left) - slant * (bx + 0.5 - self.xc)))
            items = [((x, y), c, None) for (x, y), c in P.px.items()] + \
                    [((x, y), c, g) for (x, y), (c, g) in P.fg.items()]
            low = {}
            for (x, y), c, g in items:
                bx = x0 + x
                by = y0 + y + (off(int(round(x0 + P.gc[g]))) if g else off(bx))
                if self.face.get((bx, by)) != 'front':
                    continue
                if len(c) == 4:
                    fp[bx, by] = c
                    continue
                fp[bx, by] = tuple(c[:3]) + (255,)
                if P.mode == 'paper' and (x, y) in P.shape:
                    low[bx] = max(low.get(bx, -1), by)
            for bx, by in low.items():
                y = by + 1
                if self.face.get((bx, y)) == 'front' and fp[bx, y][3] < 255 and self.mp[bx, y][3]:
                    fp[bx, y] = self.g['shade'][0] + (min(255, max(fp[bx, y][3], 150)),)


def cellar_label(cid, sh, fp, face, left, fw, top_v, bot_v, slope, S):
    """The room's label on the front face: paper, a border, the mark, a bar — sheared with the face."""
    mark, paper, border, ink, accent, bar = ss.LABELS[cid]
    art = ss.ICONS[mark]
    ih, iw = len(art), len(art[0])
    paper, border, ink, accent, bar = (ss.col(c) for c in (paper, border, ink, accent, bar))
    shape = ss.SPEC[cid].get('label', 'rect')
    w = int(fw) - (1 if fw >= 12 else 0)
    h = ih + 6
    y_lo = int(math.ceil(top_v)) + 2
    y_hi = int(math.floor(bot_v)) - 3
    at = {'high': 0.3, 'tall': 0.5}.get(shape, 0.55)
    if shape == 'tall':
        h = min(y_hi - y_lo + 1, ih + 12)
    y0 = max(y_lo, min(y_hi - h + 1, int(round(y_lo + (y_hi - y_lo) * at - h / 2.0))))
    x0 = int(round(left + (fw - w) / 2.0))
    sl = 0.2 if shape == 'slant' else 0.0
    xc = x0 + w / 2.0

    def off(x):
        return int(round(slope * (x + 0.5 - left) - sl * (x + 0.5 - xc)))
    cells = {}
    for yy in range(h):
        for xx in range(w):
            corner = (xx in (0, w - 1)) and (yy in (0, h - 1)) and shape != 'torn'
            if corner:
                continue
            if shape == 'torn' and yy == 0 and xx % 3 == 1:
                continue
            edge = xx in (0, w - 1) or yy in (0, h - 1)
            cells[(xx, yy)] = border if edge else paper
    mx0 = (w - iw) // 2
    for r, line in enumerate(art):
        for q, ch in enumerate(line):
            if ch != '.':
                cells[(mx0 + q, 2 + r)] = ink if ch == 'k' else accent if ch == 'a' else rp('Cream', 4)
    for xx in range(2, w - 2):
        cells[(xx, 3 + ih)] = bar
    mid = x0 + w // 2
    for (xx, yy), c in cells.items():
        x = x0 + xx
        y = y0 + yy + (off(mid) if 2 <= yy < 2 + ih else off(x))     # the mark keeps its rows
        if face.get((x, y)) == 'front':
            fp[x, y] = tuple(c) + (255,)


def draw_hand(cid):
    k = min(ss.HAND_H.get(cid, 172), 186) / (ss.SPEC[cid]['ht'] * 3.0)
    for _ in range(6):
        out = draw(cid, S=3, open_mouth=cid not in ss.KEEP_CLOSED, k=k)
        bb = out[2].getbbox()
        if bb[2] - bb[0] <= ss.HAND_MAX_W and bb[1] >= 1:
            return out
        k *= min((ss.HAND_MAX_W - 1) / float(bb[2] - bb[0]), 1.0 if bb[1] >= 1 else 0.97)
    return out


def has(cid):
    return cid in BOX
