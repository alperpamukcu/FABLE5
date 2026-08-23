# -*- coding: utf-8 -*-
"""Cut the window's palm plate into the parts that move on their own.

The generator returns ONE plate with both trees and the ground plants on it. Drawn as one
sprite, both trees lean in perfect lockstep, which is the one thing a pair of trees never
does. So the plate is cut here, once, into six full-canvas layers:

    window_palm_l / window_palm_l_crown     the left tree's pole and its fronds
    window_palm_r / window_palm_r_crown     the right tree's
    window_plants_l / window_plants_r       the ground plants at the foot of each

Every layer keeps the plate's own 141x274 canvas, so the game hangs all six at the window's
centre and does not have to carry an offset per layer -- the only numbers that cross into
the code are the PIVOTS this script prints, and they are art px on that same canvas.

The cut is not a box. The two crowns touch, so the trees are separated by geodesic distance
through the ink itself: every pixel goes to the pole it can be reached from soonest, which
puts the seam in the contact zone where the fronds already interleave. The plants are lifted
out first, by flooding them with the pole erased -- they hang off the trees ONLY through the
pole, so erasing it disconnects them cleanly.

AND THE CUT IS NOT A PARTITION EITHER (2026-08-23, the author: "palmiyeler tam degil
bazilarinin uclari yok onlarida tamamlayabilirsin"). Where the crowns overlap, the plate
holds ONE silhouette for TWO trees' fronds, and it does not record what is underneath.
Handing each of those pixels to a single tree therefore truncates the other one: the right
tree came out with two blunt stumps on its left where its fronds run under the left tree's.

So the contested ink is SHARED. CROWN_SHARE says how far past the seam each crown keeps
reaching, in geodesic steps: the ink is drawn twice, which costs nothing when both trees are
the same black silhouette, and it means no hole can ever open along the seam whichever way
the two lean. The right tree is the one that reaches (30); the left tree is already whole and
takes nothing (0), because everything it is missing is a notch that sits inside the right
crown's own reach and is covered by it at every lean. These two numbers were chosen by
LOOKING at each crown drawn on its own -- 16 left a hole, 44 had one tree swallow the other.

Run:  python Tools/window_palms_split.py
"""
import os
from collections import Counter, deque

from PIL import Image

SRC = r'Tools/AssetPipeline/sources/window_palms.png'
OUT = r'Assets/Resources/Scene'

WHITE = 150          # a pixel this bright everywhere is a star the sky baked into a frond
POLE_MAX_W = 14      # wider than this and the run is not a pole any more, it is the crown
GROUND_Y = 274       # the plate's bottom edge: where a trunk's root would stand
CROWN_SHARE = {      # how far past the seam each crown keeps claiming ink, geodesic steps
    'l': 0,          # already whole: its notches sit inside the right crown and stay covered
    'r': 30,         # the occluded one: this is what gives it its left-hand fronds back
}


def load():
    im = Image.open(SRC).convert('RGBA')
    return im, im.size[0], im.size[1], im.load()


def despeck(px, w, h):
    """Paint out the baked stars. They sit INSIDE the silhouette -- 4 to 8 opaque
    neighbours each -- so a hole would show sky through a frond. They are filled with the
    commonest colour around them instead, which is the frond they are sitting on."""
    hits = [(x, y) for y in range(h) for x in range(w)
            if px[x, y][3] > 8 and min(px[x, y][:3]) > WHITE]
    for x, y in hits:
        near = Counter()
        for dy in (-2, -1, 0, 1, 2):
            for dx in (-2, -1, 0, 1, 2):
                nx, ny = x + dx, y + dy
                if (dx or dy) and 0 <= nx < w and 0 <= ny < h:
                    r, g, b, a = px[nx, ny]
                    if a > 8 and min(r, g, b) <= WHITE:
                        near[(r, g, b, a)] += 1
        if near:
            px[x, y] = near.most_common(1)[0][0]
    return len(hits)


def row_runs(px, w, y):
    out, cur = [], None
    for x in range(w):
        on = px[x, y][3] > 8
        if on and cur is None:
            cur = x
        elif not on and cur is not None:
            out.append((cur, x - 1))
            cur = None
    if cur is not None:
        out.append((cur, w - 1))
    return out


def trace_pole(px, w, h, start_y, start_run):
    """Follow one trunk DOWN from a row where it is already on its own, taking the run that
    best continues it. It ends where it walks out of the frame, which is what a trunk does:
    the root is below the sill and the plate stops at the sill."""
    pole = {}
    y, run = start_y, start_run
    while y < h:
        pole[y] = run
        if y + 1 >= h:
            break
        cx = (run[0] + run[1]) * 0.5
        best = None
        for r in row_runs(px, w, y + 1):
            if r[1] - r[0] + 1 > POLE_MAX_W:
                continue
            d = abs((r[0] + r[1]) * 0.5 - cx)
            if d <= 4.0 and (best is None or d < best[0]):
                best = (d, r)
        if best is None:
            break
        y, run = y + 1, best[1]
    return pole


def trace_pole_up(px, w, h, start_y, start_run):
    """The same walk upwards, to find where the pole disappears into the fronds. That row is
    the JUNCTION: the point the crown turns about, so the fronds stay rooted to the trunk."""
    y, run = start_y, start_run
    got = {y: run}
    while y > 0:
        cx = (run[0] + run[1]) * 0.5
        best = None
        for r in row_runs(px, w, y - 1):
            if r[1] - r[0] + 1 > POLE_MAX_W:
                continue
            d = abs((r[0] + r[1]) * 0.5 - cx)
            if d <= 3.0 and (best is None or d < best[0]):
                best = (d, r)
        if best is None:
            break
        y, run = y - 1, best[1]
        got[y] = run
    return got, y, run


def flood(px, w, h, seeds, blocked):
    seen = set()
    q = deque()
    for s in seeds:
        if s not in blocked and px[s[0], s[1]][3] > 8:
            seen.add(s)
            q.append(s)
    while q:
        x, y = q.popleft()
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                n = (x + dx, y + dy)
                if (dx or dy) and 0 <= n[0] < w and 0 <= n[1] < h \
                        and n not in seen and n not in blocked and px[n[0], n[1]][3] > 8:
                    seen.add(n)
                    q.append(n)
    return seen


def distance_field(seeds, live):
    """Steps THROUGH THE INK from a set of seeds. Two crowns that merely brush each other
    part along the brush; a box cut would slice fronds in half."""
    d = {}
    q = deque()
    for s in seeds:
        if s in live:
            d[s] = 0
            q.append(s)
    while q:
        x, y = q.popleft()
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                n = (x + dx, y + dy)
                if (dx or dy) and n in live and n not in d:
                    d[n] = d[(x, y)] + 1
                    q.append(n)
    return d


def write(px, w, h, cells, name):
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    o = im.load()
    for x, y in cells:
        o[x, y] = px[x, y]
    path = os.path.join(OUT, name + '.png')
    im.save(path)
    return path, len(cells)


def main():
    im, w, h, px = load()
    print('source %dx%d' % (w, h))
    print('stars painted out of the fronds: %d' % despeck(px, w, h))

    # The two poles are alone on their rows well below the crowns and well above the plants.
    runs = row_runs(px, w, 170)
    assert len(runs) == 2, 'row 170 should carry exactly the two trunks, got %s' % (runs,)
    left_run, right_run = runs

    poles, roots, joints = {}, {}, {}
    for name, run in (('l', left_run), ('r', right_run)):
        down = trace_pole(px, w, h, 170, run)
        up, jy, jrun = trace_pole_up(px, w, h, 170, run)
        pole = dict(down)
        pole.update(up)
        poles[name] = pole
        joints[name] = ((jrun[0] + jrun[1]) * 0.5, float(jy))
        # The root is off the plate: the sill cuts the trunk before the ground does. Extend
        # the pole's own line to the ground row and turn about THAT, or the tree pivots about
        # a point half way up itself and swings like a hanged sign.
        ys = sorted(pole)
        y0, y1 = ys[len(ys) // 2], ys[-1]
        c0 = (pole[y0][0] + pole[y0][1]) * 0.5
        c1 = (pole[y1][0] + pole[y1][1]) * 0.5
        slope = (c1 - c0) / float(max(1, y1 - y0))
        roots[name] = (c1 + slope * (GROUND_Y - y1), float(GROUND_Y))
        print('pole %s: rows %d..%d, junction (%.1f,%.1f), root (%.1f,%.1f)'
              % (name, ys[0], ys[-1], joints[name][0], joints[name][1],
                 roots[name][0], roots[name][1]))

    pole_cells = set()
    for name in poles:
        for y, (a, b) in poles[name].items():
            for x in range(a, b + 1):
                pole_cells.add((x, y))

    ink = set((x, y) for y in range(h) for x in range(w) if px[x, y][3] > 8)

    # The plants hang off the trees only through the pole, so with the pole erased they fall
    # off on their own. Seeded from the two bottom corners, which is where they grow.
    plants = {}
    for name, keep in (('l', lambda x: x < 55), ('r', lambda x: x > 85)):
        # Seeded from every scrap of ink along the bottom of its own corner rather than one
        # chosen pixel: which pixel is opaque down there is the art's business, not this
        # script's, and a seed that misses returns an empty layer without complaining.
        seeds = [(x, y) for y in range(252, h) for x in range(w)
                 if keep(x) and px[x, y][3] > 8 and (x, y) not in pole_cells]
        got = flood(px, w, h, seeds, pole_cells)
        plants[name] = got
        xs = [c[0] for c in got]
        ys = [c[1] for c in got]
        print('plants %s: %5d px  x[%d..%d] y[%d..%d]'
              % (name, len(got), min(xs), max(xs), min(ys), max(ys)))

    live = ink - plants['l'] - plants['r']
    dist = {n: distance_field(set(
        (x, y) for y, (a, b) in poles[n].items() for x in range(a, b + 1)), live)
        for n in ('l', 'r')}
    stray = [c for c in live if c not in dist['l'] and c not in dist['r']]
    print('ink no pole can reach: %d' % len(stray))

    crowns = {}
    for name in ('l', 'r'):
        other = 'r' if name == 'l' else 'l'
        mine, theirs, share = dist[name], dist[other], CROWN_SHARE[name]
        pole = set((x, y) for y, (a, b) in poles[name].items()
                   for x in range(a, b + 1))
        tree = set()
        for c in live:
            dm = mine.get(c)
            if dm is None:
                continue
            dt = theirs.get(c)
            # Nearer to my pole, or close enough behind that the pixel is contested and I
            # keep my claim on it as well. Never both a trunk and a crown.
            if dt is None or dm <= dt or dm - dt <= share:
                tree.add(c)
        crowns[name] = tree - pole
        p, n = write(px, w, h, tree & pole, 'window_palm_' + name)
        print('  %-40s %5d px' % (p, n))
        p, n = write(px, w, h, crowns[name], 'window_palm_%s_crown' % name)
        print('  %-40s %5d px' % (p, n))
        p, n = write(px, w, h, plants[name], 'window_plants_' + name)
        print('  %-40s %5d px' % (p, n))

    shared = crowns['l'] & crowns['r']
    print('ink both crowns carry: %d (%.1f%% of the plate)'
          % (len(shared), 100.0 * len(shared) / len(ink)))
    carried = set()
    for name in ('l', 'r'):
        carried |= crowns[name] | plants[name]
        carried |= set((x, y) for y, (a, b) in poles[name].items()
                       for x in range(a, b + 1))
    print('ink accounted for: %d of %d (%.2f%%)'
          % (len(carried & ink), len(ink), 100.0 * len(carried & ink) / len(ink)))
    print('PIVOTS for DiegeticStage (art px on the 141x274 plate, y from the TOP):')
    for name in ('l', 'r'):
        print('  %s root (%.1f, %.1f)   junction (%.1f, %.1f)'
              % (name, roots[name][0], roots[name][1], joints[name][0], joints[name][1]))


if __name__ == '__main__':
    main()
