# -*- coding: utf-8 -*-
"""Take the black keyline off a character, deterministically (2026-08-20).

THE HOUSE DRAWS NO KEYLINES. The author has said it four times now, and the last time
without qualification: "hicbir karakterde siyah kontur olmamali, lineless_neutral sekilde
olmali". PixelLab cannot be made to obey it - `outline: lineless` is soft guidance, the
same request has come back at 3% and at 93% on consecutive rolls, and three re-rolls in a
row lost the toss. Fighting a lottery with more coins is not a method.

So the line is REMOVED rather than wished away, which is the same move the bottle pipeline
makes for open states: derive it, do not re-generate it and hope.

WHAT COUNTS AS A LINE, and why it is safe to erase:

  A keyline is THIN and it is DARKER THAN WHAT IT ENCLOSES. Both halves matter. Black
  trousers are dark but they are a mass - a near-black pixel in the middle of them has
  near-black neighbours on every side. A drawn line has light on both sides of it (or
  light on one side and nothing at all on the other, which is the silhouette rim).

  So a pixel is a line when it is near-black AND either
    * the pixels left and right of it are both markedly lighter, or
    * the pixels above and below it are both markedly lighter, or
    * it sits on the silhouette with a markedly lighter pixel just inside.
  Every one of those tests is local and symmetric, which is what keeps hair, eyes, a belt
  and a black trouser leg intact: none of them is a one-pixel rib between two light things.

  A line pixel is replaced by the lighter side it borders - the colour the drawing would
  have had if the line had never been put there. A silhouette rim is replaced by the pixel
  just inside it, so the figure keeps its shape and loses its ink.

This runs at SHIP time, never on the download: the generated file stays exactly as it came
back, because that is the provenance, and what the game loads is the derived plate.
"""
import io
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

INK = 78          # a pixel this dark can be a line
CONTRAST = 45     # ...if what it borders is this much lighter


def _lum(p):
    return max(p[0], p[1], p[2])


MIN_RUN = 7       # a line is long; an eye is not


def _components(px, w, h):
    """Connected runs of near-black, 4-connected. A keyline is one long component that
    follows a garment's edge; an eye, a nostril, a lip line are small ones."""
    seen = bytearray(w * h)
    comps = []
    for y0 in range(h):
        for x0 in range(w):
            i0 = y0 * w + x0
            if seen[i0]:
                continue
            p = px[x0, y0]
            if p[3] < 40 or _lum(p) >= INK:
                continue
            stack, comp = [(x0, y0)], []
            seen[i0] = 1
            while stack:
                x, y = stack.pop()
                comp.append((x, y))
                for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                    if 0 <= nx < w and 0 <= ny < h and not seen[ny * w + nx]:
                        q = px[nx, ny]
                        if q[3] >= 40 and _lum(q) < INK:
                            seen[ny * w + nx] = 1
                            stack.append((nx, ny))
            comps.append(comp)
    return comps


def delineate(im, passes=2):
    """Return the figure with its keyline removed. Idempotent: a lineless figure comes
    back unchanged, because nothing in it satisfies the tests.

    TWO tests, and the second one was bought with a mistake. The first version asked only
    "is this a thin dark rib between two lighter things", and that is true of an EYE - the
    first shipped pass bleached the cast's eyes and eyebrows to pale smudges. A keyline is
    also LONG: it runs the length of a sleeve or a lapel. So a dark pixel is only erased
    when it is thin AND belongs to a near-black component of at least MIN_RUN pixels, which
    an eye, a nostril and a lip line never are.
    """
    out = im.copy()
    w, h = out.size
    for _ in range(passes):
        src = out.load()
        big = set()
        for comp in _components(src, w, h):
            if len(comp) >= MIN_RUN:
                big.update(comp)
        if not big:
            break
        dst = out.copy()
        dp = dst.load()
        changed = 0
        for (x, y) in big:
            p = src[x, y]
            here = _lum(p)

            def side(dx, dy):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h:
                    q = src[nx, ny]
                    return q if q[3] >= 40 else None
                return None

            for dx, dy in ((1, 0), (0, 1)):
                a, b = side(-dx, -dy), side(dx, dy)
                if a is not None and b is not None:
                    if _lum(a) - here >= CONTRAST and _lum(b) - here >= CONTRAST:
                        dp[x, y] = a if _lum(a) >= _lum(b) else b
                        changed += 1
                        break
                elif a is None and b is not None and _lum(b) - here >= CONTRAST:
                    dp[x, y] = b
                    changed += 1
                    break
                elif b is None and a is not None and _lum(a) - here >= CONTRAST:
                    dp[x, y] = a
                    changed += 1
                    break
        out = dst
        if changed == 0:
            break
    return out


if __name__ == '__main__':
    import patron_trial_gen as trial
    for name in sys.argv[1:]:
        p = os.path.join(trial.RAW, name + '.png')
        im = Image.open(p).convert('RGBA')
        before = trial.edge_darkness(im)
        after_im = delineate(im)
        print('%-14s outline %.0f%% -> %.0f%%' % (name, before, trial.edge_darkness(after_im)))
        after_im.save(os.path.join(trial.RAW, name + '_lineless.png'))
