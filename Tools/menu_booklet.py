# -*- coding: utf-8 -*-
"""THE MENU, OPEN, and its page turn drawn frame by frame (2026-08-20).

The recipe book has been a landscape clipboard since the clipboard menu was retired into
it - a wooden board with a metal clip, 396x248, drawn at 1148x719 in the HUD. The author
asked for the scene to become what it actually is: "ince uzun bir restoran menusu", and
for the page to TURN when you go left or right, with the turn drawn rather than faked by
the engine ("animasyonu motorun icerisinden degil cizimde yapmani istiyorum ... gercekten
kitap sayfasi gibi").

SECOND DESIGN, and the correction is the whole shape of the thing: "menu acik bir kitap
gibi olmali, sagdan gelen sayfa soldaki sayfanin ustune binmeli". The first draft was one
tall page hinged on its left edge, so the leaf swung up and vanished behind the spine - a
page that goes AWAY, which is what a wall calendar does, not a book. An open book hinges
in the MIDDLE: the right leaf lifts, crosses the gutter, and comes down ON TOP of the left
page. That is a 180 degree turn, not 90, and the half nobody drew is the half that sells
it - the BACK of the leaf, arriving over the left page.

So the object is a spread now: two tall pages of the same 167x326 paper, a stitched gutter
between them, leather all round. Each page keeps the narrow menu proportion the author
asked for; the book they sit in is what got wider.

WHY PROCEDURAL AND NOT GENERATED. A turning page has to be the same paper, at the same
pixel, as the page it lifts off and the page it lands on. An image model cannot hold a
book still across twelve frames, and the seam would show on every flip. It is also free,
which matters after a casting round spent on a keyline lottery.

THE RULER. Authored at 370x346 art pixels and drawn at exactly 2x (740x692 HUD units) -
the room's own rate, one art pixel per stage unit. The board it replaces was authored at
396x248 and drawn at 1148x719, which is 2.899x: a fractional upscale, and the reason its
clip and its grain never looked as crisp as the room behind it.

WHAT IS DRAWN, and why each piece earns its pixels:
  the cover      leather in the Amber ramp - the bar's own brown. Flat steps, no gradient.
  the gutter     a stitched band down the CENTRE, with the paper stepping into shade on
                 both sides of it. Paper curving into the fold is the one cue that says
                 "book" rather than "two cards side by side".
  the page block stacked leaves along both outer edges, so the book has depth and the
                 turning page has somewhere it could have come from.
  the gold rules a hairline frame per page, a double rule under each heading zone and a
                 single rule above each foot. This is what says "restaurant menu".
  the paper      Cream[4], the palette's white - never #FFFFFF (14 v3 3).
  the ribbon     a bookmark tail out of the gutter, past the bottom edge.

THE TURN - a page PEELED, not a door swung (third model, and the one that reads).

The first two models hinged a rigid sheet and swung it like a door: the silhouette
stayed a rectangle, only its projected width moved, and the print was squeezed onto it
by a separate scale-and-mask - two computations that could disagree, and did, by a
pixel here and a pixel there ("tasmalar ve kaymalar"). The real gesture is a PEEL: the
sheet stays low, bends over a travelling roll, and everything past the roll lies flat
again face-up, creeping across the spine until it has covered the left page. Every
e-reader page curl is this model, because it is what paper actually does.

Per row the sheet splits into four regions, all driven by ONE number - the fold
position `a`, swept from the outer edge to the spine over the turn (smoothstepped, so
the flip starts gently and settles gently):

    [0 .. a]       still flat on the right page, UNMOVED. Its print neither scales nor
                   shifts - the advancing fold simply consumes it column by column.
    [a .. a+r]     the roll: paper bending up and over a radius-r curl. Drawn art -
                   two flat shade steps and the sheet's own edge on the silhouette.
    past the roll  the flipped part, lying flat again and showing the BACK of the
                   sheet: the next left page's print, shifted right by exactly
                   2a + pi*r, creeping left as `a` shrinks, landing at zero shift.
    the free edge  at 2a + pi*r - R: it crosses the spine mid-turn - the moment the
                   page lies over both halves of the book at once - and settles on the
                   left page's outer edge as `a` reaches nought.

The fold is ANGLED: the bottom corner leads, the way a hand pulls a page, so the first
frames read as a corner peel and the print on the flipped part shears very slightly -
which is the page rotating in plane, drawn honestly rather than suppressed.

WHY THERE CAN BE NO DRIFT (the author: "daha hesapli profesyonelce"). The frame drawer
and the ink mapper read the SAME per-row table, row_fold(t) - one source of truth. The
front print is clipped at the fold and never resampled; the back print is an integer
shift of the finished page and never resampled; the only region without live print is
the roll band itself, a strip a few pixels wide. Nothing anywhere is scaled, so there
is nothing to smear and nothing to slide.

ONE SET SERVES BOTH DIRECTIONS: forward is the frames in order, back is the same
frames reversed, because the fold is the same fold.

Run: py -3 Tools/menu_booklet.py
"""
import math
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, '..', 'Assets', 'Resources', 'Items')

# ── the ruler ────────────────────────────────────────────────────────────────
PAD = 10           # leather showing round the paper
GUT = 16           # the stitched gutter between the two pages
PW, PH = 167, 326  # one page's paper - the narrow menu proportion, kept from draft one
W = PAD + PW + GUT + PW + PAD          # 370
H = PAD + PH + PAD                     # 346
TAIL = 8           # canvas rows below the board, for the ribbon to hang into

# Where everything lands. The UI is placed against these numbers rather than against a
# measurement taken off the PNG later - the same law HeadY lives under.
LEFT_X0 = PAD
LEFT_X1 = PAD + PW - 1
RIGHT_X0 = PAD + PW + GUT
RIGHT_X1 = RIGHT_X0 + PW - 1
PAPER_Y0 = PAD
PAPER_Y1 = PAD + PH - 1
SPINE_X = PAD + PW + GUT // 2          # the hinge

# The turning leaf's own canvas: the whole paper span, both pages and the gutter.
LEAF_X0 = LEFT_X0
LEAF_W = RIGHT_X1 - LEFT_X0 + 1
LEAF_H = PH
HINGE = SPINE_X - LEAF_X0              # the hinge inside that canvas
REACH = LEAF_W - HINGE                 # how far the leaf reaches when it lies flat

FRAMES = 16   # 12 read as steps at the fast end of the dial; 16 is smooth at 40ms


# ── the palette, straight out of UITheme (14 v3 3) ───────────────────────────
def rgb(v, a=255):
    return ((v >> 16) & 255, (v >> 8) & 255, v & 255, a)


AMBER = [rgb(0x4A2E14), rgb(0x8F5A1E), rgb(0xC9822B), rgb(0xE8A33D), rgb(0xF5C97B)]
CREAM = [rgb(0x453E38), rgb(0x6E6459), rgb(0x9C8F80), rgb(0xC9BCA8), rgb(0xF2E8D5)]
MALT = [rgb(0x3A2410), rgb(0x6B4416), rgb(0x9E6A1D), rgb(0xC98F2B), rgb(0xE6B959)]
VICE = [rgb(0x3D1220), rgb(0x6E1B32), rgb(0xA62B44), rgb(0xD9455C), rgb(0xF27D8A)]
CLEAR = (0, 0, 0, 0)

_SIZES = {}


def px_of(im):
    p = im.load()
    _SIZES[id(p)] = im.size
    return p


def rect(p, x0, y0, x1, y1, c):
    w, h = _SIZES[id(p)]
    for y in range(max(0, y0), min(h, y1 + 1)):
        for x in range(max(0, x0), min(w, x1 + 1)):
            p[x, y] = c


def frame_(p, x0, y0, x1, y1, c):
    rect(p, x0, y0, x1, y0, c)
    rect(p, x0, y1, x1, y1, c)
    rect(p, x0, y0, x0, y1, c)
    rect(p, x1, y0, x1, y1, c)


def chamfer(p, x0, y0, x1, y1, cut):
    """Knock the corners off - the room's own chamfer, so the board is not a hard box."""
    for dy in range(cut):
        for dx in range(cut - dy):
            p[x0 + dx, y0 + dy] = CLEAR
            p[x1 - dx, y0 + dy] = CLEAR
            p[x0 + dx, y1 - dy] = CLEAR
            p[x1 - dx, y1 - dy] = CLEAR


def page_frame():
    """ONE PAGE'S PRINTED FURNITURE, on its own transparent sheet: the gold hairline
    frame, the double rule under the heading and the single rule over the foot.

    IT IS NOT PART OF THE BOARD, and that is the fix for a fault the author caught in
    the preview: "sayfa degisirken sayfadaki yazilar duruyor fakat sayfada kullanilan
    altin renkli cizgiler ... gozukmuyor". The rules were drawn into the cover, so they
    belonged to the BOOK rather than to the page - the moment a leaf came over the top,
    the type went with it and the gold stayed behind on the furniture underneath. A page's
    printed frame is printed ON THAT PAGE; it has to travel with it.

    So it ships as a page-sized sprite that the UI lays on each page inside the same
    container as the type. During a turn that whole container is masked to the leaf and
    squeezed toward the hinge, and the gold foreshortens with the words, which is what
    the eye was missing.
    """
    im = Image.new('RGBA', (PW, PH), CLEAR)
    p = px_of(im)
    frame_(p, 5, 5, PW - 6, PH - 6, AMBER[2])
    hy = 34
    rect(p, 9, hy, PW - 10, hy, AMBER[2])
    rect(p, 9, hy + 2, PW - 10, hy + 2, AMBER[1])
    fy = PH - 23
    rect(p, 9, fy, PW - 10, fy, AMBER[1])
    return im


def cover():
    """The open book: leather, page block, two sheets of paper, the stitched gutter."""
    im = Image.new('RGBA', (W, H + TAIL), CLEAR)
    p = px_of(im)

    # THE LEATHER, two steps: the body, and one darker step along the bottom and the
    # right so the board has a thickness rather than being a flat brown rectangle.
    rect(p, 0, 0, W - 1, H - 1, AMBER[0])
    rect(p, 0, H - 3, W - 1, H - 1, MALT[0])
    rect(p, W - 3, 0, W - 1, H - 1, MALT[0])
    rect(p, 0, 0, W - 1, 1, AMBER[1])
    chamfer(p, 0, 0, W - 1, H - 1, 3)

    # THE PAGE BLOCK: the leaves this book is made of, stacked along both outer edges.
    # Without them the paper is a sticker on a board and the turning page has nowhere it
    # could have come from.
    for i, c in ((1, CREAM[3]), (3, CREAM[2]), (5, CREAM[3])):
        rect(p, LEFT_X0 - i, PAPER_Y0 + i, LEFT_X0 - i, PAPER_Y1 - i, c)
        rect(p, RIGHT_X1 + i, PAPER_Y0 + i, RIGHT_X1 + i, PAPER_Y1 - i, c)

    # THE TWO SHEETS - the paper and nothing printed on it. Everything printed lives on
    # the page's own sheet (page_frame) so that it turns when the page turns.
    for x0, x1 in ((LEFT_X0, LEFT_X1), (RIGHT_X0, RIGHT_X1)):
        rect(p, x0, PAPER_Y0, x1, PAPER_Y1, CREAM[4])

    # THE GUTTER: the stitched band, and - the part that makes it a book - the paper
    # stepping into shade as it curves down into the fold, two flat steps a side.
    rect(p, LEFT_X1 - 5, PAPER_Y0, LEFT_X1 - 2, PAPER_Y1, CREAM[3])
    rect(p, LEFT_X1 - 1, PAPER_Y0, LEFT_X1, PAPER_Y1, CREAM[2])
    rect(p, RIGHT_X0, PAPER_Y0, RIGHT_X0 + 1, PAPER_Y1, CREAM[2])
    rect(p, RIGHT_X0 + 2, PAPER_Y0, RIGHT_X0 + 5, PAPER_Y1, CREAM[3])
    rect(p, LEFT_X1 + 1, PAPER_Y0 - 1, RIGHT_X0 - 1, PAPER_Y1 + 1, MALT[0])
    rect(p, SPINE_X, PAPER_Y0 - 1, SPINE_X, PAPER_Y1 + 1, rgb(0x2A1A0C))
    for y in range(PAPER_Y0 + 6, PAPER_Y1 - 4, 8):
        rect(p, SPINE_X - 3, y, SPINE_X - 3, y + 3, AMBER[2])
        rect(p, SPINE_X + 3, y, SPINE_X + 3, y + 3, AMBER[2])

    # THE RIBBON, out of the gutter and past the foot. All anyone ever sees of a bookmark
    # is its tail, so that is all that is drawn - down the page it was a stripe through
    # the middle of the menu. One notch cut in the end: a square end reads as a peg.
    rx = SPINE_X - 3
    rect(p, rx, H - 6, rx + 6, H + TAIL - 1, VICE[1])
    rect(p, rx, H - 6, rx + 2, H + TAIL - 1, VICE[2])
    for i in range(3):
        rect(p, rx + 2 - i, H + TAIL - 1 - i, rx + 4 + i, H + TAIL - 1 - i, CLEAR)
    return im


def ease(t):
    """Smoothstep. The fold accelerates out of the corner and brakes into the spine,
    which is the cadence of a hand doing it; constant frame times then give the eased
    motion for free."""
    return t * t * (3.0 - 2.0 * t)


def fold_params(t):
    """The turn's three numbers at time t: mid-height fold position, how far the bottom
    corner leads, and the roll radius. Everything else derives from these in row_fold -
    the ONE table both the frame drawer and the ink mapper read."""
    a_mid = REACH * (1.0 - ease(t))
    lead = 22.0 * math.sin(math.pi * t)
    r = 1.0 + 7.0 * math.sin(math.pi * t)
    return a_mid, lead, r


def frame_times():
    """Where the shipped frames sample the turn. The last lands at t=1 exactly - its
    residual shift is the roll's own ~3px, which reads as the page settling when the
    static spread replaces it."""
    return [(i + 1) / float(FRAMES) for i in range(FRAMES)]


def row_fold(t):
    """Per paper row: (a, w, x_e, shift), all in spine coordinates.

    a       the fold position on this row (the bottom rows lead)
    w       the roll band's projected width at this row
    x_e     where the free edge lies, or None while the sheet has not yet laid a
            flipped part down (the opening corner-peel frames)
    shift   how far right the back-face print sits from its final resting place -
            an INTEGER, because a shifted column is crisp and a scaled one is not
    """
    a_mid, lead, r = fold_params(t)
    arc = math.pi * r
    rows = []
    for y in range(LEAF_H):
        yn = y / float(LEAF_H - 1)
        a = max(0.0, min(float(REACH), a_mid + lead * (0.5 - yn)))
        consumed = REACH - a
        if consumed < arc:
            # not enough sheet past the roll to lie down yet: the corner is lifting
            phi = min(math.pi / 2.0, consumed / max(0.001, r))
            rows.append((int(round(a)), int(round(r * math.sin(phi))), None, None))
        else:
            shift = int(round(2.0 * a + arc))
            rows.append((int(round(a)), int(round(r)), shift - REACH, shift))
    return rows


# The shadow a floating sheet throws where it is NOT over its own paper: one flat
# translucent step (a shadow that fades is a gradient, 16 6.10). Where it falls on the
# sheet's own front face it is painted as the opaque cream step instead - same depth,
# no blending arithmetic inside one PNG.
SHADOW = (13, 8, 19, 64)


def leaf(t):
    """One frame of the peel, over the paper span and nothing else.

    The frame is the SHEET alone: opaque cream wherever paper covers the book (its
    print is composited over this by the UI, clipped by the same row_fold numbers),
    the roll band's shading, the paper edges, and the two cast shadows. Everything to
    the right of the roll's silhouette is transparent - the next right page shows
    through there.
    """
    im = Image.new('RGBA', (LEAF_W, LEAF_H), CLEAR)
    p = px_of(im)
    _unused, _unused2, r = fold_params(t)
    shw = 2 + int(round(1.3 * r))
    for y, (a, w, x_e, shift) in enumerate(row_fold(t)):
        flipped = x_e is not None
        # the sheet's whole coverage this row: bound edge (or landed free edge) out to
        # the roll's silhouette
        lo = 8 if (not flipped or x_e >= 8) else x_e
        hi = min(a + w, REACH - 1)
        if hi >= lo:
            rect(p, HINGE + lo, y, HINGE + hi, y, CREAM[4])
        # the gutter-side shade the resting page carries (the cover draws these same
        # pixels): kept while the sheet still lies there, gone once the flipped part
        # has crept over it
        if a > 13 and (not flipped or x_e > 13):
            rect(p, HINGE + 8, y, HINGE + 9, y, CREAM[2])
            rect(p, HINGE + 10, y, HINGE + 13, y, CREAM[3])
        # the roll: cream into shade into the sheet's own edge on the silhouette, then
        # one step of shadow on the page being revealed
        if w > 0 and hi >= lo:
            half = a + max(1, w // 2)
            if half <= hi:
                rect(p, HINGE + half, y, HINGE + hi, y, CREAM[3])
            rect(p, HINGE + hi, y, HINGE + hi, y, CREAM[2])
            for x in range(a + w + 1, min(REACH - 1, a + w + shw) + 1):
                p[HINGE + x, y] = SHADOW
        # the free edge, and the shadow it throws ahead of itself: on the sheet's own
        # front face as an opaque step, on the gutter or the left page as translucence
        if flipped:
            ex = HINGE + x_e
            if 0 <= ex < LEAF_W:
                p[ex, y] = CREAM[2]
            for x in range(x_e - shw, x_e):
                lx = HINGE + x
                if lx < 0:
                    continue
                if x >= 8 and x_e >= 8:
                    p[lx, y] = CREAM[3]
                else:
                    p[lx, y] = SHADOW
    return im


def main():
    out = os.path.abspath(OUT)
    os.makedirs(out, exist_ok=True)
    cover().save(os.path.join(out, 'menu_booklet.png'))
    page_frame().save(os.path.join(out, 'menu_page_frame.png'))
    for i, t in enumerate(frame_times()):
        leaf(t).save(os.path.join(out, 'menu_page_%02d.png' % i))
        a_mid, lead, r = fold_params(t)
        print('  frame %02d  t=%.3f  a=%6.1f  lead=%5.1f  r=%4.1f'
              % (i, t, a_mid, lead, r))
    print('spread %dx%d (+%d tail)  pages L x%d-%d  R x%d-%d  y%d-%d  hinge x%d'
          % (W, H, TAIL, LEFT_X0, LEFT_X1, RIGHT_X0, RIGHT_X1,
             PAPER_Y0, PAPER_Y1, SPINE_X))
    print('leaf canvas %dx%d at (%d,%d), %d frames'
          % (LEAF_W, LEAF_H, LEAF_X0, PAPER_Y0, FRAMES))
    print('drawn at 2x -> %dx%d HUD; one page %dx%d'
          % (W * 2, (H + TAIL) * 2, PW * 2, PH * 2))


if __name__ == '__main__':
    main()
