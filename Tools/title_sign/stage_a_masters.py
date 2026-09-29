# -*- coding: utf-8 -*-
"""STAGE A - read the two logo masters once and keep only what the pixel re-draw needs.

The masters are the full-resolution neon drawings the shipped menu_logo.png (560x210) was cut from
(Tools/steam_kit/out/logo/, local to the author's machine). Every master pixel is already a palette
token at one of six alphas, so the parts can be read off by exact colour:

  mark (malibu_club_script_01_master.png, 2638x702)
    tube  = alpha 255                       core   = Cream[4] @255
    lining= Magenta[4] @255 (+ core)        body   = Magenta[3] @255
    12 tube components = the 12 letter groups (M+swash, A, L, I, B, U, C, L, U, B, swash, coupe)
    coupe = the component carrying Cyan[4]; its bowl's enclosed hole = the liquid
  line (cocktail_bar_simulator_tagdeco_00_master.png, 3027x833)
    tube  = alpha 255, core = Cream[4] @255, 20 components = the 20 letters

Writes work/masters.npz.
"""
import os
import numpy as np
from PIL import Image
from logo_lib import label

HERE = os.path.dirname(os.path.abspath(__file__))
LOGO = os.path.join(HERE, '..', 'steam_kit', 'out', 'logo')     # local to the author's machine, not in git
WM = os.path.join(LOGO, 'malibu_club_script_01_master.png')
TG = os.path.join(LOGO, 'cocktail_bar_simulator_tagdeco_00_master.png')

# group ids, in reading order. 1..11 magenta (the name), 12 the coupe, 13..32 the genre line.
MARK_NAMES = ['M', 'A', 'L', 'I', 'B', 'U', 'C', 'L2', 'U2', 'B2', 'SWASH', 'COUPE']
LINE_TEXT = 'COCKTAILBARSIMULATOR'


def is_col(a, hexv, alpha=255):
    r, g, b = (hexv >> 16) & 255, (hexv >> 8) & 255, hexv & 255
    return (a[..., 0] == r) & (a[..., 1] == g) & (a[..., 2] == b) & (a[..., 3] == alpha)


def main():
    a = np.asarray(Image.open(WM).convert('RGBA')).astype(np.int32)
    tube = a[..., 3] == 255
    core = is_col(a, 0xF2E8D5)
    lining = is_col(a, 0xFF7DC6) | core
    lab, n = label(tube)
    assert n == 12, n
    comps = []
    for i in range(1, n + 1):
        m = lab == i
        ys, xs = np.nonzero(m)
        cy = (is_col(a, 0x7DF0E3) & m).sum()
        comps.append(dict(i=i, x0=xs.min(), x1=xs.max(), y0=ys.min(), y1=ys.max(), n=len(xs), cy=cy))
    coupe = [c for c in comps if c['cy'] > 1000]
    assert len(coupe) == 1
    coupe = coupe[0]
    rest = [c for c in comps if c is not coupe]
    # the second swash is the wide low component under C-L-U-B
    swash = max(rest, key=lambda c: (c['x1'] - c['x0']) * (c['y0'] > 400))
    letters = sorted([c for c in rest if c is not swash], key=lambda c: c['x0'])
    order = letters + [swash, coupe]
    groups = np.zeros(tube.shape, np.uint8)
    for gid, c in enumerate(order, start=1):
        groups[lab == c['i']] = gid
        print('mark group %2d %-6s x %4d..%4d y %3d..%3d n=%d' % (gid, MARK_NAMES[gid - 1], c['x0'], c['x1'],
                                                                 c['y0'], c['y1'], c['n']))
    # the coupe's enclosed holes; the bowl (the biggest) is the liquid
    cm = lab == coupe['i']
    x0, x1, y0, y1 = coupe['x0'] - 2, coupe['x1'] + 3, coupe['y0'] - 2, coupe['y1'] + 3
    sub = ~cm[y0:y1, x0:x1]
    hl, hn = label(sub, conn8=False)
    border = set(np.unique(np.concatenate([hl[0], hl[-1], hl[:, 0], hl[:, -1]])).tolist())
    holes = [(int((hl == k).sum()), k) for k in range(1, hn + 1) if k not in border]
    holes.sort(reverse=True)
    print('coupe holes (px):', [h for h, _ in holes])
    liquid = np.zeros(tube.shape, bool)
    liquid[y0:y1, x0:x1] = hl == holes[0][1]
    # the other holes (rim slot, garnish eye, foot) stay open: the halo reaches into them

    b = np.asarray(Image.open(TG).convert('RGBA')).astype(np.int32)
    ltube = b[..., 3] == 255
    lcore = is_col(b, 0xF2E8D5)
    llab, ln = label(ltube)
    assert ln == 20, ln
    lcomps = []
    for i in range(1, ln + 1):
        xs = np.nonzero((llab == i).any(axis=0))[0]
        lcomps.append((xs.min(), i))
    lcomps.sort()
    lgroups = np.zeros(ltube.shape, np.uint8)
    for k, (_, i) in enumerate(lcomps):
        lgroups[llab == i] = 13 + k
    print('line groups 13..32 =', LINE_TEXT)
    os.makedirs(os.path.join(HERE, 'work'), exist_ok=True)
    np.savez_compressed(os.path.join(HERE, 'work', 'masters.npz'),
                        m_tube=tube, m_core=core, m_lining=lining, m_groups=groups, m_liquid=liquid,
                        l_tube=ltube, l_core=lcore, l_groups=lgroups)
    print('ok')


if __name__ == '__main__':
    main()
