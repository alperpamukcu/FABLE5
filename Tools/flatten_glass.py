# -*- coding: utf-8 -*-
"""
Take the transparency chequerboard out of a generated bottle's glass.

PixelLab draws SEE-THROUGH as a chequerboard, and on the soda water it drew one all
the way down the body - the author's "grey squares". Measured across the shelf, every
other vessel scores 0.02-0.08 on a two-tone alternation test, which is ordinary
dithering; the soda scores 0.12.

It is flattened rather than redrawn: inside the silhouette, away from the ink, a pixel
that differs only mildly from the tone its neighbourhood is mostly made of is replaced
by that tone. A chequer is exactly that - a minority tone a shade off its neighbours -
while a label edge or a cap differs hard and survives.

    python Tools/flatten_glass.py write
"""
from PIL import Image
from collections import Counter
import os, sys

DEST = 'Assets/Resources/Items'
# Only the soda: measured across the shelf every other vessel scores 0.02-0.08 on
# the alternation test below, which is ordinary dithering, and the soda scores 0.12.
TARGETS = ['soda']
NEAR = 2          # keep this far away from the silhouette's edge, so the ink is safe
SPAN = 3          # neighbourhood radius
SOFT = 62         # a difference bigger than this is an edge, not a chequer
WASHED = 22       # chroma below this is the chequer's grey, not the glass
MIN_NEIGHBOURS = 3   # ...and it needs this many chromatic neighbours to borrow from


def flatten(im):
    """
    The chequer is drawn in GREY over glass that is otherwise blue, so the two come
    apart on chroma rather than on brightness: the pattern's two tones carry 12 and 17
    of it against 29-45 for the glass around them. Every washed-out pixel is handed the
    colour its chromatic neighbours are mostly made of, which keeps the shading and
    takes the squares away. A neighbourhood with nothing chromatic in it is left alone,
    so a bottle whose glass really is grey is not repainted blue.
    """
    W, H = im.size
    p = im.load()
    solid = [[p[x, y][3] >= 128 for y in range(H)] for x in range(W)]

    def chroma(c):
        return max(c) - min(c)

    def inside(x, y):
        return all(0 <= x + dx < W and 0 <= y + dy < H and solid[x + dx][y + dy]
                   for dx in range(-NEAR, NEAR + 1) for dy in range(-NEAR, NEAR + 1))

    out = im.copy()
    op = out.load()
    for x in range(W):
        for y in range(H):
            if not solid[x][y] or not inside(x, y):
                continue
            c = p[x, y][:3]
            if chroma(c) >= WASHED:
                continue
            tally = Counter()
            for dx in range(-SPAN, SPAN + 1):
                for dy in range(-SPAN, SPAN + 1):
                    nx, ny = x + dx, y + dy
                    if not (0 <= nx < W and 0 <= ny < H) or not solid[nx][ny]:
                        continue
                    n = p[nx, ny][:3]
                    if chroma(n) >= WASHED and max(abs(n[i] - c[i]) for i in range(3)) <= SOFT:
                        tally[n] += 1
            if not tally:
                continue
            main, n = tally.most_common(1)[0]
            if n >= MIN_NEIGHBOURS:
                op[x, y] = main + (255,)
    return out


def checker(im):
    """How much of the sprite alternates tone on a short, regular step."""
    W, H = im.size
    p = im.load()
    n = hit = 0
    for y in range(2, H - 2):
        for x in range(2, W - 2):
            if p[x, y][3] < 128:
                continue
            n += 1
            c = p[x, y][:3]
            for per in (2, 3, 4):
                if x + 2 * per >= W:
                    continue
                same = p[x + 2 * per, y][:3]
                opp = p[x + per, y][:3]
                if (max(abs(c[i] - opp[i]) for i in range(3)) >= 16
                        and max(abs(c[i] - same[i]) for i in range(3)) <= 6):
                    hit += 1
                    break
    return hit / max(1, n)


def run(write):
    for style in TARGETS:
        for suffix in ('', '_open'):
            path = os.path.join(DEST, style + suffix + '.png')
            if not os.path.exists(path):
                continue
            im = Image.open(path).convert('RGBA')
            before = checker(im)
            done = flatten(im)
            after = checker(done)
            if write:
                done.save(path)
            print('%-12s chequer %.3f -> %.3f' % (style + suffix, before, after))
    print('%s' % ('written' if write else '(dry run)'))


if __name__ == '__main__':
    run(len(sys.argv) > 1 and sys.argv[1] == 'write')
