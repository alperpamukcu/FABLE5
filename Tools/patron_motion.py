# -*- coding: utf-8 -*-
"""A CUSTOMER WHO WALKS, ARRIVES AND LEAVES WITHOUT A HITCH (2026-09-15).

The author, looking at the walks: "animasyonun başı ile sonu arasında fark oluyor ... takılıyor
hissi", and then, on the pilot page, chose "şablon + sabitleme" for smoothness at the calm pace
of the stabilised walk, with an arrival and a departure instead of a snap. Three measured faults
this module answers, each with the number that proved it:

  THE HEAD WANDERED. Behind the counter the legs are hidden, so what reads as a walk is the head
  and shoulders - and inside one cycle the head slid 4-6 px sideways, a jolt of up to 4.5 px
  between two frames, and dipped three times at three depths. stabilise() holds the head on one
  column and smooths its rise and fall over its neighbours: sway under 1 px, jolt 2-4 px.

  THE SIDE VIEW IS ANOTHER PALETTE. The model draws a character walking in profile darker and in
  other colours than the same character facing the room: berlin's walk used the idle's colours
  for 6% of its pixels, the raw template walk for 1%, and it carried a black keyline besides - the
  author saw black hair on the walk turn light on the turn. lock_palette() redraws every pixel in
  the nearest colour of the idle's own palette (after the ink pass), so a clip cannot change the
  person's colours. A luminance-matched variant was tried and lightened the hair too far.

  THE CANVASES DISAGREE. PixelLab draws side clips on 256x256 or 220x220 and front clips on
  220x256, and refuses a start and end frame of different sizes (422). fit_canvas() puts a frame
  on another canvas, feet on the same row, centred on the figure.

Everything here is pure image work on frames already stood on the rig; patron_ship.py calls it.
"""
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)


# ── the head held still ─────────────────────────────────────────────────────────────

def _head(frame, rows=20):
    """(mean x of the head's top rows, the head's top row) - the part of a walker the room sees."""
    alpha = frame.split()[3].load()
    bb = frame.getbbox()
    xs = [x for y in range(bb[1], min(frame.height, bb[1] + rows))
          for x in range(frame.width) if alpha[x, y] >= 128]
    return (sum(xs) / float(len(xs)) if xs else frame.width / 2.0), bb[1]


def _shift(frame, dx, dy):
    out = Image.new('RGBA', frame.size, (0, 0, 0, 0))
    out.paste(frame, (dx, dy), frame)
    return out


def stabilise(frames):
    """A looping clip with its head on one column and its bob smoothed 1-2-1 around the cycle.

    Whole-pixel shifts only: nothing is resampled, so the drawing is untouched.
    """
    n = len(frames)
    if n < 3:
        return list(frames)
    heads = [_head(f) for f in frames]
    column = sum(h[0] for h in heads) / n
    out = []
    for i, f in enumerate(frames):
        before, here, after = heads[i - 1][1], heads[i][1], heads[(i + 1) % n][1]
        smooth = (before + 2 * here + after) / 4.0
        out.append(_shift(f, int(round(column - heads[i][0])), int(round(smooth - here))))
    return out


def bridge(frames, first_ref, last_ref):
    """A transition that STARTS where the clip before it stands and ENDS where the clip after it
    stands (2026-09-15, the author: "dönüş animasyonlarında otururken yükseliyor yürüyorken
    alçalıyor ... sonraki animasyona bitirdiği yerden başlamıyor").

    Anchoring one end to the idle left the other end wherever the model drew it: a turning
    figure came out a few rows off the walk it turns out of. So both ends are measured against
    the frames they join - the head's column and the head's top row, the part the room can see
    over the counter - and every frame between is shifted by the offset eased from the first
    end's to the last end's. Whole pixels only.

    HEIGHT IS HELD, NOT EASED (the author, the same evening: "ya da dönme animasyonunda
    yükseklikleri değişmemeli"). Matching the ends still let the model's settle-onto-the-seat dip
    and rise play in between. So each frame's head top is put on the straight line from the
    first join's height to the last join's - which for a turn at the bar is one height - while the
    head's sideways travel keeps the turn it was drawn with, eased between the two joins.
    """
    n = len(frames)
    if n == 0:
        return []
    hx0, _ = _head(frames[0])
    hx1, _ = _head(frames[-1])
    rx0, ry0 = _head(first_ref)
    rx1, ry1 = _head(last_ref)
    dx0, dx1 = rx0 - hx0, rx1 - hx1
    out = []
    for i, f in enumerate(frames):
        u = i / float(n - 1) if n > 1 else 1.0
        _, top = _head(f)
        target = ry0 + (ry1 - ry0) * u
        out.append(_shift(f, int(round(dx0 + (dx1 - dx0) * u)), int(round(target - top))))
    return out


# ── the person's own colours ────────────────────────────────────────────────────────

def _lab(c):
    def lin(u):
        u /= 255.0
        return u / 12.92 if u <= 0.04045 else ((u + 0.055) / 1.055) ** 2.4
    r, g, b = lin(c[0]), lin(c[1]), lin(c[2])
    x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047
    y = 0.2126 * r + 0.7152 * g + 0.0722 * b
    z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883

    def f(t):
        return t ** (1 / 3.0) if t > 0.008856 else 7.787 * t + 16 / 116.0
    fx, fy, fz = f(x), f(y), f(z)
    return 116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz)


class Palette:
    """The idle frame's colours, and the nearest of them to any other colour (Lab, lightness
    weighted over hue so a shade is never swapped for a different hue of the same value)."""

    CHROMA_WEIGHT = 0.6

    def __init__(self, idle):
        self.colours = sorted({p[:3] for p in idle.getdata() if p[3] >= 128})
        self.labs = [_lab(c) for c in self.colours]
        self._memo = {}

    def nearest(self, c):
        hit = self._memo.get(c)
        if hit is None:
            L = _lab(c)
            w = self.CHROMA_WEIGHT
            i = min(range(len(self.colours)),
                    key=lambda k: (L[0] - self.labs[k][0]) ** 2
                    + w * ((L[1] - self.labs[k][1]) ** 2 + (L[2] - self.labs[k][2]) ** 2))
            hit = self._memo[c] = self.colours[i]
        return hit


def lock_palette(frames, idle, ink=True):
    """Every opaque pixel of every frame in the nearest colour of the idle's palette.

    ink: run the ship step's keyline pass first (patron_ink.reink) - a raw frame's black outline
    would otherwise lock to the idle's darkest colour and stay a line.
    """
    import patron_ink
    palette = Palette(idle)
    out = []
    for f in frames:
        g = f.copy()
        if ink:
            patron_ink.reink(g)
        px = g.load()
        for y in range(g.height):
            for x in range(g.width):
                p = px[x, y]
                if p[3] >= 128:
                    px[x, y] = palette.nearest(p[:3]) + (p[3],)
        out.append(g)
    return out


def share_in_palette(frames, idle):
    """How much of a clip is drawn in the idle's colours - 1.0 after lock_palette."""
    colours = {p[:3] for p in idle.getdata() if p[3] >= 128}
    total = inside = 0
    for f in frames:
        for p in f.getdata():
            if p[3] >= 128:
                total += 1
                inside += p[:3] in colours
    return inside / float(total or 1)


# ── one canvas for two frames ───────────────────────────────────────────────────────

def fit_canvas(frame, size):
    """The frame on a canvas of `size`, centred on the figure, feet on the same distance from the
    bottom edge. Asserts the figure fits: a crop that cuts the person is not a fit."""
    W, H = size
    bb = frame.getbbox()
    out = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    cx = (bb[0] + bb[2]) // 2
    left = cx - W // 2
    top = H - frame.height           # bottom edges together: the feet keep their gap to the edge
    assert bb[0] - left >= 0 and bb[2] - left <= W, 'figure %s does not fit %d wide' % (bb, W)
    assert bb[1] + top >= 0, 'figure %s does not fit %d tall' % (bb, H)
    out.paste(frame, (-left, top), frame)
    return out
