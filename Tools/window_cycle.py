# -*- coding: utf-8 -*-
"""The view out of the bar's window, as a shift-long animation (2026-08-19).

The author generated six PixelLab animation sheets - a Miami skyline going from a golden
sun through the pink band into deep night - and asked for them to play in the window, in
step with the clock. This turns those six sheets into ONE frame sheet the stage can slice,
and prints the numbers the stage needs to place it.

Three decisions worth reading before changing anything here:

  * THE WHOLE PICTURE STANDS IN THE WINDOW'S PLANE. The first pass cut a 1:1 slice out of
    each frame to avoid resampling, and it showed a sliver of city through a window that is
    most of a wall wide. The author's call (2026-08-19): "kesim falan yapma, acisini ayarla
    ve sahneye tam yerlestir". So the frame is fitted to the opening's own TRAPEZOID - this
    window is seen at an angle, 182 rows tall at its near edge and 120 at its far one, and
    both edges run dead straight (measured: the mid-column lands within a pixel of the fit).
    Every destination pixel takes the NEAREST source pixel, never a blend, so the picture
    tilts without going soft - it is a Mode-7 style fit, not a filtered scale.
  * THE MASK IS THE ROOM'S OWN HOLE. Alpha comes from club_room.png, cropped to the hole's
    bounding box, exactly as scene_nb_post.post_window derives the still plate. So the
    animation cannot fall out of register with the window it plays in, and the painted
    mullions cut the view into panes for free.
  * THE SHEETS ARE ORDERED BY MEASUREMENT, not by filename. ORDER carries the distance
    matrix that pinned it, and why a greedy chain got it wrong.
  * THE HORIZON RIDES IN THE ALPHA. The stage lights the room by reading the glass, and
    the night frames fooled it: the carpet of lit city windows read as a sunrise (the
    author, 2026-08-19: "sehir isiklari mekani aydinlatamaz"). So every warped pixel
    that comes from below HORIZON_ROW ships at alpha 254 instead of 255 - invisible on
    the glass, unmistakable to the reader - and the stage samples only true 255s.

Commands:  measure          what the sheets contain, and their chain order
           build            write the sheet + print the stage constants
Sources:   Tools/window_raw/*.png   (copy the PixelLab downloads here)
Ships:     Assets/Resources/Scene/window_cycle.png
"""
import io, json, os, sys, time

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
RAW = os.path.join(HERE, 'window_raw')
ROOM = os.path.join(ROOT, 'Assets', 'Art', 'Backgrounds', 'club_room.png')
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Scene', 'window_cycle.png')
LOG = os.path.join(HERE, 'AssetPipeline', 'generation_log.jsonl')

# The PixelLab animation sheets: 17 frames of 193x150 on a 5x4 grid, the last three cells
# empty. Measured off the four sheets rather than assumed - `measure` re-checks it.
FW, FH = 193, 150
COLS, ROWS, FRAMES = 5, 4, 17

# How many columns of sheet the built frame sheet is laid out in.
SHEET_COLS = 8


def sheets():
    return sorted(f for f in os.listdir(RAW) if f.lower().endswith('.png'))


def frames_of(path):
    a = np.asarray(Image.open(path).convert('RGBA'))
    out = []
    for i in range(FRAMES):
        r, c = divmod(i, COLS)
        out.append(a[r * FH:(r + 1) * FH, c * FW:(c + 1) * FW].copy())
    return out


def dist(a, b):
    """Mean absolute RGB difference between two frames."""
    return float(np.abs(a[:, :, :3].astype(np.int16) - b[:, :, :3].astype(np.int16)).mean())


def brightness(a):
    return float(a[:, :, :3].mean())


def edges(mask):
    """The opening's top and bottom edges as two straight lines, y = a + b*x in the bounding
    box's own space, plus how far the art actually strays from them.

    Fitted rather than read column by column because the window is MULLIONED: a column that
    falls inside a painted glazing bar has no open pixel at all, and one that clips a bar's
    corner reports an edge that is a few rows off. A least-squares line over every column
    that does see sky is immune to both. The residual is returned so the fit can be checked
    instead of trusted - on this window it is under a pixel, which is what says the opening
    is a straight-sided trapezoid and not a curve."""
    h, w = mask.shape
    xs, tops, bots = [], [], []
    for x in range(w):
        rows = np.where(mask[:, x])[0]
        if len(rows):
            xs.append(x)
            tops.append(rows.min())
            bots.append(rows.max())
    xs = np.asarray(xs, float)
    top = np.polyfit(xs, np.asarray(tops, float), 1)
    bot = np.polyfit(xs, np.asarray(bots, float), 1)
    res = max(float(np.abs(np.polyval(top, xs) - tops).max()),
              float(np.abs(np.polyval(bot, xs) - bots).max()))
    return top, bot, res


# Where the sky ends and the city begins, as a SOURCE row (2026-08-19). Measured off the
# last twelve (night) frames: the lit-window carpet blazes from row ~65 down - 87 hot
# pixels in rows 65-69 against 41 in 60-64 - while the sun's core never sinks past row
# ~53. Pixels warped from at or below this row ship at CITY_ALPHA so the stage's sky
# read can refuse them; see the docstring.
HORIZON_ROW = 65
CITY_ALPHA = 254

# How many art pixels taller than the opening the picture is drawn, at every column. A
# couple of pixels of overscan so the view always reaches past the glazing bars and the
# window's own edge pixels - never a hairline of backdrop showing along a mullion - and it
# reads a touch closer through the glass (the author, 2026-08-19: "birkac pixel daha buyut").
OVERSCAN_PX = 6


def scales(top, bot, w, sh, overscan=OVERSCAN_PX):
    """Where each destination column samples the source from, at TRUE ASPECT.

    The first fit stretched each source column to its own vertical span and stepped across
    the source at a constant rate. That fills the opening but it is not a picture of
    anything: the horizontal scale was 109/193 = 0.56 while the vertical ran 183/150 = 1.22
    at the near edge, so everything in the glass stood 2.2x too tall - palms like masts.

    Aspect is held by making the horizontal scale EQUAL the vertical one column by column.
    The vertical scale at column x is span(x)/150, so the source must advance by 150/span(x)
    per column - it crawls where the window is tall and near, and hurries where it is short
    and far. That is exactly what perspective does to a flat picture behind an angled
    opening, so holding the aspect and drawing the perspective turn out to be the same job.

    Integrating that advance gives a source width of about 104 px for this window, not the
    full 193, so the glass shows a centred part of the panorama - unavoidable: the opening
    is far narrower in proportion than the picture, and the only other way to keep the
    aspect is to letterbox it inside its own window.

    Returns (u, spans): the source column for each destination column, and the source
    height each destination column covers.
    """
    xs = np.arange(w, dtype=float)
    spans = np.polyval(bot, xs) - np.polyval(top, xs) + 1.0 + overscan
    spans = np.maximum(1.0, spans)
    step = sh / spans                       # source px per destination column - isotropy
    u = np.cumsum(step) - step * 0.5        # sample at each column's centre
    return u, spans


def warp(frame, mask, top, bot, overscan=OVERSCAN_PX):
    """One frame, stood in the window's plane at true aspect and cut to its glass.

    Nearest sampling throughout: no pixel is ever blended, so the picture tilts and
    foreshortens without going soft.
    """
    h, w = mask.shape
    sh, sw = frame.shape[:2]
    u, spans = scales(top, bot, w, sh, overscan)
    u = u - u.mean() + sw * 0.5             # centre the visible part on the panorama
    out = np.zeros((h, w, 4), np.uint8)
    city = np.zeros((h, w), bool)
    for x in range(w):
        span = spans[x]
        mid = (np.polyval(top, x) + np.polyval(bot, x)) * 0.5
        y0 = mid - span * 0.5
        lo = max(0, int(np.floor(np.polyval(top, x))))
        hi = min(h - 1, int(np.ceil(np.polyval(bot, x))))
        if hi < lo:
            continue
        sx = int(round(u[x]))
        if sx < 0 or sx > sw - 1:            # off the panorama: leave it to the mask
            continue
        ys = np.arange(lo, hi + 1)
        sy = np.rint((ys - y0) / span * (sh - 1)).astype(int)
        np.clip(sy, 0, sh - 1, out=sy)
        out[lo:hi + 1, x, :3] = frame[sy, sx, :3]
        city[lo:hi + 1, x] = sy >= HORIZON_ROW
    a = np.where(mask, np.where(city, CITY_ALPHA, 255), 0).astype(np.uint8)
    out[:, :, 3] = a
    out[a == 0] = (0, 0, 0, 0)
    return out


# THE CHAIN, MEASURED AND THEN PINNED (2026-08-19). A greedy nearest-neighbour walk got
# this wrong, and the distance matrix says why: the six sheets are not a chain of six.
#
#   sheet_e  full golden sun -> sun on the horizon   brightness 95.3 -> 83.1  (REVERSED:
#            generated BRIGHTENING, so it plays backwards relative to the evening; turned
#            around, its last frame IS c's first, distance 0.0 - the author continued
#            c's start backwards into the golden hour and asked for the flip)
#   sheet_c  sun on the horizon -> sun gone                    83.1 -> 79.5
#   sheet_b  orange band -> pink band                          79.5 -> 73.3   (c's last
#            frame and b's first are IDENTICAL, distance 0.0 - the author continued it)
#   sheet_d  pink band -> deep purple dusk                     73.4 -> 68.8
#   sheet_a  pink band -> night, windows lit                   73.4 -> 52.0
#   sheet_f  night, windows lit -> deep night                  52.0 -> 47.9   (a's last
#            frame and f's first are IDENTICAL, distance 0.0 - continued again)
#
# a and d BOTH continue from b's last frame (1.7 each): they are two alternatives generated
# from the same moment, not successive ones. Played in sequence, a would throw the sky back
# to the pink band after d had already reached dusk. So a is SPLICED, not appended - it
# joins at the frame closest to d's last (a[10], distance 7.4, against 13.8 at a[0]), and
# what it contributes is the descent into night that d never had.
ORDER = ['sheet_e.png', 'sheet_c.png', 'sheet_b.png', 'sheet_d.png', 'sheet_a.png',
         'sheet_f.png']
REVERSED = {'sheet_e.png'}
SPLICE = {'sheet_a.png': 10}


def chain():
    """The pinned order, with each sheet's frames. Returns (order, frames-by-sheet)."""
    have = set(sheets())
    missing = [n for n in ORDER if n not in have]
    if missing:
        raise SystemExit('missing sheets in %s: %s' % (RAW, missing))
    heads = {}
    for n in ORDER:
        f = frames_of(os.path.join(RAW, n))
        if n in REVERSED:
            f = f[::-1]
        heads[n] = f[SPLICE.get(n, 0):]
    return list(ORDER), heads


def hole():
    """The window's bounding box in the room art, and the hole's own alpha inside it."""
    a = np.asarray(Image.open(ROOM).convert('RGBA'))
    h = a[:, :, 3] < 128
    ys, xs = np.where(h)
    if not len(xs):
        raise SystemExit('club_room.png has no keyed window hole')
    x0, x1, y0, y1 = int(xs.min()), int(xs.max()) + 1, int(ys.min()), int(ys.max()) + 1
    return (x0, y0, x1 - x0, y1 - y0), h[y0:y1, x0:x1]


def measure():
    (bx, by, bw, bh), mask = hole()
    print('window hole: x %d y %d, %dx%d  (%.0f%% of the box is open)'
          % (bx, by, bw, bh, 100.0 * mask.mean()))
    order, heads = chain()
    top, bot, res = edges(mask)
    print('the opening is a trapezoid: %d rows tall at x 0, %d at x %d '
          '(edges straight to within %.1f px)'
          % (round(np.polyval(bot, 0) - np.polyval(top, 0)) + 1,
             round(np.polyval(bot, bw - 1) - np.polyval(top, bw - 1)) + 1, bw - 1, res))
    print('\nsheets, in the pinned order (see ORDER for the measurement behind it):')
    prev = None
    for i, n in enumerate(order):
        f = heads[n]
        seam = ('  seam to previous: %.1f' % dist(prev, f[0])) if prev is not None else ''
        print('  %d. %-16s %2d frames  brightness %5.1f -> %5.1f%s'
              % (i + 1, n, len(f), brightness(f[0]), brightness(f[-1]), seam))
        prev = f[-1]
    print('\ntotal frames: %d' % sum(len(heads[n]) for n in order))
    u, spans = scales(top, bot, bw, FH)
    print('at true aspect the glass shows %d of the panorama\'s %d columns, '
          'and the source advances %.2f px per column at the near edge, %.2f at the far'
          % (round(u[-1] - u[0]) + 1, FW, FH / spans[0], FH / spans[-1]))
    print('overscan: %d art px taller than the opening at every column' % OVERSCAN_PX)


def build():
    (bx, by, bw, bh), mask = hole()
    order, heads = chain()
    frames = [f for n in order for f in heads[n]]

    # Drop a seam frame that repeats the one before it: chained animations often end and
    # begin on the same picture, and a doubled frame is a visible hitch in a slow pan.
    kept = [frames[0]]
    for f in frames[1:]:
        if dist(kept[-1], f) > 0.5:
            kept.append(f)
    dropped = len(frames) - len(kept)

    top, bot, res = edges(mask)
    if res > 3.0:
        print('WARNING: the opening strays %.1f px from a straight-sided trapezoid - the fit '
              'will not follow it exactly' % res)

    n = len(kept)
    rows = (n + SHEET_COLS - 1) // SHEET_COLS
    sheet = Image.new('RGBA', (SHEET_COLS * bw, rows * bh), (0, 0, 0, 0))
    for i, f in enumerate(kept):
        cell = warp(f, mask, top, bot)
        r, c = divmod(i, SHEET_COLS)
        sheet.paste(Image.fromarray(cell, 'RGBA'), (c * bw, r * bh))

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    sheet.save(OUT)
    print('wrote %s  %dx%d' % (os.path.relpath(OUT, ROOT), sheet.width, sheet.height))
    print('  %d frames kept (%d duplicate seam frame(s) dropped), %d columns, cell %dx%d'
          % (n, dropped, SHEET_COLS, bw, bh))
    u, spans = scales(top, bot, bw, FH)
    print('  true aspect held at every column (scale %.3f near, %.3f far), '
          '%d of %d panorama columns in the glass, %d px overscan'
          % (spans[0] / FH, spans[-1] / FH, round(u[-1] - u[0]) + 1, FW, OVERSCAN_PX))
    a = np.asarray(sheet)[:, :, 3]
    print('  horizon at source row %d: %.0f%% of the glass is sky (alpha 255), '
          'the rest city (alpha %d)'
          % (HORIZON_ROW, 100.0 * (a == 255).sum() / max(1, (a >= CITY_ALPHA).sum()),
             CITY_ALPHA))
    print('\n-- constants for DiegeticStage.cs --')
    print('  WindowCellPx   = %d x %d' % (bw, bh))
    print('  WindowCols     = %d' % SHEET_COLS)
    print('  WindowFrames   = %d' % n)
    # Bottom-left origin, the space StageArtPointToWorld speaks: the plate's centre.
    room_h = np.asarray(Image.open(ROOM)).shape[0]
    print('  WindowCentreArtPx (bottom-left origin) = (%.1f, %.1f)'
          % (bx + bw / 2.0, room_h - (by + bh / 2.0)))
    with io.open(LOG, 'a', encoding='utf-8') as fh:
        fh.write(json.dumps({
            'asset': 'window_cycle', 'event': 'built', 'frames': n, 'cell': [bw, bh],
            'cols': SHEET_COLS, 'sheets': order, 'dropped_seams': dropped,
            'ts': time.strftime('%Y-%m-%dT%H:%M:%S')}) + '\n')


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'measure'
    {'measure': measure, 'build': build}[cmd]()
