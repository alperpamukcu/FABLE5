# -*- coding: utf-8 -*-
"""Shared helpers for the pixel-native title logo: palette tokens, masks, skeletons, a bounded
exact Euclidean distance transform with nearest-feature indices, run-based connected components,
and geodesic distance along a skeleton. numpy + PIL only (no scipy on this machine)."""
import heapq
import numpy as np

# ── the palette tokens the logo may use (UITheme ramps, Assets/Scripts/UI/UITheme.cs) ────────────
RAMPS = {
    'Night':    [0x0D0813, 0x1A1023, 0x241830, 0x362447, 0x4A3160],
    'Magenta':  [0x5C1B45, 0x8F2464, 0xC23283, 0xE84DA6, 0xFF7DC6],
    'Cyan':     [0x123B45, 0x1B5F66, 0x26918F, 0x3BC8BE, 0x7DF0E3],
    'ClubBlue': [0x131B3D, 0x1F2E66, 0x2E4699, 0x4467CC, 0x6E93F0],
    'Cream':    [0x453E38, 0x6E6459, 0x9C8F80, 0xC9BCA8, 0xF2E8D5],
    'Graphite': [0x14161A, 0x24272D, 0x383D45, 0x545A64, 0x808893],
    'Amber':    [0x4A2E14, 0x8F5A1E, 0xC9822B, 0xE8A33D, 0xF5C97B],
}


def rgb(h):
    return ((h >> 16) & 255, (h >> 8) & 255, h & 255)


def tok(name):
    """'Magenta[3]' -> (r, g, b)."""
    ramp, i = name.rstrip(']').split('[')
    return rgb(RAMPS[ramp][int(i)])


# ── masks / morphology ───────────────────────────────────────────────────────────────────────────
def shift(m, dy, dx, fill=False):
    out = np.full_like(m, fill)
    h, w = m.shape[:2]
    ys, yd = (slice(0, h - dy), slice(dy, h)) if dy >= 0 else (slice(-dy, h), slice(0, h + dy))
    xs, xd = (slice(0, w - dx), slice(dx, w)) if dx >= 0 else (slice(-dx, w), slice(0, w + dx))
    out[yd, xd] = m[ys, xs]
    return out


def zhang_suen(m):
    m = m.copy().astype(np.uint8)
    m[0, :] = m[-1, :] = 0
    m[:, 0] = m[:, -1] = 0
    while True:
        changed = False
        for step in (0, 1):
            P = np.pad(m, 1)
            p2 = P[:-2, 1:-1]; p3 = P[:-2, 2:]; p4 = P[1:-1, 2:]; p5 = P[2:, 2:]
            p6 = P[2:, 1:-1]; p7 = P[2:, :-2]; p8 = P[1:-1, :-2]; p9 = P[:-2, :-2]
            nb = [p2, p3, p4, p5, p6, p7, p8, p9]
            B = sum(n.astype(np.int32) for n in nb)
            seq = nb + [p2]
            A = sum(((seq[i] == 0) & (seq[i + 1] == 1)).astype(np.int32) for i in range(8))
            if step == 0:
                c1 = (p2 * p4 * p6) == 0
                c2 = (p4 * p6 * p8) == 0
            else:
                c1 = (p2 * p4 * p8) == 0
                c2 = (p2 * p6 * p8) == 0
            kill = (m == 1) & (B >= 2) & (B <= 6) & (A == 1) & c1 & c2
            if kill.any():
                m[kill] = 0
                changed = True
        if not changed:
            return m.astype(bool)


def neighbours8(m):
    n = np.zeros(m.shape, np.int32)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            if dy or dx:
                n += shift(m, dy, dx).astype(np.int32)
    return n


def prune_spurs(sk, length):
    """Remove skeleton branches shorter than `length` px that end in a free endpoint."""
    sk = sk.copy()
    for _ in range(2):
        nb = neighbours8(sk) * sk
        ends = list(zip(*np.nonzero((nb == 1))))
        h, w = sk.shape
        for (y, x) in ends:
            path = [(y, x)]
            py, px = y, x
            prev = None
            ok = False
            while len(path) <= length:
                nxt = []
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        if not (dy or dx):
                            continue
                        yy, xx = py + dy, px + dx
                        if 0 <= yy < h and 0 <= xx < w and sk[yy, xx] and (yy, xx) != prev and (yy, xx) not in path:
                            nxt.append((yy, xx))
                if len(nxt) != 1:
                    ok = len(nxt) >= 2   # reached a junction: this was a spur
                    break
                prev = (py, px)
                py, px = nxt[0]
                # a junction pixel has >2 neighbours
                if neighbours8(sk[max(0, py - 1):py + 2, max(0, px - 1):px + 2])[min(py, 1), min(px, 1)] > 2:
                    ok = True
                    break
                path.append((py, px))
            if ok and len(path) < length:
                for (yy, xx) in path:
                    sk[yy, xx] = False
    return sk


# ── connected components (run-length + union-find) ───────────────────────────────────────────────
def label(m, conn8=True):
    """Label connected components of a bool mask. Returns (labels int32, count)."""
    h, w = m.shape
    parent = [0]

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    def union(a, b):
        a, b = find(a), find(b)
        if a != b:
            if a < b:
                parent[b] = a
            else:
                parent[a] = b

    runs_prev = []
    all_runs = []
    for y in range(h):
        row = m[y]
        d = np.diff(np.concatenate(([0], row.astype(np.int8), [0])))
        starts = np.nonzero(d == 1)[0]
        ends = np.nonzero(d == -1)[0]
        runs = []
        for s, e in zip(starts.tolist(), ends.tolist()):
            lab = len(parent)
            parent.append(lab)
            lo, hi = (s - 1, e + 1) if conn8 else (s, e)
            for ps, pe, pl in runs_prev:
                if ps < hi and pe > lo:
                    union(lab, pl)
            runs.append((s, e, lab))
            all_runs.append((y, s, e, lab))
        runs_prev = runs
    out = np.zeros((h, w), np.int32)
    remap = {}
    for (y, s, e, lab) in all_runs:
        r = find(lab)
        if r not in remap:
            remap[r] = len(remap) + 1
        out[y, s:e] = remap[r]
    return out, len(remap)


# ── bounded exact EDT with nearest-feature index ─────────────────────────────────────────────────
def edt_nearest(feat, R):
    """For every pixel: squared Euclidean distance to the nearest True pixel of `feat` (if <= R),
    and that feature's (y, x). Pixels farther than R get d2 = BIG and index -1. Exact within R."""
    h, w = feat.shape
    BIG = 10 ** 9
    # pass 1: along columns
    g = np.full((h, w), BIG, np.int64)
    gy = np.full((h, w), -1, np.int64)
    ys = np.arange(h)[:, None].repeat(w, 1)
    for dy in range(-R, R + 1):
        f = shift(feat, -dy, 0)            # f[y,x] = feat[y+dy, x]
        cand = dy * dy
        better = f & (cand < g)
        g[better] = cand
        gy[better] = (ys + dy)[better]
    # pass 2: along rows
    d2 = np.full((h, w), BIG, np.int64)
    ny = np.full((h, w), -1, np.int64)
    nx = np.full((h, w), -1, np.int64)
    xs = np.arange(w)[None, :].repeat(h, 0)
    for dx in range(-R, R + 1):
        gs = shift(g, 0, -dx, fill=BIG)    # gs[y,x] = g[y, x+dx]
        gys = shift(gy, 0, -dx, fill=-1)
        cand = gs + dx * dx
        better = (gs < BIG) & (cand < d2)
        d2[better] = cand[better]
        ny[better] = gys[better]
        nx[better] = (xs + dx)[better]
    return d2, ny, nx


# ── geodesic distance along a skeleton ───────────────────────────────────────────────────────────
def geodesic(sk, start):
    """Dijkstra over 8-connected skeleton pixels from start (y, x). Returns float array (inf off)."""
    h, w = sk.shape
    dist = np.full((h, w), np.inf)
    sy, sx = start
    dist[sy, sx] = 0.0
    pq = [(0.0, sy, sx)]
    r2 = 2 ** 0.5
    while pq:
        d, y, x = heapq.heappop(pq)
        if d > dist[y, x]:
            continue
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if not (dy or dx):
                    continue
                yy, xx = y + dy, x + dx
                if 0 <= yy < h and 0 <= xx < w and sk[yy, xx]:
                    nd = d + (r2 if dy and dx else 1.0)
                    if nd < dist[yy, xx]:
                        dist[yy, xx] = nd
                        heapq.heappush(pq, (nd, yy, xx))
    return dist
