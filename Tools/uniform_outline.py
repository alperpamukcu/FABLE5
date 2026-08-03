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

So nothing of the ARTIST'S is removed. Exactly OUTLINE pixels of ink are laid AROUND
each vessel and whatever the artist drew stays untouched. Uniformity comes from the
ring being the same everywhere and from every vessel sharing one canvas height, which
is what makes a pixel of ink read as the same width of line on the wall.

The third wrong version is also recorded here (2026-08-03): the pass was not
idempotent. ring() laid one more pixel of ink every time it ran and lit_crown lifted
the same crown by 1.26 every time it ran, and the pass had by then run on every
commit that touched the shelf - the style bottles were carrying four to six rings and
their canvases had swollen from 162 to 172, which is why their outlines read heavier
than the tier bottles rebuilt fresh from their raw takes. Two rules fix it:

  - our own ink is peeled before the ring is laid. Safe where peeling art was not,
    because OUR ink is one exact RGBA value the palettes never contain - what is
    peeled is provably what ring() put there, however many times it ran.
  - the crown steps run once. The sprite records that they have run in a PNG text
    chunk, and a marked sprite keeps the crown it has.

    python Tools/uniform_outline.py write
"""
from PIL import Image, PngImagePlugin
import json, io, os, sys

DEST = 'Assets/Resources/Items'
DATA = 'Assets/Data/bottles/base_bar.json'
INK = (12, 10, 16, 255)
OUTLINE = 1                 # pixels of ink laid around every vessel on the shelf
DARK = 70                   # luminance at or below this is ink rather than art
MARK = 'lastcall-crowned'   # PNG text chunk: the crown steps have run on this sprite

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


def peel_own_ink(im, layers=None):
    """Strip OUR ink - every accumulated layer of it, or exactly `layers` of them.

    What ring() writes is a complete hull: after it runs, EVERY boundary pixel of the
    silhouette is exactly INK. So a layer is peeled only when the whole boundary
    matches - matching pixel by pixel is not enough, because near-black art quantizes
    to INK's exact value now and then (the stout keg's own rim does), and a per-pixel
    peel ate nineteen columns of that keg before this rule replaced it.

    The mark is the second guard. The olive jar's sprig is DRAWN in ink's exact
    value, so its hull is complete right down into the art and the peel nibbled
    leaf pixels on every run, in any variant tried - capped, uncapped, it cannot be
    told apart from a ring by looking at pixels. So a sprite that carries the pass's
    mark is never peeled at all: the ring it wears is known to be one layer, ring()
    itself is a no-op on it, and the peel exists only as the one-time migration for
    unmarked legacy sprites, where the rings had stacked up. The canvas is not
    cropped here - the kegs carry deliberate padding that keeps the three of them
    at one scale."""
    out = im.copy()
    op = out.load()
    W, H = out.size
    taken = 0
    while layers is None or taken < layers:
        boundary = []
        for x in range(W):
            for y in range(H):
                if op[x, y][3] <= 40:
                    continue
                if any(not (0 <= x + dx < W and 0 <= y + dy < H)
                       or op[x + dx, y + dy][3] <= 40
                       for dx in (-1, 0, 1) for dy in (-1, 0, 1)):
                    boundary.append((x, y))
        if not boundary or any(op[x, y] != INK for x, y in boundary):
            break
        for x, y in boundary:
            op[x, y] = (0, 0, 0, 0)
        taken += 1
    return out


def ring(im, thickness=OUTLINE):
    """Lay exactly this many pixels of ink around the ART, in place.

    The ink grows from pixels that are not already our ink, which is what makes a
    second run a no-op: the hull the first run laid is neither transparent (so it is
    never re-stamped) nor a source to grow from (so nothing lands beyond it). The old
    version grew from everything solid and fattened the outline by one pixel every
    time it was called."""
    out = im.copy()
    op = out.load()
    WO, HO = out.size
    for _ in range(thickness):
        solid = [[op[x, y][3] > 40 and op[x, y] != INK for y in range(HO)]
                 for x in range(WO)]
        grew = []
        for x in range(WO):
            for y in range(HO):
                if op[x, y][3] > 40:
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


def vessels():
    """Every vessel on the wall: the shelf styles, and the brands that were drawn their
    own bottle. A tier bottle has to carry exactly the edge the rest of the shelf does,
    or the expensive vodka arrives outlined differently from the cheap one."""
    for style in styles():
        yield style
    for f in sorted(os.listdir(DEST)):
        if f.startswith('bot_') and f.endswith('.png') and not f.endswith('_open.png'):
            yield f[:-4]


def run(write):
    changed = 0
    for style in vessels():
        paths = [os.path.join(DEST, style + '.png'),
                 os.path.join(DEST, style + '_open.png')]
        loaded, crowned = [], []
        for p in paths:
            if not os.path.exists(p):
                loaded.append(None)
                crowned.append(False)
                continue
            im = Image.open(p)
            crowned.append(MARK in (im.text if hasattr(im, 'text') else {}))
            loaded.append(im.convert('RGBA'))
        if loaded[0] is None:
            continue

        # A marked sprite is already exactly what this pass produces - peeling or
        # crowning it again is where every non-idempotence bug in this file's history
        # has lived, so it goes through untouched and only the no-op ring/stand steps
        # see it. An unmarked one is either legacy (accumulated rings come off) or
        # fresh from a build chain (nothing to peel), and gets the full treatment.
        prepped = []
        for im, has in zip(loaded, crowned):
            if im is None:
                prepped.append(None)
                continue
            if not has:
                im = peel_own_ink(im)
                if style not in KEGS:
                    bb = im.getbbox()
                    if bb:
                        im = im.crop(bb)
                im = lit_crown(round_crown(im))
            prepped.append(im)

        # A shut bottle and its capless twin are padded by the SAME amount, so the
        # bottle does not jump when it is opened.
        need = [0, 0, 0, 0]
        for im in prepped:
            if im is None:
                continue
            for i, have in enumerate(margins(im)):
                need[i] = max(need[i], max(0, OUTLINE - have))
        done = [ring(pad_to(im, need)) if im is not None else None for im in prepped]
        if style not in KEGS:
            done = [stand_on_floor(im) if im is not None else None for im in done]

        for path, im in zip(paths, done):
            if im is None:
                continue
            if write:
                meta = PngImagePlugin.PngInfo()
                meta.add_text(MARK, '1')
                im.save(path, pnginfo=meta)
            changed += 1
        print('%-16s %-10s%s' % (style, '%dx%d' % done[0].size,
                                 '  open %dx%d' % done[1].size if done[1] else ''))
    print('%d sprites %s, ink peeled back to %dpx and the crowns kept'
          % (changed, 'written' if write else '(dry run)', OUTLINE))


if __name__ == '__main__':
    run(len(sys.argv) > 1 and sys.argv[1] == 'write')
