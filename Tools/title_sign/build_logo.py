# -*- coding: utf-8 -*-
"""STAGE B - draw the title lockup pixel-native at one screen scale, as a MAP the renderer lights.

Every set is DRAWN at its own size (never resized from another set): the masters are sampled
straight onto that set's grid, so a texel is one screen pixel at the HUD factor the set is for.

    set   texture      1 texel = 1 screen px at           footprint (canvas units)
    x1    560 x 212    1280x720  (HUD factor 1.0)          560 x 212, centred (0, +168)
    x1.25 700 x 265    1600x900  (1.25)
    x1.5  840 x 318    1920x1080 (1.5)
    x2    1120 x 424   2560x1440 (2.0)
    x2.5  1400 x 530   3200x1800 (2.5)
    x3    1680 x 636   3840x2160 (3.0)
    x4    2240 x 848   5120x2880 (4.0)

What is drawn (the map's classes, R channel):
    0 empty   1..4 glow bands (1 = outermost, 4 = innermost)   5 tube body   6 lining   7 core
    8 liquid (the coupe's bowl)
G = the group (1..11 the name's letters in reading order, 12 the coupe, 13..32 the genre line's
letters), B = where along its own tube the pixel sits (0..255, from the tube's start), or for the
liquid a diagonal coordinate across the bowl. The renderer turns (class, group, B, time) into a
palette token at an alpha - Assets/Scripts/UI/Art/TitleSignLight.cs (ship.py puts the maps in Resources/Logo).

Shape rules (all on the set's own grid, no anti-aliasing):
  * tube, lining and core are the masters' own shapes, sampled by AREA (8x8-ish sub-samples per
    texel) and cut at half coverage - the same drawing as the store art, aliased, nothing smoothed.
  * the genre line's core is too thin to survive that at the small sets (1.3 texels at x1), so it
    is the tube's own skeleton there: one texel, unbroken.
  * the glow is four flat bands grown from the tube by exact Euclidean distance (reach per set
    below, scaled from the x1 numbers and rounded), each band ONE token at ONE alpha.
"""
import json
import math
import os
import sys
import numpy as np
from PIL import Image
from logo_lib import zhang_suen, edt_nearest, geodesic, neighbours8, label, shift

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'out')

FOOT_W, FOOT_H = 560, 212
SETS = {'x1': 1.0, 'x1.25': 1.25, 'x1.5': 1.5, 'x2': 2.0, 'x2.5': 2.5, 'x3': 3.0, 'x4': 4.0}

# where each master sits in the footprint (canvas units): the master's core-bbox corner m0 lands on
# t0 at `scale` canvas units per master px (measured off the shipped 560 lockup, +1 down for 212).
PLACE = {
    'mark': dict(scale=0.2122, m0=(73, 74), t0=(16.0, 17.0)),
    'line': dict(scale=0.1915, m0=(177, 549), t0=(11.0, 157.0)),
}

# glow reach per band at x1, in canvas units from the tube's edge (inner -> outer): the band a
# texel is in is the first whose reach covers its distance to the nearest tube texel.
REACH = {'mark': [2.0, 4.5, 7.0, 9.0], 'line': [1.0, 2.5, 3.6, 4.6]}

NAMES = ['M', 'A', 'L', 'I', 'B', 'U', 'C', 'L', 'U', 'B', 'swash', 'coupe'] + list('COCKTAILBARSIMULATOR')

CLS_EMPTY, CLS_BAND = 0, (1, 2, 3, 4)      # 1 outermost .. 4 innermost
CLS_BODY, CLS_LINING, CLS_CORE, CLS_LIQUID = 5, 6, 7, 8


def sample(part, masks, s, W, H, bbox):
    """Area coverage of each master mask over each texel of the set, inside bbox (canvas units).
    masks: dict name -> bool/uint8 master array. Returns dict name -> float coverage (H, W), and
    for 'groups' the majority label."""
    pl = PLACE[part]
    sc = pl['scale']
    N = max(2, int(math.ceil(1.0 / (sc * s * 0.6))))
    u0, v0, u1, v1 = bbox
    j0, j1 = max(0, int(u0 * s) - 1), min(W, int(math.ceil(u1 * s)) + 1)
    i0, i1 = max(0, int(v0 * s) - 1), min(H, int(math.ceil(v1 * s)) + 1)
    jj = (np.arange(j0, j1)[:, None] + (np.arange(N)[None, :] + 0.5) / N) / s     # canvas u
    ii = (np.arange(i0, i1)[:, None] + (np.arange(N)[None, :] + 0.5) / N) / s     # canvas v
    mx = np.floor(pl['m0'][0] + (jj - pl['t0'][0]) / sc).astype(int)              # (w, N)
    my = np.floor(pl['m0'][1] + (ii - pl['t0'][1]) / sc).astype(int)              # (h, N)
    out = {}
    any_m = next(iter(masks.values()))
    mh, mw = any_m.shape
    okx = (mx >= 0) & (mx < mw)
    oky = (my >= 0) & (my < mh)
    mxc = np.clip(mx, 0, mw - 1)
    myc = np.clip(my, 0, mh - 1)
    # sample grid (h, N, w, N)
    for name, m in masks.items():
        smp = m[myc[:, :, None, None], mxc[None, None, :, :]]
        smp = smp * (oky[:, :, None, None] & okx[None, None, :, :])
        if name == 'groups':
            h_, w_ = smp.shape[0], smp.shape[2]
            flat = smp.transpose(0, 2, 1, 3).reshape(h_, w_, N * N)
            labs = np.unique(flat)
            best = np.zeros((h_, w_), np.uint8)
            bestc = np.zeros((h_, w_), np.int32)
            for L in labs:
                if L == 0:
                    continue
                c = (flat == L).sum(-1)
                upd = c > bestc
                best[upd] = L
                bestc[upd] = c[upd]
            full = np.zeros((H, W), np.uint8)
            full[i0:i1, j0:j1] = best
            out[name] = full
        else:
            cov = smp.astype(np.float32).mean(axis=(1, 3))
            full = np.zeros((H, W), np.float32)
            full[i0:i1, j0:j1] = cov
            out[name] = full
    return out


def part_bbox(part, mask):
    ys, xs = np.nonzero(mask)
    pl = PLACE[part]
    f = lambda x, a: pl['t0'][a] + (x - pl['m0'][a]) * pl['scale']
    return (f(xs.min(), 0) - 1, f(ys.min(), 1) - 1, f(xs.max() + 1, 0) + 1, f(ys.max() + 1, 1) + 1)


def build(s, masters):
    W, H = int(round(FOOT_W * s)), int(round(FOOT_H * s))
    d = masters
    # ── sample the two masters onto this set's grid ───────────────────────────────────────────
    mk = sample('mark', {'tube': d['m_tube'], 'core': d['m_core'], 'lining': d['m_lining'],
                         'liquid': d['m_liquid'], 'groups': d['m_groups']}, s, W, H,
                part_bbox('mark', d['m_tube']))
    ln = sample('line', {'tube': d['l_tube'], 'core': d['l_core'], 'groups': d['l_groups']}, s, W, H,
                part_bbox('line', d['l_tube']))
    groups = np.where(mk['tube'] >= 0.5, mk['groups'], 0).astype(np.uint8)
    lg = np.where(ln['tube'] >= 0.5, ln['groups'], 0).astype(np.uint8)
    groups = np.where(groups > 0, groups, lg)
    tube = groups > 0
    cls = np.zeros((H, W), np.uint8)
    cls[tube] = CLS_BODY
    lining = (mk['lining'] >= 0.5) & tube & (groups <= 11)
    cls[lining] = CLS_LINING
    core = ((mk['core'] >= 0.5) | (ln['core'] >= 0.5)) & tube
    # the genre line: a thin core that breaks under area sampling is replaced by the skeleton
    line_part = (groups >= 13)
    skel_all = np.zeros((H, W), bool)
    boxes = {}
    for g in range(1, 33):
        m = groups == g
        if m.any():
            ys, xs = np.nonzero(m)
            y0, y1, x0, x1 = ys.min() - 1, ys.max() + 2, xs.min() - 1, xs.max() + 2
            boxes[g] = (y0, y1, x0, x1)
            skel_all[y0:y1, x0:x1] |= zhang_suen(m[y0:y1, x0:x1])
    if s <= 1.25:
        core = np.where(line_part, skel_all, core)        # one texel, unbroken
    else:
        core = core | (line_part & skel_all)              # the drawn core, never broken
    cls[core] = CLS_CORE
    # the coupe: its body has no lining; its core is where the master's core is
    liquid = (mk['liquid'] >= 0.5) & ~tube
    cls[liquid] = CLS_LIQUID
    lgroup = np.where(liquid, 12, 0)

    # ── the glow: four flat bands by distance from the nearest tube texel ────────────────────────
    R = int(math.ceil(max(REACH['mark']) * s)) + 2
    d2, ny, nx = edt_nearest(tube, R)
    owner = np.zeros((H, W), np.uint8)
    ok = ny >= 0
    owner[ok] = groups[ny[ok], nx[ok]]
    dist = np.sqrt(d2.astype(np.float64))
    for part, sel in (('mark', (owner >= 1) & (owner <= 11)), ('cyan', owner >= 12)):
        reach = REACH['mark'] if part == 'mark' else None
        if part == 'cyan':
            # the coupe glows like the name (it stands beside it); the genre line has its own reach
            for sub, rr in ((owner == 12, REACH['mark']), (owner >= 13, REACH['line'])):
                m = sub & ~tube & ~liquid
                for k, r in enumerate(rr):              # inner (4) .. outer (1)
                    band = m & (dist <= r * s + 0.5) & (cls == 0)
                    cls[band] = 4 - k
        else:
            m = sel & ~tube & ~liquid
            for k, r in enumerate(reach):
                band = m & (dist <= r * s + 0.5) & (cls == 0)
                cls[band] = 4 - k
    grp = np.where(tube, groups, np.where(cls > 0, owner, 0)).astype(np.uint8)
    grp[liquid] = 12

    # ── where along its tube each texel sits: geodesic distance on the group's skeleton ─────────
    phase = np.zeros((H, W), np.uint8)
    lengths = {}
    pad = R + 2
    for g in range(1, 33):
        if g not in boxes:
            continue
        y0, y1, x0, x1 = boxes[g]
        y0, x0 = max(0, y0 - pad), max(0, x0 - pad)
        y1, x1 = min(H, y1 + pad), min(W, x1 + pad)
        sk = skel_all[y0:y1, x0:x1] & (groups[y0:y1, x0:x1] == g)
        if not sk.any():
            continue
        nb = neighbours8(sk) * sk
        ys, xs = np.nonzero(sk & (nb == 1))
        if len(ys) == 0:
            ys, xs = np.nonzero(sk)
        # the tube starts at its left-and-lowest free end (where a sign-maker would put the electrode)
        k = int(np.argmin(xs - 0.35 * ys))
        gd = geodesic(sk, (ys[k], xs[k]))
        fin = np.isfinite(gd)
        # disconnected skeleton bits (rare at small sets): chain them on from their own left end
        base = gd[fin].max() if fin.any() else 0.0
        rest = sk & ~fin
        while rest.any():
            ry, rx = np.nonzero(rest)
            k2 = int(np.argmin(rx - 0.35 * ry))
            gd2 = geodesic(rest, (ry[k2], rx[k2]))
            f2 = np.isfinite(gd2)
            gd[f2] = gd2[f2] + base + 1.0
            base = gd[np.isfinite(gd)].max()
            rest = rest & ~f2
        L = float(gd[np.isfinite(gd)].max()) or 1.0
        lengths[g] = L / s                                    # canvas units
        # every texel of this group (tube and glow) takes the phase of its nearest skeleton texel
        _, nys, nxs = edt_nearest(sk, pad + int(4 * s) + 4)
        mine = (grp[y0:y1, x0:x1] == g) & (nys >= 0)
        sub = phase[y0:y1, x0:x1]
        sub[mine] = np.clip(np.round(gd[nys[mine], nxs[mine]] / L * 255), 0, 255).astype(np.uint8)
    # the liquid: a diagonal coordinate across the bowl (0 at its upper-left, 255 at lower-right)
    if liquid.any():
        ys, xs = np.nonzero(liquid)
        q = xs + ys
        phase[liquid] = np.round((q - q.min()) / max(1, q.max() - q.min()) * 255).astype(np.uint8)
    return dict(W=W, H=H, cls=cls, grp=grp, phase=phase, lengths=lengths)


def load_masters():
    return dict(np.load(os.path.join(HERE, 'work', 'masters.npz')))


def save_map(name, b):
    os.makedirs(os.path.join(OUT, 'maps'), exist_ok=True)
    rgb_ = np.stack([b['cls'], b['grp'], b['phase']], -1).astype(np.uint8)
    Image.fromarray(rgb_, 'RGB').save(os.path.join(OUT, 'maps', 'logo_%s_map.png' % name), optimize=True)


if __name__ == '__main__':
    import time
    masters = load_masters()
    todo = sys.argv[1:] or list(SETS)
    meta = {}
    for name in todo:
        t = time.time()
        b = build(SETS[name], masters)
        save_map(name, b)
        meta[name] = dict(scale=SETS[name], w=b['W'], h=b['H'],
                          lengths={str(k): round(v, 2) for k, v in sorted(b['lengths'].items())})
        ys, xs = np.nonzero(b['cls'] > 0)
        print('%-6s %4dx%-4d  lit bbox x %d..%d y %d..%d  (%.1fs)' % (name, b['W'], b['H'], xs.min(), xs.max(),
                                                                     ys.min(), ys.max(), time.time() - t))
    p = os.path.join(OUT, 'maps', 'sets.json')
    old = json.load(open(p)) if os.path.exists(p) else {}
    old.update(meta)
    json.dump(old, open(p, 'w'), indent=1)
