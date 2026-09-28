# -*- coding: utf-8 -*-
"""THE HAND BOTTLE'S LABELS (round seven, 2026-09-27).

The author, on the hand bottles: "etiketlerini tam oturt ve daha çeşit ve kompozisyon ekle, düz bir
etiket üstü şekil olmasın, desenler figürler olsun". The cellar's labels stay as they are (paper, a
border, the mark, bars for lettering: all a 32x64 bottle can carry); at the hand's size a label is a
COMPOSITION — a ground with a pattern, a frame or two, a figure, a ribbon with the brand's word, lines
of print — laid out per brand after the real label it nods to.

How a label is made:
  1. a Plate is drawn FLAT: its outline follows the body it will sit on (a cone's label narrows as the
     cone does, nothing hangs off the glass), then the ground, patterns, frames, figure and words;
  2. it is PRESSED onto the glass: every column drops by the camera's ellipse at that column (the
     17-degree pitch: a ring round a cylinder bows down in the middle by 0.29 of its radius), words and
     figures move as one piece at their own centre (a letter bent across its width stops being a
     letter), and the paper takes the body's light — the far edges turn away a step or two darker.
"""
import math
from collections import deque

import fontpx
import hand_figures
import palette

PHI = math.radians(17)
N4 = ((1, 0), (-1, 0), (0, 1), (0, -1))


def C(c):
    """A palette name ('Cream4') or an rgb tuple -> rgb."""
    if isinstance(c, str):
        return palette.ramp(c[:-1], int(c[-1]))
    return tuple(c[:3])


def ramp_of(c):
    c = tuple(c[:3])
    for name, hexes in palette.RAMPS.items():
        for i, h in enumerate(hexes):
            if palette.hex_rgb(h) == c:
                return name, i
    return ramp_of(palette.nearest(c))


def step(c, k):
    if k == 0:
        return tuple(c[:3])
    r, i = ramp_of(c)
    return palette.ramp(r, max(0, min(4, i + k)))


# ── the flat label ──────────────────────────────────────────────────────────
class Plate:
    def __init__(self, w, h, allow=None, mode='paper'):
        self.w, self.h = w, h
        self.allow = allow or (lambda y: (0, w - 1))
        self.mode = mode                   # paper | print | emboss | window
        self.shape, self.depth = set(), {}
        self.px, self.fg, self.gc = {}, {}, {}    # the ground, and the words and figures laid over it
        self.ng = 0

    # -- the outline --
    def form(self, kind='rect', r=0, **kw):
        w, h = self.w, self.h
        S = set()
        for y in range(h):
            lo, hi = self.allow(y)
            lo, hi = max(0, lo), min(w - 1, hi)
            for x in range(lo, hi + 1):
                if _inside(kind, x, y, w, h, r, lo, hi, kw):
                    S.add((x, y))
        self.shape = S
        d, q = {}, deque()
        for (x, y) in S:
            if any((x + dx, y + dy) not in S for dx, dy in N4):
                d[(x, y)] = 0
                q.append((x, y))
        while q:
            x, y = q.popleft()
            for dx, dy in N4:
                p = (x + dx, y + dy)
                if p in S and p not in d:
                    d[p] = d[(x, y)] + 1
                    q.append(p)
        self.depth = d
        return self

    def rows(self):
        ys = [y for (_, y) in self.shape]
        return (min(ys), max(ys)) if ys else (0, -1)

    def span(self, y):
        xs = [x for (x, yy) in self.shape if yy == y]
        return (min(xs), max(xs)) if xs else None

    # -- paint --
    def put(self, x, y, c, g=None, clip=True):
        if clip and (x, y) not in self.shape:
            return
        if not clip:
            lo, hi = self.allow(y) if 0 <= y < self.h else (1, 0)
            if not (0 <= y < self.h and lo <= x <= hi and 0 <= x < self.w):
                return
        col = C(c) if len(c) != 4 or isinstance(c, str) else tuple(c)
        if g:
            self.fg[(x, y)] = (col, g)
        else:
            self.px[(x, y)] = col

    def group(self, cx):
        self.ng += 1
        self.gc[self.ng] = cx
        return self.ng

    def fill(self, c, dmin=0, dmax=999):
        for p in self.shape:
            if dmin <= self.depth.get(p, 0) <= dmax:
                self.put(p[0], p[1], c)

    def ring(self, c, d0, d1=None):
        self.fill(c, d0, d0 if d1 is None else d1)

    def where(self, pred, c, dmin=0):
        for (x, y) in self.shape:
            if self.depth.get((x, y), 0) >= dmin and pred(x, y):
                self.put(x, y, c)

    def hband(self, y0, h, c, dmin=0):
        self.where(lambda x, y: y0 <= y < y0 + h, c, dmin)

    def hline(self, y, c, x0=None, x1=None, dmin=0):
        self.where(lambda x, yy: yy == y and (x0 is None or x >= x0) and (x1 is None or x <= x1), c, dmin)

    def dotline(self, y, c, gap=2, dmin=0):
        self.where(lambda x, yy: yy == y and x % gap == 0, c, dmin)

    def pat(self, kind, c, dmin=0, c2=None, **kw):
        p = kw.get('p', 4)
        if kind == 'vstripes':
            f = lambda x, y: x % p < kw.get('t', 1)
        elif kind == 'hstripes':
            f = lambda x, y: y % p < kw.get('t', 1)
        elif kind == 'diag':
            f = lambda x, y: (x + y) % p < kw.get('t', 1)
        elif kind == 'rdiag':
            f = lambda x, y: (x - y) % p < kw.get('t', 1)
        elif kind == 'cross':
            f = lambda x, y: (x + y) % p == 0 or (x - y) % p == 0
        elif kind == 'dots':
            f = lambda x, y: y % p == 0 and (x + (p // 2 if (y // p) % 2 else 0)) % p == 0
        elif kind == 'checker':
            f = lambda x, y: ((x // p) + (y // p)) % 2 == 0
        elif kind == 'waves':
            f = lambda x, y: (y + int(round(1.2 * math.sin(x * 2 * math.pi / p)))) % kw.get('gap', 5) == 0
        elif kind == 'rays':
            cx, cy, n = kw['cx'], kw['cy'], kw.get('n', 16)
            f = lambda x, y: int((math.atan2(y + 0.5 - cy, x + 0.5 - cx) + math.pi) / (2 * math.pi) * n * 2) % 2 == 0
        elif kind == 'fibre':
            f = lambda x, y: ((x * 73 + y * 151 + (x * y) % 17) % 23) in (0, 7)
        elif kind == 'scales':
            f = lambda x, y: ((x % p) - p / 2.0) ** 2 + ((y + (p // 2 if (x // p) % 2 else 0)) % p) ** 2 * 0.9 < (p / 2.0) ** 2 and \
                ((x % p) - p / 2.0) ** 2 + ((y + (p // 2 if (x // p) % 2 else 0)) % p) ** 2 * 0.9 > (p / 2.0 - 1.2) ** 2
        else:
            raise ValueError(kind)
        self.where(f, c, dmin)

    def disc(self, cx, cy, r, c, ring=None, ring2=None, clip=True):
        for y in range(int(cy - r) - 1, int(cy + r) + 2):
            for x in range(int(cx - r) - 1, int(cx + r) + 2):
                d = math.hypot(x + 0.5 - cx, y + 0.5 - cy)
                if d <= r:
                    col = c
                    if ring is not None and d > r - 1:
                        col = ring
                    elif ring2 is not None and r - 3 < d <= r - 2:
                        col = ring2
                    self.put(x, y, col, clip=clip)

    def text(self, word, cx, y, c, scale=1, shadow=None, clip=True, gap=None):
        """The project's 3x5 face; each letter moves as one piece when pressed. gap: the air between
        letters in pixels (a scale-2 word set tight takes 1, not 2)."""
        word = word.upper()
        gap = fontpx.GAP * scale if gap is None else gap
        w = len(word) * fontpx.GW * scale + (len(word) - 1) * gap if word else 0
        x = int(round(cx - w / 2.0))
        for ch in word:
            rows = fontpx.GLYPHS.get(ch, fontpx.GLYPHS[' '])
            g = self.group(x + fontpx.GW * scale / 2.0)
            for gy, row in enumerate(rows):
                for gx, bit in enumerate(row):
                    if bit != '#':
                        continue
                    for sy in range(scale):
                        for sx in range(scale):
                            if shadow is not None:
                                self.put(x + gx * scale + sx + 1, y + gy * scale + sy + 1, shadow, g, clip)
                    for sy in range(scale):
                        for sx in range(scale):
                            self.put(x + gx * scale + sx, y + gy * scale + sy, c, g, clip)
            x += fontpx.GW * scale + gap
        return w

    def width_at(self, y, inset=0):
        s = self.span(y)
        return (s[1] - s[0] + 1 - 2 * inset) if s else 0

    def say(self, word, cx, y, c, scale=2, inset=3, shadow=None):
        """The word as big as the label's narrowest row under it allows; returns the rows it took."""
        while scale > 1 and fontpx.width(word, scale) > min(self.width_at(yy, inset) for yy in range(y, y + fontpx.GH * scale)):
            scale -= 1
        self.text(word, cx, y, c, scale, shadow=shadow)
        return fontpx.GH * scale

    def fig(self, name, cx, cy, ink=None, over=None, clip=True):
        rows, cmap = hand_figures.grid(name)
        cmap = dict(cmap)
        if over:
            cmap.update(over)
        fw, fh = len(rows[0]), len(rows)
        x0, y0 = int(round(cx - fw / 2.0)), int(round(cy - fh / 2.0))
        g = self.group(x0 + fw / 2.0)
        for r, line in enumerate(rows):
            for q, ch in enumerate(line):
                if ch == '.':
                    continue
                col = cmap.get(ch) or (ink if ch == 'k' and ink else 'Night1')
                self.put(x0 + q, y0 + r, col, g, clip)
        return fw, fh

    def grid(self, rows, cmap, cx, cy, clip=True):
        fw, fh = len(rows[0]), len(rows)
        x0, y0 = int(round(cx - fw / 2.0)), int(round(cy - fh / 2.0))
        g = self.group(x0 + fw / 2.0)
        for r, line in enumerate(rows):
            for q, ch in enumerate(line):
                if ch != '.' and ch in cmap:
                    self.put(x0 + q, y0 + r, cmap[ch], g, clip)

    def ribbon(self, y, h, c, word=None, wc='Cream4', scale=1, tails=0, clip=True, edge=True):
        """A band across the label; with tails it runs past the outline and ends in swallowtails."""
        lo_all = [x for (x, yy) in self.shape if y <= yy < y + h]
        if not lo_all:
            return
        x0, x1 = min(lo_all) - tails, max(lo_all) + tails
        mid = y + h / 2.0
        for yy in range(y, y + h):
            for x in range(x0, x1 + 1):
                e = min(x - x0, x1 - x)
                if tails and e < tails and abs(yy + 0.5 - mid) < (tails - e) * 0.8:
                    continue                              # the swallowtail's notch
                col = c
                if edge and (yy == y or yy == y + h - 1):
                    col = step(C(c), -1)
                if tails and e < tails:
                    col = step(C(c), -1)                  # the ends fold behind, a step darker
                self.put(x, yy, col, clip=clip and not tails)
        if word:
            self.text(word, (x0 + x1 + 1) / 2.0, int(round(mid - fontpx.GH * scale / 2.0)), wc, scale, clip=False)

    def laurel(self, cx, cy, r, c, c2):
        """Two branches of leaves round a monogram."""
        for side in (-1, 1):
            for k in range(7):
                a = math.radians(200 - k * 22) if side < 0 else math.radians(-20 + k * 22)
                a = math.radians(180 + 25 + k * 20) if side < 0 else math.radians(-25 - k * 20)
                lx, ly = cx + r * math.cos(a), cy - r * math.sin(a)
                for dy in (-1, 0):
                    for dx in (0, side):
                        self.put(int(lx) + dx, int(ly) + dy, c if dy else c2)

    def corners(self, c, d=2, size=3):
        """Small ornaments inside the frame's four corners."""
        ys = sorted({y for (_, y) in self.shape})
        if not ys:
            return
        for y in (ys[0] + d, ys[-1] - d):
            s = self.span(y)
            if not s:
                continue
            for x, sgn in ((s[0] + d, 1), (s[1] - d, -1)):
                for k in range(size):
                    self.put(x + sgn * k, y, c)
                    self.put(x, y + (k if y < self.h / 2 else -k), c)


def _inside(kind, x, y, w, h, r, lo, hi, kw):
    cx, cy = (lo + hi + 1) / 2.0, h / 2.0
    hw = (hi - lo + 1) / 2.0
    X, Y = x + 0.5 - cx, y + 0.5 - cy
    if kind in ('rect', 'full'):
        if r and (x - lo < r or hi - x < r) and (y < r or h - 1 - y < r):
            return min(x - lo, hi - x) + min(y, h - 1 - y) >= r - 1
        return True
    if kind == 'round':
        if r and (x - lo < r or hi - x < r) and (y < r or h - 1 - y < r):
            ax = r - 0.5 - min(x - lo, hi - x)
            ay = r - 0.5 - min(y, h - 1 - y)
            return ax * ax + ay * ay <= r * r
        return True
    if kind in ('oval', 'circle'):
        return (X / hw) ** 2 + (Y / (h / 2.0)) ** 2 <= 1.0
    if kind == 'diamond':
        return abs(X) / hw + abs(Y) / (h / 2.0) <= 1.0
    if kind == 'arch':
        ra = r or hw
        if y + 0.5 < ra:
            k = (ra - (y + 0.5)) / ra
            return abs(X) <= hw * math.sqrt(max(0.0, 1 - k * k))
        return True
    if kind == 'shield':
        t = (y + 0.5) / h
        if t < 0.55:
            return True
        return abs(X) <= hw * (1 - (t - 0.55) / 0.45) ** 0.8 + 0.5
    if kind == 'banner':
        e = min(x - lo, hi - x)
        return not (e < r and abs(Y) < (r - e) * 0.9)
    if kind == 'torn':
        jag = 1 if ((x * 5 + 3) % 7) < 3 else 0
        jag += 1 if ((x * 3 + 1) % 5) == 0 else 0
        return y >= jag
    if kind == 'octagon':
        c_ = r or max(2, int(min(w, h) * 0.2))
        return min(x - lo, hi - x) + min(y, h - 1 - y) >= c_ - 1
    raise ValueError(kind)


# ── pressing a label onto a round bottle ─────────────────────────────────────
class Ctx:
    """What a recipe draws with: the bottle, and label plates it asks for."""

    def __init__(self, cid, sh, fp, bp, mp, glass):
        self.cid, self.sh, self.fp, self.bp, self.mp, self.g = cid, sh, fp, bp, mp, glass
        S = sh.S
        self.xc = sh.W / 2.0
        self.y_lo = sh.shoulder_end + 2 * S
        self.y_hi = max(sh.colbot.values()) - 4 * S
        self.plates = []

    def half(self, y):
        return self.sh.width(y) / 2.0

    def label(self, kind='rect', wf=0.8, h=None, hf=None, at=0.5, y0=None, r=0, follow=True, bow=True,
              mode='paper', shade=True, w=None, **kw):
        """A plate over rows y0..y0+h, as wide as wf of the body at each row (the outline follows the
        glass; follow=False keeps the narrowest row's width all the way down)."""
        span = self.y_hi - self.y_lo + 1
        if h is None:
            h = int(round((hf or 0.5) * span))
        h = max(8, min(h, span))
        if y0 is None:
            c = self.y_lo + (span - 1) * at
            y0 = int(round(c - h / 2.0))
            y0 = max(self.y_lo, min(self.y_hi - h + 1, y0))
        halves = []
        for yy in range(y0, y0 + h):
            hb = self.half(yy)
            halves.append(hb * wf if w is None else min(w / 2.0, hb * max(wf, 0.99)))
        if not follow:
            m = min(halves)
            halves = [m] * len(halves)
        W = 2 * int(math.ceil(max(halves)))
        x0 = int(round(self.xc - W / 2.0))

        def allow(ly, halves=halves):
            a = halves[ly] if 0 <= ly < len(halves) else 0
            n = int(round(a))
            return (int(round(self.xc - n)) - x0, int(round(self.xc + n)) - 1 - x0)
        P = Plate(W, h, allow, mode)
        P.form(kind, r, **kw)
        mid = y0 + h // 2
        hb = self.half(mid)
        bowmax = hb * math.sin(PHI) if bow else 0.0
        self.plates.append((P, x0, y0, bowmax, hb, shade))
        return P

    def press(self):
        sh, fp = self.sh, self.fp
        for P, x0, y0, bowmax, hb, shade in self.plates:
            ue = (x0 + 0.5 - self.xc) / hb if hb else 0

            def off(bx):
                if not bowmax:
                    return 0
                u = (bx + 0.5 - self.xc) / hb
                v = int(round(bowmax * math.sqrt(max(0.0, 1 - min(1.0, u * u)))))
                b = int(round(bowmax * math.sqrt(max(0.0, 1 - min(1.0, ue * ue)))))
                return v - b
            low = {}
            # THE GROUND FIRST, THEN WHAT IS ON IT: a word moves as one piece while the paper under it
            # bends column by column, so the two land on different rows; pressed in one pass the paper
            # of a neighbouring row came down over a letter ("WHITE BAT" read "WHITE EAT")
            items = [((x, y), c, None) for (x, y), c in P.px.items()] +                     [((x, y), c, g) for (x, y), (c, g) in P.fg.items()]
            for (x, y), c, g in items:
                bx = x0 + x
                by = y0 + y + (off(int(round(x0 + P.gc[g]))) if g else off(bx))
                if not sh.inside(bx, by):
                    continue
                if len(c) == 4:
                    fp[bx, by] = c                         # glass (emboss, window): its own alpha
                    continue
                col = c
                if shade:
                    hw = self.half(by)
                    u = (bx + 0.5 - self.xc) / hw if hw else 0
                    k = -2 if u > 0.84 else (-1 if u > 0.52 or u < -0.86 else 0)
                    if g:
                        k = max(k, -1)                     # a word or a figure stays itself at the edge
                    col = step(c, k)
                fp[bx, by] = col + (255,)
                if P.mode == 'paper' and (x, y) in P.shape:
                    low[bx] = max(low.get(bx, -1), by)
            # THE PAPER SITS ON THE GLASS: a row of shadow under its lower edge, where the label's own
            # thickness keeps the light off the glass beneath it
            for bx, by in low.items():
                y = by + 1
                if sh.inside(bx, y) and fp[bx, y][3] < 255 and self.mp[bx, y][3]:
                    fp[bx, y] = self.g['shade'][0] + (min(255, max(fp[bx, y][3], 150)),)


# ── the recipes ──────────────────────────────────────────────────────────────
# Heights are the content's own (rows counted below); the width is a share of the glass at each row.
def R_vodka_astra(c):
    # Chambord's medallion on its gold belt: a guilloche ground, the crown, a navy ribbon
    d = 46
    P = c.label('circle', w=d, wf=0.99, h=d, at=0.38, follow=False)
    P.fill('Cream4'); P.ring('Amber2', 0); P.ring('Amber3', 1, 2); P.ring('Amber1', 3)
    P.pat('rays', 'Cream3', dmin=4, cx=P.w / 2.0, cy=P.h / 2.0, n=20)
    P.where(lambda x, y: P.depth.get((x, y)) == 1 and x % 3 == 0, 'Amber4')
    P.fig('crown', P.w / 2.0, 15)
    P.ribbon(25, 9, 'ClubBlue1', 'SMIRKOFF', 'Cream4', tails=4)
    P.text('VODKA', P.w / 2.0, 36, 'ClubBlue1')


def R_vodka_vor(c):
    # Absolut: printed straight on the glass — a medallion, the word, the script under it
    P = c.label('full', wf=0.9, h=66, at=0.5, mode='print')
    cx = P.w / 2.0
    P.disc(cx, 10, 9, 'ClubBlue1')
    P.disc(cx, 10, 7, 'ClubBlue4')
    P.fig('cameo', cx, 10, ink='ClubBlue1')
    for k in range(8):
        a = k * math.pi / 4
        P.put(int(cx + 8.5 * math.cos(a)), int(10 + 8.5 * math.sin(a)), 'Cream4')
    P.text('COUNTRY OF', cx, 23, 'ClubBlue2')
    P.text('ABSOLVE', cx, 31, 'ClubBlue0', 1, shadow='ClubBlue3')
    P.say('VODKA', cx, 40, 'ClubBlue1', 2, inset=1)
    for k, frac in enumerate((0.8, 0.66, 0.74, 0.5)):
        y = 54 + k * 3
        half = P.w * frac / 2.0
        P.where(lambda x, yy, y=y, half=half, k=k: yy == y and abs(x + 0.5 - cx) < half and (x // 3 + k) % 5 != 0,
                'ClubBlue2')


def R_vodka_leonid(c):
    # Grey Goose: the clear window in the frost, geese flying over the mountains, the flag's stripe
    P = c.label('rect', wf=0.8, h=40, at=0.36, mode='window', r=2)
    g = c.g
    P.fill(g['mid'][0] + (14,))
    P.ring(g['hi'][0] + (210,), 0)
    for fx, fy in ((0.3, 0.26), (0.66, 0.18), (0.5, 0.44)):
        P.fig('goose', P.w * fx, P.h * fy, ink='ClubBlue0')
    for x in range(1, P.w - 1):
        yy = int(P.h * 0.8 - 7 * abs(((x / float(P.w)) * 2.0) % 1.0 - 0.5))
        P.put(x, yy, 'ClubBlue1')
        P.put(x, yy + 1, 'ClubBlue3')
    y0 = c.plates[-1][2] + P.h + 5
    Q = c.label('full', wf=0.86, h=22, y0=y0, mode='print')
    Q.text('GANDER', Q.w / 2.0, 1, 'ClubBlue0', 1)
    Q.text('VODKA', Q.w / 2.0, 9, 'Graphite1')
    third = Q.w // 6
    for x in range(Q.w // 2 - third, Q.w // 2 + third):
        k = (x - (Q.w // 2 - third)) * 3 // (2 * third)
        for y in (17, 18):
            Q.put(x, y, ('ClubBlue2', 'Cream4', 'ViceRed2')[min(2, k)])


def R_gin_boothby(c):
    # Tanqueray: a cream label with a pineapple crest, a red seal pressed over its top edge
    P = c.label('round', wf=0.66, h=64, at=0.64, r=6, follow=False)
    P.fill('Cream4'); P.ring('Lime0', 0); P.ring('Cream4', 1); P.ring('Lime1', 2)
    P.pat('dots', 'Cream3', dmin=4, p=4)
    cx = P.w / 2.0
    P.fig('pineapple', cx, 23)
    P.text("GARDEN'S", cx, 34, 'Lime0')
    P.hline(42, 'ViceRed2', 7, P.w - 8)
    P.text('LONDON', cx, 45, 'Lime1')
    P.text('DRY GIN', cx, 53, 'Lime0')
    Q = c.label('circle', w=26, wf=0.99, h=26, y0=c.plates[-1][2] - 9, follow=False)
    Q.fill('ViceRed2')
    Q.where(lambda x, y: Q.depth.get((x, y)) == 0 and (x + y) % 3 == 0, 'ViceRed1')
    Q.ring('ViceRed1', 2)
    Q.where(lambda x, y: Q.depth.get((x, y), 0) >= 3 and (x - Q.w / 2.0) + (y - Q.h / 2.0) < -7, 'ViceRed3')
    from shelf_style import ICONS, scale3x
    Q.grid(scale3x(ICONS['G']), {'k': 'Cream4', 'a': 'Cream4'}, Q.w / 2.0, Q.h / 2.0)


def R_gin_juniper_crown(c):
    # Beefeater: an arch of cream in a red frame, the crown over the juniper, the name on a ribbon
    P = c.label('arch', wf=0.9, h=74, at=0.5)
    P.fill('Cream4'); P.ring('ViceRed1', 0); P.ring('ViceRed2', 1); P.ring('Cream4', 2); P.ring('ViceRed2', 3)
    P.pat('diag', 'Cream3', dmin=5, p=4)
    cx = P.w / 2.0
    P.fig('crown_small', cx, 15)
    P.fig('juniper', cx, 30)
    P.ribbon(41, 9, 'ViceRed2', 'LEAFEATER', 'Cream4', tails=3)
    P.say('GIN', cx, 53, 'ViceRed1', 2)
    P.text('LONDON', cx, 65, 'Lime0')


def R_gin_thornwood(c):
    # Hendrick's: a diamond on the dark glass, a black double frame, the rose among dotted filigree
    P = c.label('diamond', wf=0.8, h=104, at=0.5, follow=False)
    P.fill('Cream4'); P.ring('Night1', 0, 1); P.ring('Cream4', 2); P.ring('Night1', 3)
    P.where(lambda x, y: P.depth.get((x, y)) == 6 and (x + y) % 2 == 0, 'Night2')
    cx, cy = P.w / 2.0, P.h / 2.0
    P.fig('rose', cx, cy - 15)
    y = int(cy)
    y += P.say("HENDRAKE'S", cx, y, 'Night1', 2, inset=6) + 3
    P.hline(y, 'ViceRed2', int(cx - 10), int(cx + 9))
    P.text('GIN', cx, y + 3, 'ViceRed1')


def R_gin_veilcrest(c):
    # Monkey 47: a round label, a sunburst behind the monkey, the name on a navy ribbon
    d = 56
    P = c.label('circle', w=d, wf=0.99, h=d, at=0.44, follow=False)
    P.fill('Amber4'); P.ring('ViceRed1', 0); P.ring('ViceRed2', 1, 2)
    P.where(lambda x, y: P.depth.get((x, y)) == 1 and (x + y) % 4 == 0, 'Cream4')
    P.pat('rays', 'Amber3', dmin=4, cx=P.w / 2.0, cy=21, n=14)
    P.fig('gibbon', P.w / 2.0, 20)
    P.ribbon(32, 9, 'ClubBlue1', 'GIBBON 48', 'Cream4', tails=4)
    P.text('DRY GIN', P.w / 2.0, 44, 'Night1')


def R_rum_cane_coral(c):
    # Bacardi: a cream label, the bat in a red disc ringed in gold, the name over it
    P = c.label('rect', wf=0.92, h=68, at=0.54, r=3, follow=False)
    P.fill('Cream4'); P.ring('Amber2', 0); P.ring('Cream4', 1); P.ring('Night1', 2)
    P.corners('Amber2', d=4, size=3)
    cx = P.w / 2.0
    P.text('WHITE BAT', cx, 6, 'Night1')
    P.hline(13, 'Amber2', 5, P.w - 6)
    P.disc(cx, 29, 14, 'ViceRed2', ring='Amber1', ring2='Amber3')
    P.fig('bat', cx, 29)
    P.ribbon(46, 9, 'ViceRed1', 'RUM', 'Cream4')
    P.text('SUPERIOR', cx, 58, 'Night1')


def R_rum_tidewater(c):
    # Captain Morgan: an oval in a red double ring, the anchor and its rope, the name on a ribbon
    P = c.label('oval', wf=0.92, h=64, at=0.52, follow=False)
    P.fill('Cream4'); P.ring('ViceRed1', 0); P.ring('ViceRed2', 1); P.ring('Cream4', 2); P.ring('ViceRed1', 3)
    P.where(lambda x, y: P.depth.get((x, y)) == 2 and (x * 3 + y) % 5 == 0, 'Amber3')
    cx = P.w / 2.0
    P.fig('anchor', cx, 21, ink='Night1')
    P.ribbon(35, 9, 'ViceRed2', 'ADMIRAL', 'Cream4', tails=4)
    P.text('SPICED', cx, 47, 'ViceRed1')


def R_rum_windward(c):
    # Kraken: an aged banner round the black ball, engraved hatching at its ends, the volcano
    P = c.label('banner', wf=1.0, h=44, at=0.5, r=6)
    P.fill('Cream3'); P.ring('Cream1', 0)
    cx = P.w / 2.0
    P.where(lambda x, y: abs(x + 0.5 - cx) > P.w * 0.3 and (x + y) % 3 == 0, 'Cream2', dmin=1)
    P.hline(2, 'Night1', int(cx - P.w * 0.28), int(cx + P.w * 0.28))
    P.hline(P.h - 3, 'Night1', int(cx - P.w * 0.28), int(cx + P.w * 0.28))
    P.fig('volcano', cx, 15)
    P.text('KRAKATOA', cx, 28, 'Night1')
    P.text('BLACK RUM', cx, 35, 'ViceRed1')


def R_rum_reina_del_mar(c):
    # Malibu: printed on the milk glass — a whole sunset sun, its lower half cut by bars, the palm in
    # front of it on its island, the name under the horizon
    P = c.label('full', wf=0.9, h=70, at=0.44, mode='print')
    cx = P.w / 2.0
    r = 14
    cy = 17
    for y in range(int(cy - r), int(cy + r) + 1):
        for x in range(int(cx - r) - 1, int(cx + r) + 2):
            if math.hypot(x + 0.5 - cx, y + 0.5 - cy) > r:
                continue
            t = (y - (cy - r)) / (2.0 * r)
            if t > 0.5 and int((y - cy) * 1.0) % 4 in (2, 3) and (y - cy) > 2:
                continue                                   # the sunset's bars
            P.put(x, y, 'Amber4' if t < 0.3 else 'Amber3' if t < 0.5 else 'ViceRed3' if t < 0.72 else 'ViceRed2')
    P.fig('palm', cx + 4, cy + 3)
    for x in range(int(cx - r - 5), int(cx + r + 6)):
        P.put(x, int(cy + r + 1), 'Malt1')
    P.say('MALIBOO', cx, int(cy + r + 5), 'Night1', 2, inset=1)
    P.text('COCONUT', cx, int(cy + r + 17), 'ViceRed2')
    P.text('RUM', cx, int(cy + r + 24), 'Malt1')


def R_bourbon_hollow_oak(c):
    # Hibiki: washi paper high on the body, the acorn among oak leaves, a red seal in the corner
    P = c.label('rect', wf=0.76, h=58, at=0.3, follow=False)
    P.fill('Cream3'); P.pat('fibre', 'Cream4'); P.pat('fibre', 'Cream2', p=5, dmin=1)
    P.ring('Amber2', 0)
    cx = P.w / 2.0
    P.fig('oakleaf', cx - 13, 14)
    P.fig('oakleaf', cx + 13, 14)
    P.fig('acorn', cx, 15)
    P.say('WRINKLE', cx, 27, 'Malt0', 2, inset=4)
    P.hline(40, 'Malt1', 8, P.w - 9)
    P.text('WHISKY', cx, 43, 'Malt1')
    for y in range(P.h - 12, P.h - 5):
        for x in range(P.w - 12, P.w - 5):
            P.put(x, y, 'ViceRed2' if (x + y) % 5 else 'ViceRed3')
    for y in range(8, P.h - 8, 3):
        P.put(5, y, 'Malt1')


def R_tequila_sonora(c):
    # Galliano: a tall label narrowing with the cone, fluted gold columns at its sides, the crow on the sun
    P = c.label('arch', wf=0.84, h=86, at=0.8)
    P.fill('Cream4'); P.ring('Amber1', 0)
    P.where(lambda x, y: (P.span(y) and (x - P.span(y)[0] < 4 or P.span(y)[1] - x < 4)), 'Amber3', dmin=1)
    P.where(lambda x, y: (P.span(y) and (x - P.span(y)[0] == 2 or P.span(y)[1] - x == 2)), 'Amber2', dmin=1)
    cx = P.w / 2.0
    h = P.h
    P.text('TEQUILA', cx, h - 12, 'Amber2')
    P.text('CUERDO', cx, h - 21, 'Night1')
    P.disc(cx, h - 36, 9, 'Amber3', ring='Amber2')
    P.fig('crow', cx + 1, h - 37, over={'m': 'Malt1'})
    P.where(lambda x, y: y < h - 48 and y % 4 == 0 and P.depth.get((x, y), 0) >= 4 and x % 2 == 0, 'Amber3')


def R_tequila_alta_luna(c):
    # 1800: raised glass — the crescent moon and its stars stand out of the pyramid, the number under
    g = c.g
    P = c.label('full', wf=0.8, h=48, at=0.36, mode='emboss')
    cx = P.w / 2.0
    hi, sd = g['hi'][0] + (235,), g['shade'][0] + (190,)
    cells = []
    R = 11
    for y in range(0, 2 * R + 2):
        for x in range(int(cx - R) - 1, int(cx + R) + 2):
            d1 = math.hypot(x + 0.5 - cx, y + 0.5 - (R + 1))
            d2 = math.hypot(x + 0.5 - (cx + 6), y + 0.5 - (R - 2))
            if d1 <= R and d2 > R - 2:
                cells.append((x, y))
    for sx, sy in ((cx + 12, 4), (cx + 16, 14), (cx - 15, 6), (cx + 9, 22)):
        for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
            cells.append((int(sx) + dx, int(sy) + dy))
    word = '1810'
    x = int(cx - fontpx.width(word, 2) / 2.0)
    for ch in word:
        for gy, row in enumerate(fontpx.GLYPHS[ch]):
            for gx, bit in enumerate(row):
                if bit == '#':
                    for sy in range(2):
                        for sx in range(2):
                            cells.append((x + gx * 2 + sx, 2 * R + 6 + gy * 2 + sy))
        x += (fontpx.GW + fontpx.GAP) * 2
    for x, y in cells:
        P.put(x + 1, y + 1, sd)
    for x, y in cells:
        P.put(x, y, hi)
    Q = c.label('rect', wf=0.72, h=19, at=0.78, follow=False)
    Q.fill('Night1'); Q.ring('Amber2', 0); Q.ring('Night1', 1)
    Q.dotline(2, 'Amber3'); Q.dotline(Q.h - 3, 'Amber3')
    Q.text('ALTA LUNA', Q.w / 2.0, 7, 'Cream4')


def R_tequila_cielo_roto(c):
    # Clase Azul: hand-painted cobalt on white ceramic — zigzag borders, flowers, the sun in the middle
    P = c.label('full', wf=0.96, h=76, at=0.5, mode='print')
    cx = P.w / 2.0
    y0, y1 = P.rows()
    for y, par in ((y0 + 1, 0), (y1 - 1, 1)):
        P.hline(y, 'ClubBlue1')
        P.where(lambda x, yy, y=y, par=par: yy == (y + 2 if par == 0 else y - 2) and (x // 2) % 2 == 0, 'ClubBlue2')
        P.where(lambda x, yy, y=y, par=par: yy == (y + 3 if par == 0 else y - 3) and (x // 2) % 2 == 1, 'ClubBlue2')
    cy = (y0 + y1) / 2.0 - 5
    for k in range(12):
        a = k * math.pi / 6
        for t in range(9, 13):
            P.put(int(cx + t * math.cos(a)), int(cy + t * math.sin(a)), 'ClubBlue2')
    P.disc(cx, cy, 7, 'ClubBlue2', ring='ClubBlue1')
    P.disc(cx, cy, 3, 'Cream4')
    for fx, fy in ((0.16, 0.22), (0.84, 0.22), (0.16, 0.78), (0.84, 0.78)):
        px_, py_ = P.w * fx, y0 + (y1 - y0) * fy
        for dx, dy in ((0, -2), (2, 0), (0, 2), (-2, 0)):
            P.disc(px_ + dx, py_ + dy, 1.5, 'ClubBlue2')
        P.put(int(px_), int(py_), 'Amber3')
    P.text('AZULEJO', cx, int(cy + 16), 'ClubBlue1')


def R_amaro_notte(c):
    # Frangelico's oval, here a night: the stars over an orange slice, the name on the cream below
    P = c.label('oval', wf=0.92, h=62, at=0.52, follow=False)
    P.fill('Cream4'); P.ring('Malt1', 0); P.ring('Amber2', 1); P.ring('Malt1', 2)
    cx = P.w / 2.0
    sky = int(P.h * 0.5)
    P.where(lambda x, y: y < sky, 'ClubBlue0', dmin=3)
    P.where(lambda x, y: y < sky and ((x * 7 + y * 13) % 29) == 0, 'Cream4', dmin=4)
    P.where(lambda x, y: y == sky, 'Amber2', dmin=3)
    r = 8
    sy = sky - r - 2
    for y in range(sy - r - 1, sy + r + 2):
        for x in range(int(cx - r) - 1, int(cx + r) + 2):
            dx, dy = x + 0.5 - cx, y + 0.5 - sy
            d = math.hypot(dx, dy)
            if d > r:
                continue
            ang = (math.degrees(math.atan2(dy, dx)) + 360) % 45
            col = 'Amber1' if d > r - 1 else ('Cream4' if d > r - 2.2 or d < 1.2 or ang < 6 else
                                               ('Amber4' if dx + dy < 0 else 'Amber3'))
            P.put(x, y, col)
    P.text('CUMPARI', cx, sky + 4, 'ViceRed1')
    P.text('AMARO', cx, sky + 12, 'Malt1')


def R_vermouth_velvet(c):
    # a black wrap round the Bordeaux bottle, gold rules, the V in a laurel
    P = c.label('rect', wf=1.0, h=54, at=0.56)
    P.fill('Night1')
    for y in (2, 4, P.h - 5, P.h - 3):
        P.hline(y, 'Amber3' if y in (2, P.h - 3) else 'Amber2')
    P.where(lambda x, y: 7 <= y < P.h - 7 and (x + y) % 6 == 0 and y % 3 == 0, 'Night2')
    cx = P.w / 2.0
    P.grid(['kkkk...kkk', '.kk.....k.', '.kk.....k.', '..kk...k..', '..kk...k..', '...kk.k...',
            '...kk.k...', '....kkk...', '....kk....'], {'k': 'Amber4'}, cx, 18)
    for k in range(6):
        for side in (-1, 1):
            a = math.radians(k * 22 - 30)
            lx = cx + side * 14 * math.cos(a)
            ly = 18 + 12 * math.sin(a)
            P.put(int(lx), int(ly), 'Amber3')
            P.put(int(lx) + side, int(ly) - 1, 'Amber2')
    P.text('VELVET', cx, 33, 'Amber4')
    P.text('VERMOUTH', cx, 41, 'Amber2')


def R_liqueur_delia(c):
    # Grand Marnier: a cream label framed in navy, the ship on its waves, the name, a red rule
    P = c.label('round', wf=0.74, h=50, at=0.6, r=5, follow=False)
    P.fill('Cream4'); P.ring('ClubBlue0', 0); P.ring('Cream4', 1); P.ring('ClubBlue1', 2)
    cx = P.w / 2.0
    P.say('MARINER', cx, 6, 'ClubBlue1', 2)
    P.fig('ship', cx, 25)
    P.hline(35, 'ViceRed2', int(cx - 14), int(cx + 13))
    P.text('LIQUEUR', cx, 38, 'ViceRed1')


def R_liqueur_kafa(c):
    # Baileys: a wrap with a brown band carrying the name, a pastoral picture — the koala on its branch
    P = c.label('rect', wf=1.0, h=52, at=0.46)
    P.fill('Cream4')
    P.hband(0, 11, 'Malt1'); P.hline(11, 'Amber3')
    cx = P.w / 2.0
    P.text('KOALA', cx, 3, 'Cream4')
    P.where(lambda x, y: y in (14, 37) and abs(x + 0.5 - cx) < 16, 'Amber3')
    P.where(lambda x, y: 14 <= y <= 37 and abs(abs(x + 0.5 - cx) - 16) < 0.6, 'Amber3')
    P.fig('koala', cx, 22)
    P.fig('branch', cx, 32)
    P.hline(40, 'Amber3')
    P.text('LIQUEUR', cx, 43, 'Malt1')


def R_tonic_quinbury(c):
    # a tonic's wrap: pinstripes, the Q in a cyan medallion with its leaf, the name on a ribbon
    P = c.label('rect', wf=1.0, h=52, at=0.52)
    P.fill('Cream4')
    P.pat('vstripes', 'Cyan4', p=3)
    P.hline(1, 'Cyan1'); P.hline(P.h - 2, 'Cyan1')
    cx = P.w / 2.0
    P.disc(cx, 16, 12, 'Cream4', ring='Cyan1', ring2='Cyan2')
    from shelf_style import ICONS, scale3x
    P.grid(scale3x(ICONS['Q']), {'k': 'Cyan1', 'a': 'Cyan1'}, cx - 1, 16)
    P.fig('leaf', cx + 10, 6)
    P.ribbon(31, 9, 'Cyan1', "QUINN'S", 'Cream4')
    P.text('TONIC', cx, 43, 'Cyan1')


def R_soda_klara(c):
    # a Codd bottle's raised glass: the K in a ring of beads, the name and the stars under it
    g = c.g
    P = c.label('full', wf=0.86, h=44, at=0.5, mode='emboss')
    hi, sd = g['hi'][0] + (235,), g['shade'][0] + (190,)
    cx = P.w / 2.0
    cells = []
    for k in range(24):
        a = k * math.pi / 12
        cells.append((int(cx + 13 * math.cos(a)), int(15 + 13 * math.sin(a))))
    from shelf_style import ICONS, scale3x
    kk = scale3x(ICONS['K'])
    for r, line in enumerate(kk):
        for q, ch in enumerate(line):
            if ch != '.':
                cells.append((int(cx - len(line) / 2.0) + q, 15 - len(kk) // 2 + r))
    word = 'KLARA'
    x = int(cx - fontpx.width(word) / 2.0)
    for ch in word:
        for gy, row in enumerate(fontpx.GLYPHS[ch]):
            for gx, bit in enumerate(row):
                if bit == '#':
                    cells.append((x + gx, 33 + gy))
        x += fontpx.GW + fontpx.GAP
    for k in (-14, 14):
        for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
            cells.append((int(cx + k) + dx, 35 + dy))
    for x, y in cells:
        P.put(x + 1, y + 1, sd)
    for x, y in cells:
        P.put(x, y, hi)


def R_ginger_kicker(c):
    # a shield: stripes in its head, the boot kicking the ginger root, the name big
    P = c.label('shield', wf=0.74, h=74, at=0.5, follow=False)
    P.fill('Amber4'); P.ring('Night1', 0, 1); P.ring('Amber4', 2); P.ring('Night1', 3)
    P.where(lambda x, y: y < 16 and (x // 3) % 2 == 0, 'Amber3', dmin=4)
    P.hline(16, 'Night1', dmin=3)
    cx = P.w / 2.0
    P.fig('boot', cx - 5, 27)
    P.fig('ginger', cx + 13, 28)
    P.say('KICKER', cx, 37, 'Night1', 2, inset=4)
    P.text('GINGER', cx, 50, 'Malt1')
    P.text('BEER', cx, 57, 'Malt1')


def R_grenadine_rubis(c):
    # a syrup's arch: the pomegranate under its crown, the name on a red ribbon, the kind in fine print
    P = c.label('arch', wf=0.86, h=68, at=0.54)
    P.fill('Cream4'); P.ring('ViceRed1', 0); P.ring('Cream4', 1); P.ring('ViceRed2', 2)
    P.pat('dots', 'Cream3', dmin=4, p=4)
    cx = P.w / 2.0
    P.fig('crown_small', cx, 15)
    P.fig('pomegranate', cx, 29)
    P.ribbon(41, 9, 'ViceRed2', 'RUBIS', 'Cream4', tails=3)
    P.text('GRENADINE', cx, 53, 'ViceRed1')
    P.hline(60, 'ViceRed2', int(cx - 6), int(cx + 5))


def R_cola_marlow(c):
    # Loca's red wrap: bubbles rising, the cream wave, the word in cream with a dark shadow
    P = c.label('rect', wf=1.0, h=48, at=0.44)
    P.fill('ViceRed2')
    P.where(lambda x, y: ((x * 11 + y * 7) % 37) == 0, 'ViceRed3')
    P.where(lambda x, y: ((x * 5 + y * 17) % 43) == 0, 'Cream4')
    cx = P.w / 2.0
    for x in range(P.w):
        u = x / float(P.w)
        wy = int(P.h * 0.62 - 3 * math.sin(u * math.pi * 1.6))
        for t in range(4):
            P.put(x, wy + t, 'Cream4' if t < 2 else 'Cream3')
    P.say('LOCA', cx, 6, 'Cream4', 2, inset=1, shadow='ViceRed0')
    P.text('COLA', cx, int(P.h * 0.62) + 7, 'Cream4')


def R_energy_volt(c):
    # Blue Ox: bolts on the blue, the red ox on a yellow sun, the name
    P = c.label('rect', wf=1.0, h=50, at=0.46)
    P.fill('ClubBlue2')
    P.hline(0, 'ClubBlue0'); P.hline(P.h - 1, 'ClubBlue0')
    cx = P.w / 2.0
    for k in range(16):
        a = k * math.pi / 8
        for t in (12, 13):
            P.put(int(cx + t * math.cos(a)), int(16 + t * math.sin(a)), 'Amber3')
    P.disc(cx, 16, 11, 'Amber4', ring='Amber2')
    P.fig('ox', cx, 16)
    P.fig('bolt', 6, 9); P.fig('bolt', P.w - 7, 27)
    P.text('BLUE OX', cx, 33, 'Cream4', 1, shadow='ClubBlue0')
    P.text('ENERGY', cx, 41, 'Amber4')


# ── the square bottles (shelf_box): the label printed on the box's front face, sheared with it ──
def R_vodka_okhta(c):
    # Belvedere's tall label: birches in its head, the whale over the waves, the word
    P = c.label('rect', wf=0.88, h=min(90, c.y_hi - c.y_lo + 1), at=0.5, r=2)
    P.fill('ClubBlue4'); P.ring('ClubBlue1', 0); P.ring('Cream4', 1)
    cx = P.w / 2.0
    for i, x in enumerate(range(5, P.w - 4, 4)):
        for y in range(5, 16 + (i % 3) * 2):
            P.put(x, y, 'Cream4' if y % 4 == 1 else 'ClubBlue2')
    P.hline(20, 'ClubBlue1', dmin=2)
    P.fig('whale', cx, 31)
    P.where(lambda x, y: 39 <= y <= 45 and (y + int(round(1.2 * math.sin(x * 0.6)))) % 3 == 0, 'ClubBlue3', dmin=2)
    y = 49
    y += P.say('WHALE', cx, y, 'ClubBlue0', 2) + 3
    P.text('VODKA', cx, y, 'ClubBlue1')
    P.dotline(y + 8, 'ClubBlue2', dmin=2)
    P.text('OKHTA', cx, y + 11, 'ClubBlue2')


def R_bourbon_redline(c):
    # the walker's slanted label: red, a gold rule inside, the striding man and the words beside him
    P = c.label('rect', wf=0.9, h=44, at=0.52, slant=0.2)
    P.fill('ViceRed2'); P.ring('Night1', 0); P.ring('Amber3', 2)
    cx = P.w / 2.0
    P.text('WALKER', cx, 6, 'Cream4')
    P.fig('walker', 11, 26, over={'k': 'Cream4', 'w': 'ViceRed2'})
    P.text('BLACK', cx + 8, 16, 'Amber4')
    P.text('LABEL', cx + 8, 23, 'Amber4')
    P.text('WHISKEY', cx + 3, 34, 'Cream4')


def R_bourbon_old_harrow(c):
    # the Tennessee square: a black label, a dotted filigree frame, the spaniel, the word
    P = c.label('rect', wf=0.9, h=62, at=0.5, r=2)
    P.fill('Night1'); P.ring('Cream3', 1)
    P.where(lambda x, y: P.depth.get((x, y)) == 3 and (x + y) % 2 == 0, 'Cream2')
    P.corners('Cream3', d=5, size=3)
    cx = P.w / 2.0
    P.text('OLD NO 7', cx, 7, 'Cream3') if fontpx.width('OLD NO 7') <= P.w - 12 else P.text('NO 7', cx, 7, 'Cream3')
    P.fig('spaniel', cx, 22)
    y = 32
    y += P.say('SPANIEL', cx, y, 'Cream4', 2, inset=5) + 3
    P.hline(y, 'Cream2', 7, P.w - 8)
    P.text('WHISKEY', cx, y + 3, 'Cream4')
    P.text('1866', cx, y + 11, 'Cream3')


def R_bourbon_ashfall(c):
    # Maker's: a torn-edged cream label, the name, the star in its ring, the red wax above
    P = c.label('torn', wf=0.86, h=56, at=0.56)
    P.fill('Cream4'); P.ring('Cream2', 0)
    cx = P.w / 2.0
    y = 5
    y += P.say("MASON'S", cx, y, 'ViceRed1', 2, inset=3) + 3
    P.disc(cx, y + 10, 10, 'Cream4', ring='ViceRed1', ring2='ViceRed2')
    P.fig('star', cx, y + 10)
    y += 23
    P.text('WHISKEY', cx, y, 'Night1')
    P.text('HANDMADE', cx, y + 8, 'ViceRed2') if fontpx.width('HANDMADE') <= P.w - 6 else None


def R_tequila_sol_viejo(c):
    # Don Julio's squat: a high label, the sun's rays behind the agave, the name
    P = c.label('round', wf=0.86, h=56, at=0.36, r=4)
    P.fill('Amber4'); P.ring('Lime0', 0); P.ring('Amber4', 1); P.ring('Lime1', 2)
    cx = P.w / 2.0
    P.pat('rays', 'Amber3', dmin=3, cx=cx, cy=19, n=18)
    P.fig('agave', cx, 16)
    y = 26
    y += P.say('JULEP', cx, y, 'Lime0', 2, inset=4) + 3
    P.text('TEQUILA', cx, y, 'Lime1')
    P.text('BLANCO', cx, y + 8, 'Malt1')


def R_syrup_house(c):
    # a Monin: a cream label, the cottage with its smoke, the name, a bistro checker at its foot
    P = c.label('rect', wf=0.9, h=58, at=0.5, r=2)
    P.fill('Cream4'); P.ring('Amber2', 0); P.ring('Cream4', 1); P.ring('Amber3', 2)
    cx = P.w / 2.0
    y = 5
    y += P.say('HOUSE', cx, y, 'Amber1', 2, inset=3) + 2
    P.fig('cottage', cx, y + 10)
    y += 22
    P.text('SYRUP', cx, y, 'Amber2')
    P.where(lambda x, yy: yy >= y + 8 and ((x // 3) + (yy // 3)) % 2 == 0, 'Amber3', dmin=3)


HAND = {k[2:]: v for k, v in globals().items() if k.startswith('R_')}


def has(cid):
    return cid in HAND


def press_round(cid, sh, fp, bp, mp, glass):
    c = Ctx(cid, sh, fp, bp, mp, glass)
    HAND[cid](c)
    c.press()
