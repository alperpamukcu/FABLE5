# -*- coding: utf-8 -*-
"""Builds the wall television's sprite sheet from the generated ad plates
(2026-09-04). Run Tools/tv_ads_gen.py first; this writes the game asset.

WHAT IS GENERATED AND WHAT IS NOT. The four ADVERTS are PixelLab's (illustrative
content - the standing rule since 2026-08-03). The CABINET is drawn here because
it is a frame of Graphite around generated art, and the SHUT-DOWN and WARM-UP
frames are DERIVED from the picture that is playing rather than generated: a
separately generated "off" television comes back a different television, which
this project has already paid for three times (memory open-states-derive).

THE SHEET. One row per state, cell 80x60:
    row 0  ads   4 cells - one still per advert
    row 1  off   6 cells - the CRT collapsing: the picture is crushed to a band,
                           the band pulled to a dot, the dot goes out
    row 2  on    6 cells - NOT the collapse reversed; a tube lights as a line
                           that opens vertically and never has a dot stage
The stage plays an advert, runs row 1 to shut it, holds dark for five seconds,
then runs row 2 to bring the next advert up.

NO BAKED LIGHT. Not one pixel of glow, bloom or reflection is written here: the
room is lit by URP 2D lights and the CRT's spill is a Light2D the stage hangs
off the fixture (memory art-direction-rules, 2026-08-18). These frames carry the
PICTURE only.
"""
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'tv_ads_raw')
OUT = os.path.join(HERE, '..', 'Assets', 'Resources', 'Fixtures')
PREVIEW = os.path.join(HERE, 'tv_preview.png')

ADS = ['cocktail', 'flamingo', 'palmcar', 'beer']

# THE CABINET IS THE AUTHOR'S OWN DRAWING (2026-09-04: "Televizyon gorseli bu
# olacak"), not a generated or a procedural one. It is a 45x45 set seen at an
# angle - the screen face on the left, a depth panel down the right - so the
# picture goes inside its FACE rather than into a centred rectangle, and every
# measurement below is taken off the drawing itself.
#
# It lives with the other hand-drawn sources (the triptych, the rug, the sinks)
# because Tools/*.png at the root is git-ignored, and a build input that git does
# not carry is a build that only works on this machine.
CABINET = os.path.join('AssetPipeline', 'sources', 'konsept_art', 'wall_tv.png')

# FOUR PIXELS BIGGER (2026-09-04, the author: "tv boyutunu 4 pixel daha buyutup").
# The cabinet is scaled by whole pixels with NEAREST - 45 -> 49 is not an integer
# multiple, so the scale is done by nearest-neighbour resampling and then the
# result is re-flattened to the drawing's own four colours, because a resample
# that lands between two of them would invent a fifth and put the set off palette.
CAB_SRC = 45
GROW = 4
CELL_W, CELL_H = CAB_SRC + GROW, CAB_SRC + GROW      # 49x49

# THE SCREEN IS A TRAPEZOID, NOT A RECTANGLE (the author, same round: "icerisindeki
# ekrani tv perspektifine gore duzelt"). The set is drawn facing LEFT: its right
# edge is the near one and stands full height, its left edge is farther away and
# is shorter, so the top and bottom edges converge to the left. Measured off the
# drawing at CAB_SRC scale, the face runs 32 rows tall at its left and 41 at its
# right. A rectangle pasted into that reads as a sticker on the glass, which is
# exactly what was wrong before.
#
# These four numbers are the face's own corners in the 45x45 drawing, one pixel
# of face left all round as a bezel. They are scaled with the cabinet below, so a
# redraw needs new measurements here and nothing else.
#
# The face's own colour in the drawing - the one the advert is allowed to cover.
FACE_RGB = (70, 70, 70)
FACE_L, FACE_R = 2, 38          # left and right column of the picture
FACE_L_TOP, FACE_L_BOT = 8, 37  # the face's top/bottom row at FACE_L
FACE_R_TOP, FACE_R_BOT = 3, 41  # ...and at FACE_R

# What the adverts were generated at, and the only size they are ever read at.
AD_W, AD_H = 64, 40

OFF_FRAMES = 6
ON_FRAMES = 6

# UITheme.Graphite / Night / Cyan, verbatim - the cabinet is furniture, so it is
# built out of the material ramp and never out of a colour picked here.
GRAPHITE = [(0x14, 0x16, 0x1A), (0x24, 0x27, 0x2D), (0x38, 0x3D, 0x45),
            (0x54, 0x5A, 0x64), (0x80, 0x88, 0x93)]
NIGHT = [(0x0D, 0x08, 0x13), (0x1A, 0x10, 0x23), (0x24, 0x18, 0x30),
         (0x36, 0x24, 0x47), (0x4A, 0x31, 0x60)]
CYAN = [(0x12, 0x3B, 0x45), (0x1B, 0x5F, 0x66), (0x26, 0x91, 0x8F),
        (0x3B, 0xC8, 0xBE), (0x7D, 0xF0, 0xE3)]


def _dark(p):
    return sum(p) < 170


def _bezel(im):
    """How many rows/cols of self-drawn bezel each side carries.

    MEASURED, not assumed: the brief said "on a bar television" and two of the
    four plates drew their own frame - the cocktail's is 7px down the sides and
    the palm car has none at all. A fixed inset therefore either leaves a purple
    ring on one picture or eats into another. A side is bezel while 85% or more
    of its line is dark, which is the ring; a picture's own dark sky never fills
    a whole edge that consistently.
    """
    w, h = im.size

    def row(y):
        return sum(1 for x in range(w) if _dark(im.getpixel((x, y)))) / float(w)

    def col(x):
        return sum(1 for y in range(h) if _dark(im.getpixel((x, y)))) / float(h)

    # The ring is not always dark all the way out: the cocktail plate drew a
    # whole HOUSING, a light grey casing edge around a dark bezel, so a scan that
    # only counts dark lines stops on the casing at column 0 and reports no
    # bezel at all. Each side is walked while the line is EITHER mostly dark or
    # mostly flat (one colour across it) - a casing edge is flat, a picture's
    # own edge is not.
    lim = 8  # a frame thicker than this is a picture, not a bezel

    def flat_row(y):
        line = [im.getpixel((x, y)) for x in range(w)]
        return max(line.count(c) for c in set(line)) / float(w)

    def flat_col(x):
        line = [im.getpixel((x, y)) for y in range(h)]
        return max(line.count(c) for c in set(line)) / float(h)

    def edge(dark_f, flat_f, i):
        return dark_f(i) >= 0.85 or flat_f(i) >= 0.85

    def walk(dark_f, flat_f, at):
        """How deep the frame runs on one side.

        The outermost line is allowed to be a MIXED one and still count: a drawn
        casing has rounded corners, so the cocktail's column 0 is part sky and
        part plastic (flat .45, dark .47) while columns 1-6 behind it are solid
        bezel. Stopping at the first imperfect line therefore found no frame at
        all on the one plate that has the thickest. The rule is: keep walking
        while the line is a frame line, and let a single soft line pass if the
        line behind it is a frame line.
        """
        n = 0
        while n < lim:
            if edge(dark_f, flat_f, at(n)):
                n += 1
                continue
            if n == 0 and edge(dark_f, flat_f, at(1)):
                n += 1          # soft outer casing line, frame behind it
                continue
            break
        return n

    t = walk(row, flat_row, lambda n: n)
    b = walk(row, flat_row, lambda n: h - 1 - n)
    l = walk(col, flat_col, lambda n: n)
    r = walk(col, flat_col, lambda n: w - 1 - n)
    return l, t, r, b


def _despeckle(im):
    """Drops lone pixels that match none of their four neighbours.

    The beer plate came back with its purple ground stippled in ClubBlue - the
    forced palette pushed a colour the ground never wanted. Single stray pixels
    read as dirt on the glass at this size; a pixel that has at least one
    neighbour of its own colour is deliberate texture and is left alone.
    """
    w, h = im.size
    src = im.copy()
    px = im.load()
    sp = src.load()
    for y in range(1, h - 1):
        for x in range(1, w - 1):
            me = sp[x, y]
            around = [sp[x - 1, y], sp[x + 1, y], sp[x, y - 1], sp[x, y + 1]]
            if me in around:
                continue
            best, n = None, 0
            for c in around:
                k = around.count(c)
                if k > n:
                    best, n = c, k
            if n >= 3:
                px[x, y] = best
    return im


def load_ads():
    """The four plates, each cropped of whatever bezel the generator drew itself
    and pushed back out to 64x40 with NEAREST, so the four stay one size and
    stay pixel-crisp."""
    out = []
    for name in ADS:
        p = os.path.join(RAW, name + '.png')
        im = Image.open(p).convert('RGB')
        if im.size != (AD_W, AD_H):
            raise SystemExit('%s is %s, expected the generated %dx%d.'
                             % (name, im.size, AD_W, AD_H))
        l, t, r, b = _bezel(im)
        w0, h0 = im.size
        if l or t or r or b:
            im = im.crop((l, t, w0 - r, h0 - b))
        im = _despeckle(im)
        # NO RESIZE HERE any more. The picture is sampled straight onto the face
        # by with_picture, one destination column at a time, so it is fitted to
        # the trapezoid in the same pass that draws it - resizing it to a
        # rectangle first would throw away rows the warp still needs and would
        # resample the art twice for one result.
        print('  %-9s bezel L%d T%d R%d B%d (kept %dx%d)'
              % (name, l, t, r, b, im.size[0], im.size[1]))
        out.append(im)
    return out


def _snap(im, palette):
    """Forces every pixel back onto the drawing's own colours.

    NEAREST scaling cannot invent a colour, but it CAN leave a half-transparent
    edge pixel; anything that is not one of the four colours the author drew with
    is pulled to the closest of them, so a 49x49 set is the same four colours the
    45x45 one was.
    """
    px = im.load()
    for y in range(im.size[1]):
        for x in range(im.size[0]):
            r, g, b, a = px[x, y]
            if a < 128:
                px[x, y] = (0, 0, 0, 0)
                continue
            best, bd = palette[0], None
            for c in palette:
                d = (r - c[0]) ** 2 + (g - c[1]) ** 2 + (b - c[2]) ** 2
                if bd is None or d < bd:
                    best, bd = c, d
            px[x, y] = best + (255,)
    return im


def cabinet():
    """The author's own set, loaded rather than drawn, and grown by GROW pixels.

    A prop in the room is illustrative content and this project does not let me
    draw one (memory art-direction-rules, 2026-08-19: the hand-drawn tap and till
    were rejected outright). The author drew this one and handed it over, so it
    is the asset - the build's only job is to size it and seat the adverts in it.
    """
    p = os.path.join(HERE, CABINET)
    im = Image.open(p).convert('RGBA')
    if im.size != (CAB_SRC, CAB_SRC):
        raise SystemExit('%s is %s, expected %dx%d - the drawing is the cell'
                         % (CABINET, im.size, CAB_SRC, CAB_SRC))
    pal = []
    for c in im.getdata():
        if c[3] >= 128 and c[:3] not in pal:
            pal.append(c[:3])
    im = im.resize((CELL_W, CELL_H), Image.NEAREST)
    return _snap(im, pal)


def _scale(v):
    """A measurement taken on the 45x45 drawing, in the grown cabinet's pixels."""
    return int(round(v * CELL_W / float(CAB_SRC)))


def with_picture(cab, pic):
    """One advert seated in the set's face, WARPED TO ITS PERSPECTIVE.

    The set faces left: its near (right) edge is tall and its far (left) edge is
    short, so the picture is drawn column by column, each one squeezed to the
    height the face has at that column. A straight rectangle - what this did
    before - reads as a sticker laid on the glass rather than as a screen inside
    a box, which is what the author sent back.

    The picture goes UNDER the cabinet's own dark edges rather than over them:
    that outline is what makes the box read as a box, so the face's colour is
    the only thing the advert is allowed to replace.
    """
    out = cab.copy()
    op = out.load()
    src = pic.convert('RGB')
    sp = src.load()
    sw, sh = src.size

    l, r = _scale(FACE_L), _scale(FACE_R)
    lt, lb = _scale(FACE_L_TOP), _scale(FACE_L_BOT)
    rt, rb = _scale(FACE_R_TOP), _scale(FACE_R_BOT)
    span = float(max(1, r - l))

    for x in range(l, r + 1):
        f = (x - l) / span                      # 0 at the far edge, 1 at the near
        top = lt + (rt - lt) * f
        bot = lb + (rb - lb) * f
        h = bot - top
        if h < 1:
            continue
        # the advert's column that belongs at this depth
        u = min(sw - 1, int(f * (sw - 1) + 0.5))
        y0, y1 = int(round(top)), int(round(bot))
        for y in range(y0, y1 + 1):
            v = (y - top) / h                   # 0 at the face's top, 1 at its foot
            sy = min(sh - 1, max(0, int(v * (sh - 1) + 0.5)))
            op[x, y] = sp[u, sy] + (255,)

    # the cabinet's own edges go back on top of the advert
    cp = cab.load()
    for y in range(CELL_H):
        for x in range(CELL_W):
            c = cp[x, y]
            if c[3] > 0 and c[:3] != FACE_RGB:
                op[x, y] = c
    return out


def squeeze(pic, t):
    """The CRT collapse, derived from the picture that is playing.

    t runs 0 (full picture) to 1 (nothing). The tube does two things and in this
    order: the image is crushed VERTICALLY toward the centre line at full width,
    then what is left is pulled in horizontally to a dot. Both stages resample
    with NEAREST - a smoothed collapse is a blurred one, and blur is the thing
    this project's whole look is built against (GDD 16 6.10).
    """
    w, h = pic.size
    out = Image.new('RGB', (w, h), NIGHT[0])
    if t >= 1.0:
        return out
    if t <= 0.6:
        f = t / 0.6
        nh = max(2, int(round(h * (1.0 - f) + 2 * f)))
        out.paste(pic.resize((w, nh), Image.NEAREST), (0, (h - nh) // 2))
    else:
        f = (t - 0.6) / 0.4
        nw = max(1, int(round(w * (1.0 - f))))
        band = pic.resize((w, 2), Image.NEAREST).resize((nw, 2), Image.NEAREST)
        out.paste(band, ((w - nw) // 2, h // 2 - 1))
    return out


def warm(pic, f):
    """The warm-up: a line that opens vertically. No dot stage - a tube lights
    differently than it dies, and playing the collapse backwards reads as a
    rewind rather than a switch-on."""
    w, h = pic.size
    scr = Image.new('RGB', (w, h), NIGHT[0])
    if f >= 1.0:
        return pic.copy()
    if f > 0.0:
        nh = max(1, int(round(h * f)))
        scr.paste(pic.resize((w, nh), Image.NEAREST), (0, (h - nh) // 2))
    return scr


def build():
    ads = load_ads()
    cab = cabinet()

    # The collapse and the warm-up are shot against the FIRST advert. They are
    # one set rather than one per advert because the tube's shape is the same
    # whatever was on it, and four sets would quadruple the sheet to no gain.
    field = ads[0]

    rows = [
        [with_picture(cab, a) for a in ads],
        [with_picture(cab, squeeze(field, i / float(OFF_FRAMES - 1)))
         for i in range(OFF_FRAMES)],
        [with_picture(cab, warm(field, i / float(ON_FRAMES - 1)))
         for i in range(ON_FRAMES)],
    ]

    cols = max(len(r) for r in rows)
    sheet = Image.new('RGBA', (cols * CELL_W, len(rows) * CELL_H), (0, 0, 0, 0))
    for ri, row in enumerate(rows):
        for ci, cell in enumerate(row):
            sheet.paste(cell, (ci * CELL_W, ri * CELL_H))

    dest = os.path.join(OUT, 'fx_tv.png')
    sheet.save(dest)
    print('wrote', os.path.normpath(dest), sheet.size,
          '(%d cols x %d rows of %dx%d)' % (cols, len(rows), CELL_W, CELL_H))

    sheet.resize((sheet.width * 3, sheet.height * 3), Image.NEAREST).save(PREVIEW)
    print('preview', os.path.normpath(PREVIEW))


if __name__ == '__main__':
    build()
