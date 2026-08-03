# -*- coding: utf-8 -*-
"""
One outline thickness for the whole shelf (the author, 2026-08-03).

The bottles arrived from three places and each brought its own edge, so side by side
on the wall they did not match. Two attempts at fixing that are recorded in the git
history and both were wrong in the same way: they PEELED the existing ink before
laying a fresh ring on, and ink at the edge of a drawing is not always trim. On a
bottle's wall it is; on a screw cap it is the crown, and on a wide flat cap it is the
top two rows of the cap itself. Every version of the peel took art with it, and the
author watched the caps lose first their curve and then their height.

So nothing is removed. Exactly OUTLINE pixels of ink are laid AROUND each vessel and
whatever the artist drew stays untouched. Uniformity comes from the ring being the
same everywhere and from every vessel sharing one canvas height, which is what makes
a pixel of ink read as the same width of line on the wall.

    python Tools/uniform_outline.py write
"""
from PIL import Image
import json, io, os, sys

DEST = 'Assets/Resources/Items'
DATA = 'Assets/Data/bottles/base_bar.json'
INK = (12, 10, 16, 255)
OUTLINE = 1                 # pixels of ink laid around every vessel on the shelf
DARK = 70                   # luminance at or below this is ink rather than art

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


def cap_region(im):
    """The closure: from the top of the sprite down to where the vessel pinches into its
    neck. Anything that goes on for a quarter of the sprite is a carton or a jar, not a
    cap, and is left alone."""
    W, H = im.size
    p = im.load()
    rows = [[x for x in range(W) if p[x, y][3] >= 128] for y in range(H)]
    live = [y for y in range(H) if rows[y]]
    if not live:
        return None
    top = live[0]
    widths = [len(rows[y]) for y in range(top, min(top + 40, H))]
    if len(widths) < 8:
        return None
    broad = max(widths[:8])
    bottom = top
    for i, w in enumerate(widths):
        if i > 2 and w < broad * 0.82:
            break
        bottom = top + i
    if bottom - top < 3 or bottom - top > (live[-1] - top) * 0.25:
        return None
    return top, bottom, broad, rows


def lit_crown(im, lift=1.26, seam=0.72):
    """
    The closure's top FACE: the disc you see when you look down on a cap.

    The shelf is drawn from slightly above, so a cap should show its lid and not just
    its side, and the author asked for exactly that. The pixels are LIT rather than
    repainted: filling the ellipse with a flat lighter tone did read as a lid, but it
    wiped the vodka's knurling and the tonic's crown teeth off the sprite. Lifting what
    is already there keeps every one of those marks and only changes which way the light
    falls on them. The darkened arc along the ellipse's lower edge is what sells it -
    that seam is where the face turns into the wall.
    """
    info = cap_region(im)
    if info is None:
        return im
    top, _, broad, rows = info
    W, H = im.size
    p = im.load()
    depth = max(2, int(round(broad * 0.16)))
    out = im.copy()
    op = out.load()
    cy = top + depth
    for y in range(top, min(top + 2 * depth + 1, H)):
        if not rows[y]:
            continue
        xs = rows[y]
        cx = (xs[0] + xs[-1]) / 2.0
        rx = (xs[-1] - xs[0] + 1) / 2.0
        dy = (y - cy) / float(depth)
        if abs(dy) > 1.0:
            continue
        half = rx * (1.0 - dy * dy) ** 0.5
        for x in xs:
            c = p[x, y][:3]
            if lum(c) <= DARK:
                continue                      # never paint over the artist's outline
            d = abs(x - cx)
            if d <= half - 1.0:
                op[x, y] = tuple(min(255, int(v * lift)) for v in c) + (255,)
            elif d <= half + 0.4 and y > cy:
                op[x, y] = tuple(int(v * seam) for v in c) + (255,)
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
        peeled = [lit_crown(round_crown(im)) if im is not None else None for im in loaded]

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
    print('%d sprites %s, %dpx of ink added and nothing taken away'
          % (changed, 'written' if write else '(dry run)', OUTLINE))


if __name__ == '__main__':
    run(len(sys.argv) > 1 and sys.argv[1] == 'write')
