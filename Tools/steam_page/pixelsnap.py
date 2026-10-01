# -*- coding: utf-8 -*-
"""Turn an image model's "pixel-art-looking" picture into TRUE pixel art: one grid, one palette.

An image model draws pixel art as soft blocks of roughly even size (a 4K take of ours used ~12 screen px per art
pixel). Upscaling that directly gives blurry, uneven "pixels". Instead:

  1. find the implied grid: the period and phase of the edges, per axis (comb search over the gradient profile);
  2. sample each cell from its CENTRE (the borders carry the model's blur) -> the native-resolution picture;
  3. snap every native pixel to ONE shared palette (the game's UITheme ramps plus the colours the art needs),
     with no dithering, so every layer of the key art shares the same inks;
  4. from then on the picture is only ever enlarged by whole numbers with NEAREST.

    python3 pixelsnap.py in.png out.png [--period 12] [--palette pal.png]
"""
import argparse
import numpy as np
from PIL import Image


def _profile(a, axis):
    lum = a[..., :3].astype(np.float32) @ np.array([0.299, 0.587, 0.114], np.float32)
    g = np.abs(np.diff(lum, axis=axis))
    return g.sum(axis=1 - axis)          # edge energy along the axis


def grid(a, lo=3, hi=40):
    """(period, phase) per axis. The period is the autocorrelation's first strong peak of the edge profile (the
    comb search alone prefers multiples of the true period), refined to a fraction of a pixel by a comb search
    around it; the phase is the comb offset that lands on the most edge energy."""
    out = []
    for axis in (1, 0):                  # x then y
        prof = _profile(a, axis)
        prof = prof - np.convolve(prof, np.ones(31) / 31, mode='same')
        prof = np.clip(prof, 0, None)
        x = prof - prof.mean()
        ac = np.correlate(x, x, 'full')[len(x) - 1:]
        ac = ac / ac[0]
        peaks = [i for i in range(lo, hi) if ac[i] > ac[i - 1] and ac[i] >= ac[i + 1]]
        top = max(ac[i] for i in peaks)
        p0 = next(i for i in peaks if ac[i] >= 0.6 * top)
        best = None
        for p in np.arange(p0 - 0.8, p0 + 0.81, 0.02):
            for ph in np.arange(0, p, 0.25):
                k = np.round(np.arange(ph, len(prof) - 1, p)).astype(int)
                sc = prof[k].mean()
                if best is None or sc > best[0]:
                    best = (sc, p, ph)
        out.append((float(best[1]), float(best[2])))
    return out


def sample(a, gx, gy, centre=0.5):
    """Native picture: each cell's centre region averaged (median), so the model's soft borders drop out."""
    (px, ox), (py, oy) = gx, gy
    H, W = a.shape[:2]
    nx = int((W - ox) // px)
    ny = int((H - oy) // py)
    out = np.zeros((ny, nx, a.shape[2]), np.uint8)
    r = max(1, int(round(min(px, py) * centre / 2)))
    for j in range(ny):
        cy = int(oy + (j + 0.5) * py)
        y0, y1 = max(0, cy - r), min(H, cy + r + 1)
        for i in range(nx):
            cx = int(ox + (i + 0.5) * px)
            x0, x1 = max(0, cx - r), min(W, cx + r + 1)
            out[j, i] = np.median(a[y0:y1, x0:x1].reshape(-1, a.shape[2]), axis=0)
    return out


# ── palette ─────────────────────────────────────────────────────────────────────────────────────────
RAMPS = {
    'Night':    [0x0D0813, 0x1A1023, 0x241830, 0x362447, 0x4A3160],
    'Magenta':  [0x5C1B45, 0x8F2464, 0xC23283, 0xE84DA6, 0xFF7DC6],
    'Cyan':     [0x123B45, 0x1B5F66, 0x26918F, 0x3BC8BE, 0x7DF0E3],
    'ClubBlue': [0x131B3D, 0x1F2E66, 0x2E4699, 0x4467CC, 0x6E93F0],
    'Cream':    [0x453E38, 0x6E6459, 0x9C8F80, 0xC9BCA8, 0xF2E8D5],
    'Graphite': [0x14161A, 0x24272D, 0x383D45, 0x545A64, 0x808893],
    'Amber':    [0x4A2E14, 0x8F5A1E, 0xC9822B, 0xE8A33D, 0xF5C97B],
}


def game_inks():
    return np.array([((h >> 16) & 255, (h >> 8) & 255, h & 255) for r in RAMPS.values() for h in r], np.float32)


def _lab(rgb):
    c = rgb / 255.0
    c = np.where(c > 0.04045, ((c + 0.055) / 1.055) ** 2.4, c / 12.92)
    M = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]], np.float32)
    xyz = c @ M.T / np.array([0.9505, 1.0, 1.089], np.float32)
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


def build_palette(samples, extra=40, seed=1):
    """The game's 35 inks plus `extra` colours the art needs (k-means in Lab over what the inks miss)."""
    base = game_inks()
    px = samples.reshape(-1, 3).astype(np.float32)
    rng = np.random.RandomState(seed)
    if len(px) > 60000:
        px = px[rng.choice(len(px), 60000, replace=False)]
    L = _lab(px)
    d = np.min(((L[:, None, :] - _lab(base)[None]) ** 2).sum(-1), axis=1)
    miss = px[d > 60]                                   # colours the game's ramps cannot express (skin, hair, sky)
    if len(miss) < extra:
        return base
    cent = miss[rng.choice(len(miss), extra, replace=False)].copy()
    Lm = _lab(miss)
    for _ in range(20):
        lab_c = _lab(cent)
        lbl = np.argmin(((Lm[:, None, :] - lab_c[None]) ** 2).sum(-1), axis=1)
        for k in range(extra):
            sel = miss[lbl == k]
            if len(sel):
                cent[k] = sel.mean(0)
    return np.vstack([base, np.round(cent)])


def snap(native_rgb, palette):
    """Every pixel to its nearest palette ink in Lab; alpha (if any) kept as fully on/off."""
    rgb = native_rgb[..., :3].astype(np.float32)
    L = _lab(rgb.reshape(-1, 3))
    P = _lab(palette)
    out = np.empty_like(rgb.reshape(-1, 3))
    for s in range(0, len(L), 50000):
        idx = np.argmin(((L[s:s + 50000, None, :] - P[None]) ** 2).sum(-1), axis=1)
        out[s:s + 50000] = palette[idx]
    res = out.reshape(rgb.shape).astype(np.uint8)
    if native_rgb.shape[2] == 4:
        a = (native_rgb[..., 3] > 127).astype(np.uint8) * 255
        res = np.dstack([res, a])
    return res


def despeckle(a, passes=1):
    """A lone pixel whose 4 neighbours all agree on one other colour takes that colour (model noise)."""
    a = a.copy()
    for _ in range(passes):
        c = a[1:-1, 1:-1]
        n = [a[:-2, 1:-1], a[2:, 1:-1], a[1:-1, :-2], a[1:-1, 2:]]
        same = np.all([np.all(n[0] == x, axis=-1) for x in n[1:]], axis=0)
        lone = same & ~np.all(c == n[0], axis=-1)
        c[lone] = n[0][lone]
    return a


def to_pixels(img, period=None, palette=None, extra=40):
    a = np.asarray(img.convert('RGBA' if img.mode == 'RGBA' else 'RGB'))
    if period:
        g = [(float(period), 0.0), (float(period), 0.0)]
        # phase only
        g = [(period, gp[1]) for gp in grid(a, period, period)]
    else:
        g = grid(a)
    nat = sample(a, g[0], g[1])
    if palette is None:
        palette = build_palette(nat[..., :3], extra)
    out = despeckle(snap(nat, palette))
    return Image.fromarray(out, 'RGBA' if out.shape[2] == 4 else 'RGB'), g, palette


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('src'); ap.add_argument('dst')
    ap.add_argument('--period', type=float)
    a = ap.parse_args()
    im, g, pal = to_pixels(Image.open(a.src), a.period)
    im.save(a.dst)
    print('grid x=%.2f+%.2f y=%.2f+%.2f -> native %dx%d, %d inks' % (g[0][0], g[0][1], g[1][0], g[1][1],
                                                                    im.width, im.height, len(pal)))
