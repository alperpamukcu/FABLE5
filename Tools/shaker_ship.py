# -*- coding: utf-8 -*-
"""The author's own shaker, into the game (2026-09-06).

The tin was generated art (`shaker_gen.py`, `shaker_cap_open.py`) until the author drew
one — TWO, a steel and a gold — and the gold is an upgrade the bar buys. This lands them
on the sheets the two benches already work, so no scene code has to move:

    Items/shaker[_t2].png          the whole thing, capped        (ItemArt.Shaker, the gauge)
    Items/tin_open[_t2].png        the tin alone, open            (both benches' body)
    Items/shaker_cap[_t2].png      the lid, seated                (the bench's cap prop)
    Items/shaker_cap_pour[_t2].png the lid with its tip off       (the serve bench, pouring)
    Items/shaker_prop[_t2].png     the little one on the counter  (the main scene)

THE SHEETS ARE ALIGNED TO EACH OTHER AND THAT IS THE WHOLE CONTRACT (VesselArt's own
note): the bench draws the tin and the cap into two rects of the same size, so the lid
lands on the tin's neck only because both drawings sit at the same place on a shared
116x208 canvas. The author drew all four plates of a tier on ONE 208x208 canvas, which
means the alignment is already true — this only crops the same 116-wide window out of
each, and never moves a drawing inside it.

TWO PLATES ARE DERIVED, NOT REDRAWN (the house's own law, memory `open-states-derive`):

  * the SEATED LID is the cap file the author drew separately, and where it goes is
    MEASURED — slid over the whole shaker until every one of its pixels agrees with it.
    It seats at (61,14) on their canvas, 3883 of 3883 matching, so the seam is theirs.
  * the POURING LID is the TIPLESS drawing's own lid box — the same rows the seated lid
    occupies — which is the dome with its tip lifted and its strainer showing. The four
    plates are separate drawings rather than layers (measured: the open tin's body is not
    the closed one's), so nothing is subtracted from anything; the lid box is cut where
    the author's own cap ends, and the serve bench draws it over the tin it belongs to.

    py -3 Tools/shaker_ship.py            measure, ship, and write the contact sheet
    py -3 Tools/shaker_ship.py --check    measure and report, write nothing
"""
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SRC = os.path.join(HERE, 'AssetPipeline', 'sources', 'shaker')
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Items')

# The sheet both benches draw into: 116x208, which is the 200x358 rect's own aspect, so a
# plate fills its rect edge to edge under preserveAspect. Unchanged since 2026-07-23.
SHEET = (116, 208)
INK = 16                      # a pixel counts as drawn above this alpha


def load(name):
    return Image.open(os.path.join(SRC, name)).convert('RGBA')


def bbox(im):
    return im.getbbox()


def window(im):
    """The 116-wide window out of the author's 208 canvas, centred on the drawing.

    Centred on the WHOLE shaker's own centre for every plate of a tier — one window, so
    the four plates stay in step with each other exactly as the author drew them.
    """
    return im.crop(WINDOW)


def best_offset(cap, whole, tin):
    """Where the separately drawn cap sits on the tier's canvas.

    Measured, not assumed: the cap is slid over the whole shaker and scored on how many of
    its own pixels land on the same colour. The right seat is the one that matches — a
    drawing laid back where it was cut from agrees with itself everywhere, and this one
    does: 3883 of 3883.
    """
    top, left = bbox(whole)[1], bbox(whole)[0]
    best, best_score = None, -1
    pw, pc = whole.load(), cap.load()
    for dy in range(top - 6, top + 7):
        for dx in range(left - 6, left + 7):
            score = 0
            for y in range(cap.height):
                for x in range(cap.width):
                    q = pc[x, y]
                    if q[3] < INK:
                        continue
                    tx, ty = dx + x, dy + y
                    if 0 <= tx < whole.width and 0 <= ty < whole.height and pw[tx, ty] == q:
                        score += 1
            if score > best_score:
                best, best_score = (dx, dy), score
    return best, best_score


def lid_box(im, bottom):
    """A lid's own box out of a full drawing: every row above the seated cap's last one.

    The dome's skirt reaches down over the tin's mouth, so the box is cut by ROW rather
    than by silhouette — and it is safe to keep a few of the tin's own shoulder pixels
    with it, because the bench draws this over that same tin in that same rect.
    """
    out = Image.new('RGBA', im.size, (0, 0, 0, 0))
    out.alpha_composite(im.crop((0, 0, im.width, bottom)), (0, 0))
    return out


def on_sheet(im, at=None):
    """A drawing placed on the shared 116x208 sheet."""
    sheet = Image.new('RGBA', SHEET, (0, 0, 0, 0))
    if at is None:
        sheet.alpha_composite(window(im), (0, 0))
    else:
        sheet.alpha_composite(im, at)
    return sheet


def count(im):
    return sum(1 for p in im.getdata() if p[3] >= INK)


TIERS = [('silver', ''), ('gold', '_t2')]
WINDOW = None


def main():
    global WINDOW
    check = '--check' in sys.argv
    report = []
    plates = {}
    for tier, suffix in TIERS:
        whole, tipless = load(tier + '_whole.png'), load(tier + '_tipless.png')
        tin, cap = load(tier + '_tin.png'), load(tier + '_cap.png')
        b = bbox(whole)
        centre = (b[0] + b[2]) // 2
        WINDOW = (centre - SHEET[0] // 2, 0, centre + SHEET[0] // 2, whole.height)

        # THE SEATED LID, seated where it was drawn — proved, not placed.
        (cx, cy), score = best_offset(cap, whole, tin)
        seated = Image.new('RGBA', whole.size, (0, 0, 0, 0))
        seated.alpha_composite(cap, (cx, cy))
        report.append('%-6s cap seats at (%d,%d): %d of %d of its pixels are the whole '
                      'drawing itself' % (tier, cx, cy, score, count(cap)))

        # THE POURING LID: the tipless drawing's own lid box, cut where the seated cap ends.
        pour = lid_box(tipless, cy + cap.height)
        report.append('%-6s pouring lid: rows 0-%d of the tipless drawing, %d px (seated lid %d)'
                      % (tier, cy + cap.height - 1, count(pour), count(seated)))

        plates['shaker' + suffix] = on_sheet(whole)
        plates['tin_open' + suffix] = on_sheet(tin)
        plates['shaker_cap' + suffix] = on_sheet(seated)
        plates['shaker_cap_pour' + suffix] = on_sheet(pour)
        # The little one that stands on the counter's coaster: the author's own 48x48
        # drawing, untouched — the main scene draws it at two units a pixel like the book.
        plates['shaker_prop' + suffix] = load(tier + '_small.png')

    for line in report:
        print(' ', line)
    for name, im in sorted(plates.items()):
        b = bbox(im)
        print('  %-22s %-9s bbox=%-20s px=%d' % (name, im.size, b, count(im)))
    if check:
        print('  --check: nothing written')
        return

    shipped = [n for n in plates if not n.startswith(('CAPPED', 'POURING'))]
    for name in shipped:
        plates[name].save(os.path.join(OUT, name + '.png'))
    print('  wrote %d plates to Assets/Resources/Items' % len(shipped))

    # The contact sheet: every plate at 1x on the counter's own dark, the two shared-canvas
    # families overlaid so the seam can be SEEN rather than trusted.
    # ...and the two COMPOSITES the benches actually draw, so the seam is looked at rather
    # than trusted: the tin with its lid seated, and the tin pouring with the tip off.
    for _, s in TIERS:
        for lid, name in (('shaker_cap' + s, 'CAPPED' + s), ('shaker_cap_pour' + s, 'POURING' + s)):
            comp = plates['tin_open' + s].copy()
            comp.alpha_composite(plates[lid])
            plates[name] = comp
    order = ['shaker', 'tin_open', 'shaker_cap', 'CAPPED', 'shaker_cap_pour', 'POURING', 'shaker_prop']
    cols = [n + s for _, s in TIERS for n in order]
    W = sum(plates[n].width + 14 for n in cols) + 14
    H = max(plates[n].height for n in cols) + 40
    sheet = Image.new('RGBA', (W, H), (0x1F, 0x19, 0x24, 255))
    d = ImageDraw.Draw(sheet)
    x = 14
    for n in cols:
        im = plates[n]
        sheet.alpha_composite(im, (x, H - 14 - im.height))
        d.text((x, 6), n, fill=(0xC9, 0xBC, 0xA8, 255))
        x += im.width + 14
    sheet.save(os.path.join(HERE, 'shaker_ship_preview.png'))
    print('  preview: Tools/shaker_ship_preview.png')


if __name__ == '__main__':
    main()
