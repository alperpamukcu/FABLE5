# -*- coding: utf-8 -*-
"""
One outline thickness for the whole shelf (the author, 2026-08-03).

The bottles arrived from three places and each brought its own edge: the approved set
is drawn with a single pixel of the artist's own ink, the generated vessels came with
their own dark edge and then took another pixel from the quantize chain on top, and a
keg is simply black at its rim. Side by side on the wall they do not match.

Measuring what is there and topping it up cannot work: a run of dark pixels at the
edge is an outline on a pale bottle and is the ARTWORK on a black keg, and nothing in
the pixels tells the two apart. So the edge is rebuilt instead of measured. Up to
OUTLINE layers of boundary ink are peeled off - which takes any existing ring away and
leaves genuinely dark art alone past that depth - and then exactly OUTLINE pixels of
ink are laid back on. Whatever a vessel started with, it ends with the same edge as
every other vessel.

A style and its capless twin are cropped to a SHARED box, so the bottle does not shift
in the hand when it is opened.

    python Tools/uniform_outline.py write
"""
from PIL import Image
import json, io, os, sys

DEST = 'Assets/Resources/Items'
DATA = 'Assets/Data/bottles/base_bar.json'
INK = (12, 10, 16, 255)
OUTLINE = 2                 # pixels of ink around every vessel on the shelf
DARK = 70                   # luminance at or below this is ink rather than art
SOLID_AROUND = 16           # of a 5x5 neighbourhood, how much must be vessel before ink
                            # counts as trim on a broad edge rather than a thin crown

# Every vessel is fitted to the same height on the shelf - 110 points - so a sprite's
# ink reads at 110/height of what it measures. Two pixels on a 162-tall bottle came out
# at 1.38 points and the same two pixels on a 119-tall carton at 1.85: uniform in the
# file, a third heavier on the wall. Equal ink needs equal pixels per point, so every
# vessel is padded to ONE canvas height and stands on its floor. The art is not
# stretched to fill it - a short carton simply reads shorter than a tall bottle, which
# is what it is.
CANVAS_H = 162
KEGS = {'lager', 'pale_ale', 'stout'}   # drawn at keg scale on the floor, not the wall


def lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def styles():
    d = json.load(io.open(DATA, encoding='utf-8'))
    return sorted({c.get('style') for c in d['cards'] if c.get('style')})


def peel(im, layers=OUTLINE):
    """
    Take the existing ink ring off a vessel's broad edges, one layer at a time, and no
    deeper.

    Only the BROAD edges. A screw cap's crown is a curve two or three pixels deep, drawn
    entirely in ink, and peeling it away leaves the cap's flat interior behind: the
    author saw every cap on the wall come back with its top sliced off. A pixel is
    peelable only where the vessel is solid all around it - the wall of a bottle, where
    ink is trim - and not on a thin crown, where the ink IS the shape.
    """
    out = im.copy()
    p = out.load()
    W, H = out.size
    for _ in range(layers):
        solid = [[p[x, y][3] >= 128 for y in range(H)] for x in range(W)]
        doomed = []
        for x in range(W):
            for y in range(H):
                if not solid[x][y] or lum(p[x, y][:3]) > DARK:
                    continue
                if not any(not (0 <= x + dx < W and 0 <= y + dy < H) or not solid[x + dx][y + dy]
                           for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                    continue
                near = sum(1 for dx in range(-2, 3) for dy in range(-2, 3)
                           if 0 <= x + dx < W and 0 <= y + dy < H and solid[x + dx][y + dy])
                if near >= SOLID_AROUND:
                    doomed.append((x, y))
        if not doomed:
            break
        for x, y in doomed:
            p[x, y] = (0, 0, 0, 0)
    return out


def margins(im):
    """How much empty canvas each side already has."""
    bb = im.getbbox()
    if not bb:
        return (0, 0, 0, 0)
    W, H = im.size
    return (bb[0], bb[1], W - bb[2], H - bb[3])


def pad_to(im, need):
    """Grow the canvas only where the ink would otherwise run off it. The kegs are
    drawn with deliberate padding so the three of them keep one scale on the floor;
    trimming them to their contents would quietly resize the beer."""
    l, t, r, b = need
    if not any(need):
        return im
    out = Image.new('RGBA', (im.size[0] + l + r, im.size[1] + t + b), (0, 0, 0, 0))
    out.alpha_composite(im, (l, t))
    return out


def ring(im, thickness=OUTLINE):
    """Lay exactly this many pixels of ink around whatever is left, in place."""
    out = im.copy()
    op = out.load()
    WO, HO = out.size
    for _ in range(thickness):
        solid = [[op[x, y][3] > 40 for y in range(HO)] for x in range(WO)]
        grew = []
        for x in range(WO):
            for y in range(HO):
                if solid[x][y]:
                    continue
                if any(0 <= x + dx < WO and 0 <= y + dy < HO and solid[x + dx][y + dy]
                       for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                    grew.append((x, y))
        for x, y in grew:
            op[x, y] = INK
    return out


def round_crown(im):
    """
    Give a flat-topped closure an elliptical crown.

    The shelf is drawn from slightly above - it is why the glasses have elliptical
    bottoms and why an opened bottle gets an oval mouth - but seven of the closures
    came with their tops drawn as a straight line, and under two pixels of ink a
    straight line reads as a cap with its top cut off. Only the flat ones are touched:
    a closure that already curves is left exactly as its artist drew it.
    """
    W, H = im.size
    p = im.load()
    rows = [[x for x in range(W) if p[x, y][3] >= 128] for y in range(H)]
    live = [y for y in range(H) if rows[y]]
    if len(live) < 8:
        return im
    top = live[0]
    widths = [len(rows[y]) for y in range(top, min(top + 4, H))]
    if len(widths) < 4 or widths[-1] - widths[0] > 2:
        return im                       # already curved: leave the artist alone

    out = im.copy()
    op = out.load()
    for i, inset in enumerate((0.11, 0.05)):
        y = top + i
        if y >= H or not rows[y]:
            continue
        cut = max(1, int(round(len(rows[y]) * inset)))
        for x in rows[y][:cut] + rows[y][-cut:]:
            op[x, y] = (0, 0, 0, 0)
    return out


def stand_on_floor(im, height=CANVAS_H):
    """Put the vessel on a canvas of the shared height, standing on its floor. The
    padding goes above it, so the base still lands on the shelf when the sprite is
    fitted to the slot; what changes is how many pixels the slot has to squeeze."""
    W, H = im.size
    if H >= height:
        return im
    out = Image.new('RGBA', (W, height), (0, 0, 0, 0))
    out.alpha_composite(im, (0, height - H))
    return out


def run(write):
    changed = 0
    for style in styles():
        paths = [os.path.join(DEST, style + '.png'),
                 os.path.join(DEST, style + '_open.png')]
        loaded = [Image.open(p).convert('RGBA') if os.path.exists(p) else None for p in paths]
        if loaded[0] is None:
            continue
        peeled = [round_crown(peel(im)) if im is not None else None for im in loaded]

        # A shut bottle and its capless twin are padded by the SAME amount, so the
        # bottle does not jump when it is opened.
        need = [0, 0, 0, 0]
        for im in peeled:
            if im is None:
                continue
            for i, have in enumerate(margins(im)):
                need[i] = max(need[i], max(0, OUTLINE - have))
        done = [ring(pad_to(im, need)) if im is not None else None for im in peeled]
        if style not in KEGS:
            done = [stand_on_floor(im) if im is not None else None for im in done]

        for path, im in zip(paths, done):
            if im is None:
                continue
            if write:
                im.save(path)
            changed += 1
        print('%-16s %-10s%s' % (style, '%dx%d' % done[0].size,
                                 '  open %dx%d' % done[1].size if done[1] else ''))
    print('%d sprites %s at %dpx of ink' % (changed, 'written' if write else '(dry run)', OUTLINE))


if __name__ == '__main__':
    run(len(sys.argv) > 1 and sys.argv[1] == 'write')
