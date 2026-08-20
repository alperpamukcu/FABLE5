# -*- coding: utf-8 -*-
"""Cut the generated button out, measure its nine-slice, and prove it stretches.

The proof is the point. A button that has to fit its label is not finished when the art
looks right - it is finished when the SAME pixels draw "OK" and "MAKE A DRINK" without a
corner smearing. So this measures the border with the house's own rule (market_borders.py:
a slice may only stretch a run that is uniform ALONG the edge it lies on) and then renders
one sprite at six widths so the failure, if there is one, is visible rather than argued.

What comes out:
    ui_btn.png              the button, cropped to its own pixels
    _ui_btn_slice.png       the same sprite drawn at six widths, with the border marked
and a border in Unity's Vector4 order (left, bottom, right, top) to paste into
PatronArtPostprocessor, which is where every other sliced frame in this project gets its.
"""
import io, os, sys
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'scene_cast_raw')
SHEET = os.path.join(RAW, 'ui_sheet_0.png')
BTN = os.path.join(RAW, 'ui_btn.png')


def luma(c):
    return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]


def border_of(im):
    """Verbatim from Tools/market_borders.py - the same rule every other frame was cut by."""
    W, H = im.size
    px = im.load()

    def flat(line):
        vals = [luma(c[:3]) for c in line if c[3] > 20]
        if len(vals) < max(4, len(line) // 3):
            return False
        return max(vals) - min(vals) <= 4

    def scan(line_at, depth, span):
        lo, hi = int(span * 0.1), int(span * 0.9)
        for i in range(depth):
            if flat([line_at(i, j) for j in range(lo, hi)]):
                return max(1, i)
        return max(1, depth // 3)

    left = scan(lambda i, j: px[i, j], W // 2, H)
    right = scan(lambda i, j: px[W - 1 - i, j], W // 2, H)
    bottom = scan(lambda i, j: px[j, H - 1 - i], H // 2, W)
    top = scan(lambda i, j: px[j, i], H // 2, W)
    return left, bottom, right, top


def corner_radius(im):
    """How far in the ROUNDED CORNER reaches, per side.

    market_borders.py's rule alone is not enough for this button, and finding out why is
    worth writing down. That scan reads the middle 80% of each edge line and stops at the
    first flat one - which is correct for a frame whose decoration is a band running along
    the edge, and blind to a frame whose decoration is at the CORNERS. This button's edges
    are one flat magenta line, so it measured a border of 1,1,5,1 and would have stretched
    the rounded corners into smears.

    So the corner is measured directly: walk in from each side until the silhouette stops
    curving - the first column whose topmost opaque pixel is already at the shape's own
    minimum. The border is then whichever of the two rules is LARGER, because a nine-slice
    has to protect everything that is not a repeat of its neighbour.
    """
    px = im.load()
    W, H = im.size

    def first_opaque(fixed, along, horizontal):
        rng = range(H) if horizontal else range(W)
        for k in rng:
            x, y = (fixed, k) if horizontal else (k, fixed)
            if px[x, y][3] > 20:
                return k
        return None

    def run(count, probe):
        vals = [probe(i) for i in range(count)]
        vals = [v for v in vals if v is not None]
        if not vals:
            return 1
        best = min(vals)
        for i, v in enumerate(vals):
            if v == best:
                return max(1, i)
        return max(1, count // 3)

    left = run(W // 2, lambda i: first_opaque(i, None, True))
    right = run(W // 2, lambda i: first_opaque(W - 1 - i, None, True))
    top = run(H // 2, lambda i: first_opaque(i, None, False))
    bottom = run(H // 2, lambda i: first_opaque(H - 1 - i, None, False))
    return left, bottom, right, top


def nine_slice(src, l, b, r, t, w, h):
    """Draw src at w x h the way Unity's Image.Type.Sliced does.

    Corners are pasted at 1:1 and never touched. Edges stretch along one axis only. The
    centre stretches both ways. Written out rather than approximated with a plain resize,
    because a plain resize is exactly the bug this file exists to rule out.
    """
    W, H = src.size
    cw, ch = W - l - r, H - t - b
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    mw, mh = max(1, w - l - r), max(1, h - t - b)

    def put(box, dst, size):
        piece = src.crop(box)
        if piece.size != size:
            piece = piece.resize(size, Image.NEAREST)
        out.paste(piece, dst, piece)

    put((0, 0, l, t), (0, 0), (l, t))                       # corners
    put((W - r, 0, W, t), (w - r, 0), (r, t))
    put((0, H - b, l, H), (0, h - b), (l, b))
    put((W - r, H - b, W, H), (w - r, h - b), (r, b))
    put((l, 0, W - r, t), (l, 0), (mw, t))                  # edges
    put((l, H - b, W - r, H), (l, h - b), (mw, b))
    put((0, t, l, H - b), (0, t), (l, mh))
    put((W - r, t, W, H - b), (w - r, t), (r, mh))
    put((l, t, W - r, H - b), (l, t), (mw, mh))             # centre
    return out


def main():
    if not os.path.exists(SHEET):
        sys.exit('run ui_buttons_gen.py fetch first')
    sheet = Image.open(SHEET).convert('RGBA')
    btn = sheet.crop(sheet.getbbox())
    btn.save(BTN)
    edge = border_of(btn)
    corner = corner_radius(btn)
    # Whichever rule asks for more. See corner_radius() for why one rule is not enough.
    l, b, r, t = (max(a, c) + 1 for a, c in zip(edge, corner))
    print('button %dx%d' % btn.size)
    print('  edge rule  (market_borders): %d, %d, %d, %d' % edge)
    print('  corner rule               : %d, %d, %d, %d' % corner)
    print('nine-slice border (Unity Vector4 l,b,r,t): %d, %d, %d, %d' % (l, b, r, t))
    print('  stretchable centre: %d x %d px' % (btn.width - l - r, btn.height - t - b))

    # The proof sheet: one sprite, six widths, real labels off the game's own screens.
    labels = ['OK', 'SERVE', 'BUY', 'MENU - MAKE A DRINK', 'POUR A PINT', 'LAST CALL']
    widths = [64, 110, 150, 300, 220, 180]
    pad, gap = 24, 16
    scale = 2
    rowh = btn.height * scale + gap
    out = Image.new('RGBA', (pad * 2 + 300 * scale,
                             pad * 2 + len(labels) * rowh + 30), (26, 22, 30, 255))
    d = ImageDraw.Draw(out)
    d.text((pad, 8), 'ONE sprite, %dx%d, border %d/%d/%d/%d - drawn at six widths'
           % (btn.width, btn.height, l, b, r, t), fill=(238, 230, 226, 255))
    for i, (lab, w) in enumerate(zip(labels, widths)):
        y = pad + 22 + i * rowh
        big = nine_slice(btn, l, b, r, t, w, btn.height)
        big = big.resize((big.width * scale, big.height * scale), Image.NEAREST)
        out.alpha_composite(big, (pad, y))
        d.text((pad + big.width + 12, y + big.height // 2 - 6),
               '%-22s %3d px' % ('"' + lab + '"', w), fill=(200, 190, 200, 255))
    out.convert('RGB').save(os.path.join(RAW, '_ui_btn_slice.png'))
    print('wrote _ui_btn_slice.png')


if __name__ == '__main__':
    main()
