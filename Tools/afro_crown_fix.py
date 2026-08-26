# -*- coding: utf-8 -*-
"""GIVE THE AFRO ITS CROWN BACK (2026-08-26, the author: "gorseldeki kadinin kafasi
kesiliyor" — with a screenshot of Simone Baptiste on her stool, the top of her hair
sliced flat).

WHAT IS ACTUALLY WRONG. Every clip of the `afrowoman` cast member is drawn on the
rig's 220x220 canvas with her hair flush against ROW 0, twenty-five pixels wide and
still WIDENING as it goes down — she was generated too tall for the canvas and the
crown of the afro was clipped off at the top of the image, in the source PNGs, before
Unity ever saw them. No amount of placing fixes that: the pixels do not exist.

WHAT THIS DOES. Two measured moves, applied to all 8 clips so the animation stays in
register:

  1. Every frame slides DOWN by SHIFT rows inside its own canvas. There is room: the
     lowest ink in the whole set is row 211, so the feet land at 218 of 220. It costs
     her SHIFT stage units of height in the room — about 3%, well inside the cast's
     own spread — and it is the only way to buy sky above her head.

  2. The dome is rebuilt over the flat cut, and its curve is MEASURED off her own
     hair rather than invented. The silhouette is a circle: it runs 25 px wide at the
     cut and 36 px nine rows below, so the half-width grows 0.61 px a row, which for a
     circle means the centre sits h = w·0.61 below the cut and R = (w² + SHIFT²)/(2·SHIFT).
     Solved on the real numbers that comes out at R ≈ 14.7 and a crown exactly SHIFT
     rows above the cut — which is why SHIFT is 7 and not a taste. Each new row is
     filled by sampling the hair a few rows further in, so the cap carries the same
     speckle the rest of the afro does instead of reading as a helmet.

Frames whose hair does NOT reach the top (the walk and drink bobs dip below it) are
shifted and left alone: there is nothing cut to rebuild.

Her licence photo is re-cut from the repaired idle frame afterwards, through
patron_gen.make_face — the same crop the ship script makes, so the portrait can never
drift from the body it belongs to.

    py -3 Tools/afro_crown_fix.py            # report only
    py -3 Tools/afro_crown_fix.py apply
"""
import math
import os
import sys

from PIL import Image, PngImagePlugin

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SLUG = 'afrowoman'
CAST = os.path.join(ROOT, 'Assets', 'Resources', 'Patron', SLUG)

SHIFT = 7          # rows down, and the height of the rebuilt cap
CUT_RUN = 8        # a row-0 run at least this wide is a CUT, not a stray pixel

# THIS PASS IS NOT IDEMPOTENT AND CANNOT BE MADE SO. A second run would shift her down
# another seven rows and build a second cap on top of the first, because after the first
# run row 0 is legitimately opaque again. So every repaired frame is STAMPED, and a
# stamped frame is refused — the same rule the sprite pipeline learned the hard way when
# uniform_outline stacked ink on ink (memory: sprite-pipeline-idempotence).
MARK = 'lastcall_crown'


def stamped(im):
    return (im.info or {}).get(MARK) == str(SHIFT)


def stamp():
    meta = PngImagePlugin.PngInfo()
    meta.add_text(MARK, str(SHIFT))
    return meta


def opaque_span(px, w, y):
    xs = [x for x in range(w) if px[x, y][3] > 8]
    return (min(xs), max(xs)) if xs else None


def rebuild(im):
    """Slide the figure down and, if it was cut, put the crown back on."""
    w, h = im.size
    src = im.load()
    cut = opaque_span(src, w, 0)
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    out.paste(im, (0, SHIFT), im)
    if cut is None or (cut[1] - cut[0] + 1) < CUT_RUN:
        return out, False

    px = out.load()
    x0, x1 = cut[0], cut[1]
    cx = (x0 + x1) / 2.0
    half = (x1 - x0 + 1) / 2.0
    # The circle the hair is already drawing (see the header): its centre sits h below
    # the cut line, and the cut line is now row SHIFT.
    R = (half * half + SHIFT * SHIFT) / (2.0 * SHIFT)
    centre = R - SHIFT                       # rows below the cut
    for dy in range(1, SHIFT + 1):
        y = SHIFT - dy
        span = R * R - (centre + dy) * (centre + dy)
        if span <= 0:
            continue
        wide = math.sqrt(span)
        lo, hi = int(round(cx - wide)), int(round(cx + wide))
        for x in range(max(0, lo), min(w - 1, hi) + 1):
            # Sample the hair a little way in, cycling three rows so the cap carries
            # the afro's own speckle rather than one flat tone.
            for probe in (SHIFT + 1 + (dy % 3), SHIFT + 1, SHIFT + 2, SHIFT + 3):
                c = px[x, probe] if probe < h else (0, 0, 0, 0)
                if c[3] > 8:
                    px[x, y] = c
                    break
            else:
                px[x, y] = px[int(round(cx)), SHIFT + 1]
    return out, True


def main(apply):
    clips = sorted(d for d in os.listdir(CAST) if os.path.isdir(os.path.join(CAST, d)))
    touched = capped = skipped = 0
    idle0 = None
    for clip in clips:
        d = os.path.join(CAST, clip)
        for name in sorted(os.listdir(d)):
            if not name.endswith('.png'):
                continue
            p = os.path.join(d, name)
            opened = Image.open(p)
            if stamped(opened):
                skipped += 1
                if clip == 'idle' and name.endswith('_00.png'):
                    idle0 = opened.convert('RGBA')
                continue
            im = opened.convert('RGBA')
            fixed, did = rebuild(im)
            touched += 1
            capped += 1 if did else 0
            bb = fixed.getbbox()
            assert bb[3] <= fixed.height, '%s: the feet fell off the canvas' % name
            if apply:
                fixed.save(p, pnginfo=stamp())
            if clip == 'idle' and name.endswith('_00.png'):
                idle0 = fixed
    print('%d frames shifted %d rows, %d of them re-crowned, %d already stamped'
          % (touched, SHIFT, capped, skipped))
    if apply and idle0 is not None:
        sys.path.insert(0, HERE)
        import patron_gen
        patron_gen.make_face(SLUG, idle0)


if __name__ == '__main__':
    main(len(sys.argv) > 1 and sys.argv[1] == 'apply')
