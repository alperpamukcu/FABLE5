# -*- coding: utf-8 -*-
"""The cellar's OPEN sign, and the arrow under it (2026-08-25, the author: "Menu butonu
kaldirilsin onun yerine ekrandaki raflarin onundeki kapaga, text olarak Open ve asagi ok
koyulsun ... en dis katman magenta ici beyaz-koyu pembe ve en icerisi govde kismida pembe
olan italik duvar yazisi tasariminda bir yazi gorseli uret ve Open yazsin yuksekligi 34
pixeli gecmesin yazinin").

DRAWN, NOT GENERATED. This is a control's label - the word that says how you get into the
counter - so it is UI chrome, and UI chrome in this project is never a model's guess (14
sec.5 / 16 sec.0; the two written exceptions are the calendar backplate and the star icon).
PixelLab could not do it anyway: it cannot write text. So the letters are struck here with
a round pen, sheared into their slant, and grown outward into their three coats.

THE THREE COATS ARE THE BRIEF, in the author's own order, and they are grown from the
letter rather than drawn beside it, which is what keeps them exactly parallel at every
corner a hand would have wobbled:

    body      the innermost, pink            Magenta[4] over Magenta[3] below the waist
    lining    white going into dark pink     Cream[4]  over Magenta[1] below the waist
    keyline   the outermost, magenta         Magenta[1] - the deep one, so it reads as ink

Every colour is a step of UITheme's Magenta or Cream ramp. Nothing is anti-aliased: the pen
is struck at 8x and thresholded at half coverage, so what lands is a hard 1-bit mask that
the coats are dilated off. The word is capped at 34 px tall INCLUDING both coats, because
34 is what the author asked for and the coats are part of the writing.

    py -3 Tools/open_sign_gen.py --takes    # all three hands, side by side, to choose from
    py -3 Tools/open_sign_gen.py <take>     # write that take's sprites + the preview sheet
    py -3 Tools/open_sign_gen.py --preview  # the sheet only, nothing shipped

It also ships light_spill.png, the warm sliver that comes out of the roller when it is held
open a crack under the pointer (DiegeticStage.BuildShutterLight) - same fixture, same tool.
"""
import io
import os
import sys

from PIL import Image, ImageChops, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')

S = 8                       # supersample: struck at 8x, thresholded down to hard pixels

# THREE HANDS, ONE WORD (2026-08-25, the author: "Open yazisi tekrardan olusturulsun").
# The first sign shipped and was sent back, so the tool stopped having ONE answer and started
# having a choice: the same three coats and the same 34 px ceiling, struck by three different
# hands. What varies is only what a hand varies - how hard the pen is pressed, how far the
# word leans, how tight the letters are packed, and how far the flick runs off the 'n'.
# Everything structural (the coats, the ceiling, the counters-first rule) is shared, because
# those are the brief and not the handwriting.
#
#   tag     the first shipped one: an even marker, moderate lean
#   fat     a throw-up: heavy pen, wide letters, standing straighter
#   quick   a fast tag: thin pen, hard lean, packed tight, long flick
#   wall    the shipped one: the same marker held further back, letters given air
#
# WHY 'wall' IS THE DEFAULT (2026-08-25, the author: "open yazisini degistir istersen
# yazani da degistir"). The three earlier hands all fought the same losing battle: at a
# 34 px ceiling the counters are what a coat eats first, and every one of them was struck
# TIGHT, so the 'e' and the 'p' closed and the word read as four leaning blobs. 'wall' does
# not press harder, it spaces wider - the letters sit further apart and the bowls are drawn
# rounder, which is the one change that buys daylight without buying height. It also stands
# up straighter, because a sign that has to be READ before it is admired leans less.
TAKES = {
    'tag':   dict(shear=0.26, weight=1.00, track=1.00, round=0.0, flick=1.00),
    'fat':   dict(shear=0.20, weight=1.22, track=1.08, round=0.9, flick=0.70),
    'quick': dict(shear=0.36, weight=0.80, track=0.92, round=-0.4, flick=1.55),
    'wall':  dict(shear=0.21, weight=1.02, track=1.16, round=0.8, flick=0.85),
}
TAKE = dict(TAKES['wall'])
SHEAR = TAKE['shear']


def use(name):
    """Pick up a take. Everything downstream reads TAKE, so this is the only switch."""
    global TAKE, SHEAR
    if name not in TAKES:
        raise SystemExit('no such take: %s (have %s)' % (name, ', '.join(sorted(TAKES))))
    TAKE = dict(TAKES[name])
    SHEAR = TAKE['shear']

# UITheme's ramps, by name, so a palette change is a change in one project and not two.
MAGENTA = ('#5C1B45', '#8F2464', '#C23283', '#E84DA6', '#FF7DC6')
CREAM = ('#453E38', '#6E6459', '#9C8F80', '#C9BCA8', '#F2E8D5')

# FOUR PINKS, and they have to stay four. The first take ran the lining's lower band and
# the keyline both at Magenta[1] and the bottom half of every letter fused into one dark
# mass - the coats were there and could not be seen, which is the whole failure this
# three-coat brief exists to avoid. Each band is now a clear ramp step away from its
# neighbour: white -> dark pink -> deep magenta going outward, pink in the middle.
BODY_HI, BODY_LO = MAGENTA[4], MAGENTA[3]      # pink, and the pink under the waist
LINE_HI, LINE_LO = CREAM[4], MAGENTA[2]        # white going into dark pink
KEYLINE = MAGENTA[1]

# The word's own geometry, in FINAL pixels, and every number in it is set by ONE fact:
# a coat grows into the counters as well as out of the silhouette, so a 2 px coat costs
# every hole in the word 4 px of daylight. Take two was struck at 2 and 2 and the 'e', the
# 'p' bowl and the 'O' all filled in solid - three coats, none of them visible, the word a
# leaning smudge. So the coats are 1 px each and the letters are given the height back:
# a 17 px x-height under a 23 px cap, which is graffiti's own proportion anyway.
#
# 30 rows of letter plus 1 + 1 of coat top and bottom is 34, which is the author's ceiling
# to the pixel. Nothing may be added to it.
CAP_TOP, XTOP, WAIST, BASELINE, DESCENDER = 2, 8, 16, 25, 30
LINING, KEY = 1, 1          # how far each coat grows past the one inside it


def hexcol(h):
    h = h.lstrip('#')
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)


# ---------------------------------------------------------------------------
# the pen
# ---------------------------------------------------------------------------

def ring(cx, cy, rx, ry, a0=0.0, a1=360.0, steps=64):
    """Points along an ellipse, degrees measured clockwise from 3 o'clock (y grows down)."""
    import math
    out = []
    n = max(2, int(steps * abs(a1 - a0) / 360.0))
    for i in range(n + 1):
        a = math.radians(a0 + (a1 - a0) * i / float(n))
        out.append((cx + rx * math.cos(a), cy + ry * math.sin(a)))
    return out


def glyphs():
    """Every stroke of 'Open bar', as (points, pen width) in final pixels.

    WHY TWO WORDS (2026-08-25). The roller is 592 px across and the sign was 90 of them:
    the ceiling that matters here is the HEIGHT, and width was never spent. 'Open' alone
    also said the same thing twice, because the chevron under it already points the way the
    roller travels - so the second word is free room used to say something the arrow cannot.
    It is what a bar's shutter would actually be tagged with, and it is still the verb that
    tells the player what the click does.

    THE COUNTERS ARE THE SPECIFICATION. Take one was struck with a 5 px pen at this size
    and every hole in the word closed: the 'e' went solid and 'Open' read as four blobs
    leaning right. A letter is its holes at 26 px, so the pen is set from the counter
    outward - the ring radius and the pen together have to leave daylight - and the
    letters are spaced so no two keylines touch, because coats that merge are one coat.

    The capital carries the cap height; the three lowercase letters are struck a shade
    lighter, the way a marker thins when the hand speeds up, and the 'n' keeps a flick off
    its right leg - the one stroke that says this was written and not typeset.
    """
    mid = (XTOP + BASELINE) / 2.0                # the lowercase middle
    w = TAKE['weight']                           # how hard the pen is pressed
    t = TAKE['track']                            # how far apart the letters sit
    r = TAKE['round']                            # how much wider the bowls are drawn
    strokes = []

    # THE PEN AND THE COUNTER MOVE TOGETHER. A heavier take that kept these radii would shut
    # the holes it just spent its height opening, so every bowl is widened by half of what
    # the pen gained - which is exactly what a wider nib does on paper anyway.
    def x(v):
        return 8.2 + (v - 8.2) * t

    # O - the capital, the only letter carrying the full cap height. Narrow rather than
    # round: four letters and a slant have to fit a sign, and a circular O eats the room.
    strokes.append((ring(8.2, (CAP_TOP + BASELINE) / 2.0, 6.2 + r, 10.0), 3.0 * w))

    # p - stem into the descender, then the bowl hung off it.
    strokes.append(([(x(22.5), XTOP), (x(22.5), DESCENDER)], 3.0 * w))
    strokes.append((ring(x(27.5) + r, mid, 5.6 + r, 7.0), 3.0 * w))

    # e - a ring open at the lower right, with the bar across it. The bar rides HIGH, not
    # on the middle: at 17 px of x-height only one of an 'e''s two counters can survive
    # two coats, so the lower one is given all the room and the upper one is spent. What
    # is left reads as an 'e' - a closed shoulder, a slot, an open mouth - and would have
    # read as an 'o' with the bar centred and both counters shut.
    ecx, erx, ery = x(47.0) + r * 2, 5.8 + r, 7.0
    strokes.append((ring(ecx, mid, erx, ery, a0=32.0, a1=333.0), 2.8 * w))
    strokes.append(([(ecx - erx + 0.4, mid - 2.0), (ecx + erx - 0.8, mid - 2.0)], 2.0 * w))

    # n - leg, arch, leg, and the flick. The flick is the take's signature: it is the one
    # stroke that carries no information, so it is where a hand shows.
    nx = x(60.7) + r * 3
    arch = 4.5 + r * 0.5
    strokes.append(([(nx, XTOP), (nx, BASELINE)], 3.0 * w))
    strokes.append((ring(nx + arch, XTOP + 5.0, arch, 5.0, a0=180.0, a1=360.0), 3.0 * w))
    strokes.append(([(nx + arch * 2, XTOP + 5.0), (nx + arch * 2, BASELINE)], 3.0 * w))
    f = TAKE['flick']
    strokes.append(([(nx + arch * 2, BASELINE - 1.4),
                     (nx + arch * 2 + 4.0 * f, BASELINE - 5.2 * min(1.0, f))], 2.4 * w))

    # ── the second word ────────────────────────────────────────────────────
    # LOWER CASE THROUGHOUT, and the 'b' is the reason. A capital B at this cap height is
    # TWO counters stacked in 23 rows, and two coats eat both of them - the same failure
    # the 'e' was rebuilt to avoid. A lowercase 'b' is the 'p' turned upside down: one
    # counter, the one hole this size can actually keep, and a stem that carries the cap
    # height anyway so the word still opens on a tall letter.

    # b - the ascender, then the bowl hung off it at the x-height.
    bx = x(86.0) + r * 4
    strokes.append(([(bx, CAP_TOP), (bx, BASELINE)], 3.0 * w))
    strokes.append((ring(bx + 5.0 + r, mid, 5.6 + r, 7.0), 3.0 * w))

    # a - single-storey: the bowl, and the stem down its right side. Two strokes, one
    # counter, and no shoulder to close at this size.
    ax = x(107.0) + r * 5
    strokes.append((ring(ax, mid, 5.6 + r, 7.0), 3.0 * w))
    strokes.append(([(ax + 5.6 + r, XTOP + 1.0), (ax + 5.6 + r, BASELINE)], 3.0 * w))

    # r - leg and a shoulder that stops where a marker's would: the arch is cut at 310
    # degrees rather than run round, which is the whole difference between an 'r' and an 'n'.
    rx = x(122.0) + r * 6
    strokes.append(([(rx, XTOP), (rx, BASELINE)], 3.0 * w))
    strokes.append((ring(rx + arch, XTOP + 5.0, arch, 5.0, a0=180.0, a1=312.0), 3.0 * w))

    return strokes


def strike():
    """The word as a hard 1-bit mask: struck at 8x with a round pen, sheared, thresholded."""
    ink = glyphs()
    right = max(x for pts, w in ink for x, y in pts) + 4
    core = Image.new('L', (int(right * S), int((DESCENDER + 4) * S)), 0)
    pen = ImageDraw.Draw(core)
    for pts, width in ink:
        p = [(x * S, y * S) for x, y in pts]
        pen.line(p, fill=255, width=int(round(width * S)), joint='curve')
        # Round caps: PIL's line has none, so both ends are set by hand.
        for end in (p[0], p[-1]):
            r = width * S / 2.0
            pen.ellipse([end[0] - r, end[1] - r, end[0] + r, end[1] + r], fill=255)

    # THE SLANT IS APPLIED TO THE STRUCK LETTER, not to its points: shearing the path and
    # then striking it would turn a round pen into an oval one, and every stroke would
    # thicken on the diagonals. Sheared here, the pen stays round and the WORD leans.
    w, h = core.size
    grow = int(SHEAR * h) + 1
    core = core.transform((w + grow, h), Image.AFFINE, (1, SHEAR, -SHEAR * h, 0, 1, 0),
                          resample=Image.BILINEAR)

    small = core.resize((core.size[0] // S, core.size[1] // S), Image.BOX)
    return small.point(lambda v: 255 if v >= 128 else 0)


def grown(mask, by):
    """The mask dilated by `by` pixels - the coats are struck parallel to the letter rather
    than drawn beside it, which is what keeps them even at every corner."""
    out = mask.copy()
    for dx in range(-by, by + 1):
        for dy in range(-by, by + 1):
            if dx == 0 and dy == 0:
                continue
            if dx * dx + dy * dy > by * by + by:      # a rounded square, not a diamond
                continue
            shifted = Image.new('L', mask.size, 0)
            shifted.paste(mask, (dx, dy))
            out = ImageChops.lighter(out, shifted)
    return out


def coat(canvas, mask, hi, lo, split):
    """Paints one coat: `hi` above the waist, `lo` on and below it. Two flat runs with a
    hard seam - a band set, never a gradient (16 sec.6.10)."""
    px = canvas.load()
    mp = mask.load()
    w, h = canvas.size
    for y in range(h):
        colour = hi if y < split else lo
        for x in range(w):
            if mp[x, y]:
                px[x, y] = colour


def word():
    core = strike()
    lining = grown(core, LINING)
    keyline = grown(lining, KEY)

    # The seam sits on the waist, carried down by the slant the same way the letters are,
    # so the white sits on the shoulders of the word and the dark pink under its belt.
    split = WAIST

    # Painted outward-in: each coat covers the one before it and what is left showing is
    # the ring between them. Three passes, no masking arithmetic to get wrong.
    art = Image.new('RGBA', core.size, (0, 0, 0, 0))
    coat(art, keyline, hexcol(KEYLINE), hexcol(KEYLINE), split)
    coat(art, lining, hexcol(LINE_HI), hexcol(LINE_LO), split)
    coat(art, core, hexcol(BODY_HI), hexcol(BODY_LO), split)
    return trim(art)


def arrow(up=False):
    """The way it travels. A chevron in the same three coats, so the sign and its mark are
    one drawing - the roller goes DOWN to open, which is what the arrow has always said.

    Wide and shallow rather than tall: it sits under the word on a lid that is only a
    hand's width of art tall, and a deep chevron there reads as a second letter.

    ONE DRAWING, TWO DIRECTIONS (2026-08-25). The open cellar leaves a rail of roller
    standing at the sill and that rail is the way back out, so it carries the same chevron
    MIRRORED - struck here rather than flipped in Unity, because a sprite flipped by a
    negative scale is a sprite whose pixels no longer land on the grid. There is no waist
    on the chevron (see below), so the mirror is exact and not a second hand-drawing that
    could drift from the first."""
    w, h = 30, 13
    core = Image.new('L', (w * S, h * S), 0)
    pen = ImageDraw.Draw(core)
    pts = [(4 * S, 4 * S), (15 * S, 9 * S), (26 * S, 4 * S)]
    if up:
        pts = [(x, h * S - y) for x, y in pts]
    pen.line(pts, fill=255, width=int(3.0 * S), joint='curve')
    for end in (pts[0], pts[-1]):
        r = 1.5 * S
        pen.ellipse([end[0] - r, end[1] - r, end[0] + r, end[1] + r], fill=255)
    small = core.resize((w, h), Image.BOX).point(lambda v: 255 if v >= 128 else 0)

    # No waist on the arrow: it is one short stroke, and a seam across five pixels of it
    # would read as a printing fault rather than as the word's own shading.
    lining = grown(small, LINING)
    keyline = grown(lining, KEY)
    art = Image.new('RGBA', small.size, (0, 0, 0, 0))
    coat(art, keyline, hexcol(KEYLINE), hexcol(KEYLINE), h)
    coat(art, lining, hexcol(LINE_HI), hexcol(LINE_HI), h)
    coat(art, small, hexcol(BODY_HI), hexcol(BODY_HI), h)
    return trim(art)


def spill():
    """THE LIGHT OUT OF THE CELLAR'S CRACK (2026-08-25).

    The roller slides down a few units under the pointer and what is behind it is lit; this
    is that light landing on the sill it just left. One pixel wide and stretched across the
    roller - there is no horizontal detail in a slit of light, and a wide sprite would only
    be 592 copies of the same column.

    BANDED, not blurred (16 sec.6.10): six flat runs of falling alpha over the same warm
    tungsten the room's own lamps are, so it stays inside the palette rule the same way the
    market's fade does. The top three rows are the slit itself and near solid; everything
    under them is the fall-off down the roller's face.
    """
    rows = [208, 202, 186, 152, 118, 90, 68, 50, 36, 26, 18, 12, 8, 5, 3, 1]
    warm = (255, 206, 138)
    # THE CELLAR IS NOT A LIGHTBOX. Take one was one pixel wide, stretched the roller's whole
    # width at full alpha, and what landed was a cream RULE across the bar - a stripe, not a
    # spill. Light out of a slit is brightest where the room behind it is deepest and dies at
    # the jambs, so this is drawn at the roller's real width with the outer eighth falling
    # off, and its peak is 208 rather than 255: it is lit air, and lit air is never solid.
    w = 592
    img = Image.new('RGBA', (w, len(rows)), (0, 0, 0, 0))
    px = img.load()
    fade = w // 8
    for x in range(w):
        edge = min(x, w - 1 - x)
        k = 1.0 if edge >= fade else (edge / float(fade)) ** 0.7
        for y, a in enumerate(rows):
            px[x, y] = (warm[0], warm[1], warm[2], int(round(a * k)))
    return img


def trim(img):
    box = img.getbbox()
    return img.crop(box) if box else img


# ---------------------------------------------------------------------------

META = '''fileFormatVersion: 2
guid: %s
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 0
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {x: 0.5, y: 0.5}
  spritePixelsToUnits: 100
  spriteBorder: {x: 0, y: 0, z: 0, w: 0}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
'''


def guid_for(name):
    """A stable GUID from the asset's name. Unity writes its own on import when the .meta
    is missing, but shipping one means the sprite lands crisp on a machine where this
    project has never compiled - a Default-filtered PNG is the silent bug the importer
    comment warns about, and Resources/Items is not under the importer's rule."""
    import hashlib
    return hashlib.md5(('lastcall/' + name).encode('utf-8')).hexdigest()


def ship(img, name):
    path = os.path.join(ITEMS, name + '.png')
    img.save(path)
    meta = path + '.meta'
    if not os.path.exists(meta):
        io.open(meta, 'w', encoding='utf-8', newline='\n').write(META % guid_for(name))
    print('  %-22s %dx%d' % (name + '.png', img.size[0], img.size[1]))
    return path


def sheet(word_img, arrow_img, up_img):
    """The sign as it will be READ: on the roller's own grey, with the shut roller's word
    and chevron over the rail's own mark, which is the only thing left showing once the
    cellar is open."""
    scale = 4
    pad = 14
    slat = (0x88, 0x82, 0x8C, 0xFF)
    rows = (word_img, arrow_img, up_img)
    w = max(i.size[0] for i in rows) * scale + pad * 2
    h = sum(i.size[1] for i in rows) * scale + pad * 4
    out = Image.new('RGBA', (w, h), slat)
    for y in range(0, h, 4 * scale // 2):
        ImageDraw.Draw(out).line([(0, y), (w, y)], fill=(0x6E, 0x69, 0x72, 255))

    def blit(img, y):
        big = img.resize((img.size[0] * scale, img.size[1] * scale), Image.NEAREST)
        out.alpha_composite(big, ((w - big.size[0]) // 2, y))
        return y + big.size[1]

    y = blit(word_img, pad)
    y = blit(arrow_img, y + pad // 2)
    blit(up_img, y + pad)
    return out


def contact():
    """Every take side by side on the roller's own grey, so one can be CHOSEN rather than
    argued about. Named, because a take nobody can name cannot be asked for."""
    from PIL import ImageDraw as D
    scale, pad = 4, 16
    made = []
    for name in ('tag', 'fat', 'quick', 'wall'):
        use(name)
        w = word()
        if w.size[1] > 34:
            sys.exit('take %s is %d px tall; the ceiling is 34' % (name, w.size[1]))
        made.append((name, w))
        print('  %-6s %dx%d' % (name, w.size[0], w.size[1]))
    width = max(w.size[0] for _, w in made) * scale + pad * 2
    height = sum(w.size[1] * scale + pad + 14 for _, w in made) + pad
    out = Image.new('RGBA', (width, height), (0x88, 0x82, 0x8C, 255))
    pen = D.Draw(out)
    for y in range(0, height, 12):
        pen.line([(0, y), (width, y)], fill=(0x6E, 0x69, 0x72, 255))
    y = pad
    for name, w in made:
        pen.text((pad, y), name.upper(), fill=(0x24, 0x18, 0x30, 255))
        y += 14
        big = w.resize((w.size[0] * scale, w.size[1] * scale), Image.NEAREST)
        out.alpha_composite(big, ((width - big.size[0]) // 2, y))
        y += big.size[1] + pad
    out.convert('RGB').save(os.path.join(HERE, 'open_sign_takes.png'))
    print('  takes                 Tools/open_sign_takes.png')


def main():
    preview_only = '--preview' in sys.argv
    if '--takes' in sys.argv:
        contact()
        return
    for arg in sys.argv[1:]:
        if not arg.startswith('--'):
            use(arg)
    w = word()
    a = arrow()
    u = arrow(up=True)
    if w.size[1] > 34:
        sys.exit('the writing is %d px tall; the author capped it at 34' % w.size[1])
    print('word  %dx%d   arrow %dx%d   up %dx%d' % (w.size + a.size + u.size))

    sheet(w, a, u).convert('RGB').save(os.path.join(HERE, 'open_sign_preview.png'))
    print('  preview               Tools/open_sign_preview.png')
    if preview_only:
        return
    ship(w, 'sign_open')
    ship(a, 'sign_open_arrow')
    ship(u, 'sign_shut_arrow')
    ship(spill(), 'light_spill')


if __name__ == '__main__':
    main()
