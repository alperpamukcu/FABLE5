# -*- coding: utf-8 -*-
"""Re-cut licence_shell3 to a flawless, symmetric edge.

The author, 2026-08-10: "kusursuz bir kesim ve simetri olmali sinirlarda."

The generated card carried the paper beautifully and the cut badly. Measured off the
shipped sprite:

  * the border band wandered between 0 and 4 px wide around the card — 4px at the
    left at y60, 3px at the right at y120, and NOTHING at all at the left at y120,
    where the paper ran straight off the cut.
  * the four corners were four different curves. The top ones ran 6,5,4,3,1,2,0 —
    non-monotonic, which is the notch you can see at the top left — and the bottom
    ones ran 6,4,3,2,1,1,0.

The bottom corners turned out to be an exact radius-9 pixel circle, so the artist's
own curve is the specification; the top corners are the ones that drifted. This
prints that circle on all four, and a band of one constant thickness all the way
round, WITHOUT touching the paper: the stock, the wear, the guilloche tint, the navy
header, the portrait well and the five rules all come through from the source.

Symmetric BY CONSTRUCTION: the mask is a function of the FOLDED coordinates
(distance from the nearer edge on each axis), so a mirror check cannot fail — it is
not a test the output passes, it is a shape it cannot violate.

Idempotent (memory sprite-pipeline-idempotence): the pristine art is snapshotted to
Tools/licence_raw/ on the first run and every later run re-cuts from THAT, so running
this twice cannot stack a second band on top of the first.

    python Tools/licence_recut.py            # re-cut, write, verify
    python Tools/licence_recut.py --dry      # verify only, write nothing
"""
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
DEST = os.path.join(ROOT, 'Assets', 'Resources', 'Items', 'licence_shell3.png')
RAW_DIR = os.path.join(HERE, 'licence_raw')
SRC = os.path.join(RAW_DIR, 'licence_shell3_src.png')
PREVIEW = os.path.join(ROOT, 'Art', 'pilot', 'licence_cut.png')

# The card on the 256x160 sheet, measured off the cream (the sprite is drawn on an
# opaque ground, so the canvas is NOT the card — TycoonHud's zones are measured off
# these same bounds and must keep them).
L, T, R, B = 14, 11, 241, 148
RADIUS = 9                     # the artist's own bottom-corner curve, exactly
BAND = 2                       # border rings, in art pixels

# Taken from the top edge, the one side whose band was already right.
RIM = (248, 245, 233)          # ring 0: the lit outer edge of the stock
EDGE = (220, 211, 164)         # ring 1: the card's printed border


def inside(x, y, inset):
    """Is the pixel in the rounded rect, shrunk by `inset` on every side?

    Folded coordinates only — fx/fy are the distance to the NEARER edge on each
    axis — so the four corners and the two axes are the same shape by construction.
    """
    fx = min(x - L, R - x) - inset
    fy = min(y - T, B - y) - inset
    if fx < 0 or fy < 0:
        return False
    r = RADIUS - inset
    if r <= 0 or fx >= r or fy >= r:
        return True
    dx, dy = r - (fx + 0.5), r - (fy + 0.5)
    return dx * dx + dy * dy <= r * r


def snapshot():
    """Freeze the pristine art once; every re-cut reads from the freeze."""
    os.makedirs(RAW_DIR, exist_ok=True)
    if not os.path.exists(SRC):
        Image.open(DEST).convert('RGBA').save(SRC)
        print('snapshotted pristine art -> Tools/licence_raw/licence_shell3_src.png')
    return Image.open(SRC).convert('RGBA')


def inward(px, W, H, x, y):
    """The nearest opaque source pixel, walking towards the card's centre.

    The re-cut circle is a hair fuller than the drawn top corners, so a handful of
    pixels per top corner land inside the mask with nothing drawn under them. They
    are filled from the art directly inboard of them — which under the header is the
    navy, not the paper — rather than with a flat tone that would show as a chip.
    """
    cx, cy = (L + R) / 2.0, (T + B) / 2.0
    vx, vy = cx - x, cy - y
    n = max(abs(vx), abs(vy)) or 1.0
    vx, vy = vx / n, vy / n
    for step in range(1, 12):
        sx, sy = int(round(x + vx * step)), int(round(y + vy * step))
        if 0 <= sx < W and 0 <= sy < H and px[sx, sy][3] > 200:
            return px[sx, sy]
    return None


def recut(src):
    W, H = src.size
    sp = src.load()
    out = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    op = out.load()
    filled = 0
    for y in range(H):
        for x in range(W):
            if not inside(x, y, 0):
                continue                      # outside the cut: transparent
            if not inside(x, y, 1):
                op[x, y] = RIM + (255,)
            elif not inside(x, y, BAND):
                op[x, y] = EDGE + (255,)
            elif sp[x, y][3] > 200:
                op[x, y] = sp[x, y][:3] + (255,)
            else:
                c = inward(sp, W, H, x, y)
                op[x, y] = (c[:3] if c else EDGE) + (255,)
                filled += 1
    return out, filled


def verify(im):
    """The proof: symmetry, band thickness, and a solid card with no holes."""
    W, H = im.size
    px = im.load()
    ok = True

    rows = {}
    for y in range(H):
        xs = [x for x in range(W) if px[x, y][3] >= 128]
        if xs:
            rows[y] = (min(xs), max(xs))
    cols = {}
    for x in range(W):
        ys = [y for y in range(H) if px[x, y][3] >= 128]
        if ys:
            cols[x] = (min(ys), max(ys))

    bad_lr = [y for y, (l, r) in rows.items() if l != W - 1 - r]
    bad_tb = [x for x, (t, b) in cols.items() if t != H - 1 - b]
    print('  left/right mirror : %s' % ('OK' if not bad_lr else 'FAIL rows %s' % bad_lr[:8]))
    print('  top/bottom mirror : %s' % ('OK' if not bad_tb else 'FAIL cols %s' % bad_tb[:8]))
    ok &= not bad_lr and not bad_tb

    span = (min(rows), max(rows), min(cols), max(cols))
    print('  card bounds       : y %d..%d  x %d..%d  %s'
          % (span[0], span[1], span[2], span[3],
             'OK' if span == (T, B, L, R) else 'MOVED (zones would shift!)'))
    ok &= span == (T, B, L, R)

    widths = []
    for y in range(H):
        if y not in rows:
            continue
        l, r = rows[y]
        n = 0
        while l + n <= r and px[l + n, y][:3] in (RIM, EDGE):
            n += 1
        m = 0
        while r - m >= l and px[r - m, y][:3] in (RIM, EDGE):
            m += 1
        widths.append((n, m))
    straight = [w for w in widths[RADIUS:-RADIUS or None]]
    thin = [w for w in straight if w[0] != BAND or w[1] != BAND]
    print('  band thickness    : %s (straight run, want %d/%d both sides)'
          % ('OK' if not thin else 'FAIL %d rows off: %s' % (len(thin), thin[:6]), BAND, BAND))
    ok &= not thin

    holes = sum(1 for y in range(H) for x in range(W)
                if inside(x, y, 0) and px[x, y][3] < 128)
    print('  holes in the card : %s' % ('OK' if holes == 0 else 'FAIL %d px' % holes))
    ok &= holes == 0
    return ok


def contact(before, after):
    """Before and after at 3x, the scale the card is actually drawn at."""
    pad, s = 10, 3
    W = before.width * s * 2 + pad * 3
    H = before.height * s + pad * 2
    sheet = Image.new('RGBA', (W, H), (26, 16, 35, 255))
    for i, im in enumerate((before, after)):
        big = im.resize((im.width * s, im.height * s), Image.NEAREST)
        sheet.alpha_composite(big, (pad + i * (before.width * s + pad), pad))
    os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
    sheet.save(PREVIEW)
    print('contact sheet -> Art/pilot/licence_cut.png (before | after, 3x)')


if __name__ == '__main__':
    dry = '--dry' in sys.argv
    src = snapshot()
    out, filled = recut(src)
    print('re-cut r%d, band %dpx, %d pixel(s) filled inboard' % (RADIUS, BAND, filled))
    good = verify(out)
    contact(src, out)
    if dry:
        print('dry run: nothing written')
    elif good:
        out.save(DEST)
        print('written -> Assets/Resources/Items/licence_shell3.png')
    else:
        print('VERIFY FAILED: nothing written')
        sys.exit(1)
