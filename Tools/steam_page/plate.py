# -*- coding: utf-8 -*-
"""THE MASTER PLATE: the steam_kit page background's sunset, re-drawable at any native size.

The store page background (Tools/steam_kit, the author's; out/refs/page_background.webp here) is the scene every
store and library picture shares: flat sky bands joined by two-row scan-line seams, a bright horizon line, a
flat half sun with a few reflection dashes, sea bands sinking to near-black, a two-layer skyline with one-pixel
windows, and near-black palm silhouettes. Its native picture (237x133 on an 8 px grid) is measured here once:

  * the band profile (colours and heights, the seams kept one row each),
  * the palms (pixels darker than their row's band, so the sea and the skyline drop out),

and render() lays the same scene out on any native canvas: bands stretched to the canvas height (seams stay one
row), sun and skyline wherever the layout asks, palms at a whole-number scale. Nothing here is resampled except
the palm silhouettes, which are redrawn at their new size (smooth upsample of the mask, then a hard threshold),
so every texel of a plate sits on the canvas's own grid.
"""
import os
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
REF = os.path.join(HERE, 'out', 'refs', 'page_bg_native.png')


def hx(s):
    s = s.lstrip('#')
    return (int(s[0:2], 16), int(s[2:4], 16), int(s[4:6], 16))


# measured from the native picture, column x=95 (rows 0..132); (colour, rows, kind)  kind: band | seam
SKY = [('#643F89', 11, 'band'), ('#A3489E', 1, 'seam'), ('#713F8E', 1, 'seam'),
       ('#A44B9D', 10, 'band'), ('#E74BA2', 1, 'seam'), ('#AE489A', 1, 'seam'),
       ('#EA4AA0', 14, 'band'), ('#FE8958', 1, 'seam'), ('#F35389', 1, 'seam'),
       ('#FF8C56', 12, 'band'), ('#FFB356', 1, 'seam'), ('#FE8D4F', 1, 'seam'),
       ('#FFB457', 17, 'band'), ('#FFCD61', 1, 'seam'), ('#FEEF7E', 1, 'seam')]
SEA = [('#C4639F', 1, 'seam'), ('#9A4E9C', 4, 'band'), ('#743F8C', 5, 'band'), ('#522D72', 9, 'band'),
       ('#371953', 4, 'band'), ('#240F3D', 6, 'band'), ('#180C25', 8, 'band'), ('#12081A', 22, 'band')]
SUN = '#FEEC7B'
SUN_RIM = '#FEB555'
REFLECT = ['#C858A1', '#B4489B', '#9B4E9E']
PALM = '#190C25'
PALM_DEEP = '#110A18'
# skyline inks (sampled from the native picture's buildings)
BLD_BACK = '#230E3E'
BLD = ['#2E1549', '#351A55', '#3E1E57']
BLD_LIT = '#442365'
WIN = [('#7DF0E3', 5), ('#FEB555', 3), ('#EB4BA1', 2)]


def profile(rows, inks):
    """Stretch a band list to `rows`; every seam stays one row, the bands share the rest in proportion."""
    seams = sum(1 for _, _, k in inks if k == 'seam')
    flex = sum(n for _, n, k in inks if k == 'band')
    budget = max(0, rows - seams)
    out, acc, bands_done = [], 0.0, 0
    for c, n, k in inks:
        if k == 'seam':
            out.append(c)
            continue
        acc += n * budget / flex
        take = max(1, int(round(acc)) - bands_done)
        out += [c] * take
        bands_done += take
    out = (out + [inks[-1][0]] * rows)[:rows]
    return [hx(c) for c in out]


def _palms_from_ref():
    n = np.asarray(Image.open(REF).convert('RGB')).astype(int)
    H, W = n.shape[:2]
    col = n[:, 95]
    diff = np.abs(n - col[:, None, :]).sum(-1)
    lum = n @ np.array([.299, .587, .114])
    m = (diff > 40) & (lum < 42)
    # the skyline's buildings are not palms: drop the block right of the sun below row 38 that is not near-black
    bld = np.zeros_like(m); bld[38:75, 135:] = True
    m &= ~(bld & (lum > 20))
    m[103:] = False                                  # the bottom sea is as dark as the palms
    col_img = n.copy()
    # carry each trunk down to the bottom edge, in its own ink
    last = m[102].copy()
    for y in range(103, H):
        m[y] = last
        col_img[y] = col_img[102]
    left = (m[:, :75], col_img[:, :75])
    right = (m[:, 175:], col_img[:, 175:])
    return left, right


def palm_sprite(side, scale=1.0):
    """The ref's left or right palm cluster as a silhouette, redrawn at `scale` (smooth mask -> threshold)."""
    left, right = _palms_from_ref()
    m, col = left if side == 'left' else right
    ys, xs = np.nonzero(m)
    m = m[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    col = col[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    h, w = m.shape
    if scale != 1.0:
        size = (max(1, round(w * scale)), max(1, round(h * scale)))
        m = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).resize(size, Image.BICUBIC)) > 127
        col = np.asarray(Image.fromarray(col.astype(np.uint8)).resize(size, Image.NEAREST)).astype(int)
    lum = col @ np.array([.299, .587, .114])
    a = np.zeros(m.shape + (4,), np.uint8)
    a[m] = hx(PALM) + (255,)
    a[m & (lum < 22)] = hx(PALM_DEEP) + (255,)        # the near palms
    a[m & (lum >= 30)] = hx('#26103E') + (255,)       # the back palm, a step lighter
    return Image.fromarray(a, 'RGBA')


def skyline(width, height, seed=5, density=1.0, unit=1.0):
    """Two rows of blocks in the ref's manner: a dark back row, a front row of three body inks with a lit left
    edge, one-pixel windows on a 2x3 grid, about a third lit (cyan, amber, pink)."""
    rng = np.random.RandomState(seed)
    a = np.zeros((height, width, 4), np.uint8)
    base = height
    x = 0
    while x < width:                                  # back row
        w = min(int(rng.randint(3, 7) * unit), width - x); h = rng.randint(height // 5, height // 2)
        a[base - h:base, x:x + w] = hx(BLD_BACK) + (255,)
        x += w
    # the land the city stands on, rising from a low spit at the left end
    for xx in range(width):
        g = min(2, int(xx * 3 / max(1, width * 0.12)))
        if g:
            a[base - g:base, xx] = hx(BLD_BACK) + (255,)
    x = max(2, int(width * 0.06))
    while x < width:                                  # front row, lower toward the spit
        ramp = min(1.0, 0.35 + x / max(1.0, width * 0.4))
        w = min(max(4, int(rng.randint(5, 11) * unit)), width - x)
        h = max(3, int(rng.randint(height // 3, height) * ramp))
        if rng.rand() < 0.25 / density:
            x += rng.randint(2, 6); continue
        body = hx(BLD[rng.randint(len(BLD))])
        a[base - h:base, x:x + w] = body + (255,)
        a[base - h:base, x] = hx(BLD_LIT) + (255,)
        if rng.rand() < 0.3 and w > 5:                # a stepped roof
            a[base - h - 2:base - h, x + 2:x + w - 2] = body + (255,)
        for wy in range(base - h + 2, base - 2, 3):
            for wx in range(x + 2, x + w - 1, 2):
                if rng.rand() < 0.33:
                    r = rng.rand() * 10
                    c = WIN[0][0] if r < 5 else WIN[1][0] if r < 8 else WIN[2][0]
                    a[wy, wx] = hx(c) + (255,)
        x += w + (rng.randint(0, 2))
    return Image.fromarray(a, 'RGBA')


def _mix(a, b):
    a, b = hx(a), hx(b)
    return '#%02X%02X%02X' % tuple((x + y) // 2 for x, y in zip(a, b))


def with_night(night=('#1A1023', '#241830', '#362447', '#4A3160'), weights=(5, 4, 4, 4), share=0.42):
    """The ref's sky with dusk above it: the game's Night ramp as flat bands, joined to each other and to the ref's
    first band by the ref's own two-row seam (the next ink, then the half-way ink). `share` of the sky's band rows
    go to the night, so a neon title over the top of the picture sits on near-black, not on pink."""
    first = SKY[0][0]
    ref_rows = sum(n for _, n, k in SKY if k == 'band')
    tot = ref_rows * share / (1 - share)
    out = []
    inks = list(night) + [first]
    for i, c in enumerate(night):
        out.append((c, max(1, round(tot * weights[i] / sum(weights))), 'band'))
        nxt = inks[i + 1]
        out += [(nxt, 1, 'seam'), (_mix(c, nxt), 1, 'seam')]
    return out + SKY


def render(W, H, horizon=0.556, sun_x=0.5, sun_r=None, skyline_spans=((0.58, 1.0),), skyline_h=None,
           palms=(('left', 0.0, 1.0), ('right', 1.0, 1.0)), seed=5, reflect=True, night=0.0, sky=None,
           sun_drop=0):
    """The plate on a W x H native canvas. palms: (side, x anchor 0..1, scale vs the ref). night > 0 puts that
    share of the sky's bands into dusk bands above the ref's sunset (for a title to sit on)."""
    hz = int(round(H * horizon))
    sky = profile(hz, sky if sky is not None else (with_night(share=night) if night else SKY))
    sea = profile(H - hz, SEA)
    a = np.zeros((H, W, 4), np.uint8)
    a[..., 3] = 255
    for y, c in enumerate(sky + sea):
        a[y, :, :3] = c
    img = Image.fromarray(a, 'RGBA')
    # the sun: a flat disc on the horizon, its top half showing, one amber rim row at the waterline
    r = sun_r or max(6, int(round(H * 0.105)))
    cx = int(round(W * sun_x))
    yy, xx = np.mgrid[0:r + 1, -r:r + 1]
    disc = (xx ** 2 + (yy - r) ** 2) <= r * r + r * 0.6
    s = np.zeros(disc.shape + (4,), np.uint8)
    s[disc] = hx(SUN) + (255,)
    s[-1][disc[-1]] = hx(SUN_RIM) + (255,)
    if sun_drop < r:                                   # a setting sun: lowered, its foot cut by the waterline
        img.alpha_composite(Image.fromarray(s[:r + 1 - sun_drop], 'RGBA'), (cx - r, hz - r - 1 + sun_drop))
    # the skyline sits on the horizon
    sh = skyline_h or max(10, int(round(H * 0.26)))
    for i, (x0, x1) in enumerate(skyline_spans):
        sw = int(round(W * (x1 - x0)))
        if sw > 4:
            img.alpha_composite(skyline(sw, sh, seed + i, unit=max(1.0, H / 133 * 0.8)), (int(round(W * x0)), hz - sh))
    # the waterline row again over the skyline's feet
    b = np.asarray(img).copy()
    b[hz - 1, :, :3] = hx(SKY[-1][0])
    # reflections under the sun
    if reflect:
        rng = np.random.RandomState(seed + 9)
        u = max(1.0, H / 133)
        for k, yo in enumerate((2, 3, 5, 7)):
            y = hz + int(round(yo * u))
            if y >= H:
                break
            half = int(r * (1.0 - 0.18 * k))
            x0 = cx - half + rng.randint(-1, 2)
            b[y, max(0, x0):min(W, x0 + 2 * half), :3] = hx(REFLECT[min(k, len(REFLECT) - 1)])
    img = Image.fromarray(b, 'RGBA')
    for spec in palms:
        side, ax, sc = spec[:3]
        p = palm_sprite(side, sc)
        x = int(round(ax * W - (0 if side == 'left' else p.width)))
        y = H - p.height if len(spec) < 4 else int(round(spec[3] * H))   # 4th: top of the crown, fraction of H
        img.alpha_composite(p, (x, y))
    return img


if __name__ == '__main__':
    import sys
    W, H = int(sys.argv[1]), int(sys.argv[2])
    render(W, H).save(sys.argv[3] if len(sys.argv) > 3 else '/tmp/plate.png')
