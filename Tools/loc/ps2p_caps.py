# -*- coding: utf-8 -*-
"""Builds the game's heading face with full-height accented capitals (2026-09-14, localization L3).

Press Start 2P squeezes an accented capital into its 8 px cell: the letter drops to lowercase height so
the mark fits, and in a heading "RÉGLAGES" reads "RéGLAGES". The store banners already fix this with
`Tools/steam_kit/i18n.ps2p_glyph` (the full-height capital, the mark lifted above the capital line);
this writes those same drawings back into the font as outlines, one 125-unit square per pixel, so the
game draws them too.

The OFL reserves the name "Press Start 2P" for the original, so the modified face is named Malibu
Arcade. Latin and Cyrillic capitals are redrawn; Greek capitals keep their own glyphs (the tonos sits
beside a Greek capital, not over it, and Greek headings drop it anyway — Core TextCase). A round-trip
guard redraws plain letters from their masks first and refuses to write if they do not come back
identical, so a wrong row mapping can never ship.

It also adds Romanian's comma-below Ș ș (U+0218/0219), which the face lacks although it has Ț ț: without
them a Romanian heading dropped to a system font mid-word. The comma is lifted off Ț ț (the letter
minus its T / t) and set under S / s.

    py -3 -X utf8 Tools/loc/ps2p_caps.py          # writes Assets/Fonts/MalibuArcade-Regular.ttf
"""
import os
import sys
import unicodedata

from fontTools.pens.recordingPen import DecomposingRecordingPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont
from PIL import Image, ImageChops, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.join(ROOT, 'Tools', 'steam_kit'))
import i18n  # noqa: E402

SRC = os.path.join(ROOT, 'Tools', 'steam_kit', 'fonts', 'PressStart2P-Regular.ttf')
OUT = os.path.join(ROOT, 'Assets', 'Fonts', 'MalibuArcade-Regular.ttf')
UNIT = 125                     # font units per pixel (1000 / 8)
TOP_Y = 1000                   # the ascender line: the top edge of mask row i18n.PS2P_TOP
FAMILY = 'Malibu Arcade'


def mask_to_glyph(mask):
    """The lit pixels of an 8 x 12 mask as TrueType outlines: one rectangle per horizontal run."""
    pen = TTGlyphPen(None)
    w, h = mask.size
    px = mask.load()
    drew = False
    for r in range(h):
        y_top = TOP_Y - (r - i18n.PS2P_TOP) * UNIT
        y_bot = y_top - UNIT
        c = 0
        while c < w:
            if px[c, r] < 128:
                c += 1
                continue
            start = c
            while c < w and px[c, r] >= 128:
                c += 1
            x0, x1 = start * UNIT, c * UNIT
            pen.moveTo((x0, y_top))     # clockwise in y-up: an outer TrueType contour
            pen.lineTo((x1, y_top))
            pen.lineTo((x1, y_bot))
            pen.lineTo((x0, y_bot))
            pen.closePath()
            drew = True
    return pen.glyph() if drew else None


def pixels_of(font, name):
    """The set of (column, row) pixels a glyph covers, read from its outline, for the guard."""
    glyf = font['glyf']
    g = glyf[name]
    out = set()
    if g.numberOfContours == 0:
        return out
    rec = DecomposingRecordingPen(font.getGlyphSet())
    font.getGlyphSet()[name].draw(rec)
    from fontTools.pens.pointInsidePen import PointInsidePen
    for r in range(i18n.PS2P_ROWS):
        cy = TOP_Y - (r - i18n.PS2P_TOP) * UNIT - UNIT / 2
        for col in range(i18n.PS2P_CELL + 1):
            cx = col * UNIT + UNIT / 2
            pip = PointInsidePen(font.getGlyphSet(), (cx, cy))
            rec.replay(pip)
            if pip.getResult():
                out.add((col, r))
    return out


def main():
    font = TTFont(SRC)
    cmap = font.getBestCmap()
    glyf = font['glyf']
    pil = ImageFont.truetype(SRC, i18n.PS2P_CELL)

    # Guard: plain letters redrawn from their masks must cover exactly the pixels the original covers.
    for ch in 'HOMWgjy0?Ş':
        name = cmap[ord(ch)]
        mask = i18n.ps2p_glyph(ch, pil)
        want = {(c, r) for r in range(mask.size[1]) for c in range(mask.size[0]) if mask.getpixel((c, r)) >= 128}
        have = pixels_of(font, name)
        if want != have:
            sys.exit('guard failed on %r: mask and outline differ by %d pixels — row mapping is wrong'
                     % (ch, len(want ^ have)))

    changed = []
    for code, name in sorted(cmap.items()):
        ch = chr(code)
        if not ch.isupper() or 0x370 <= code <= 0x3FF:
            continue
        nfd = unicodedata.normalize('NFD', ch)
        if len(nfd) == 1:
            continue
        rebuilt = i18n.ps2p_glyph(ch, pil)
        direct = Image.new('L', (i18n.PS2P_CELL, i18n.PS2P_ROWS), 0)
        ImageDraw.Draw(direct).text((0, i18n.PS2P_TOP), ch, font=pil, fill=255)
        direct = direct.point(lambda v: 255 if v >= 128 else 0)
        if rebuilt.tobytes() == direct.tobytes():
            continue                      # the face already draws it right (marks below: Ç Ş Ę)
        g = mask_to_glyph(rebuilt)
        if g is None:
            continue
        glyf[name] = g
        g.recalcBounds(glyf)
        adv, _ = font['hmtx'][name]
        font['hmtx'][name] = (adv, g.xMin)
        changed.append(ch)

    # Romanian's comma-below S: the comma comes off the face's own Ț / ț, centred under S / s.
    added = []
    for new_code, base_ch, donor_ch, donor_base in ((0x218, 'S', 'Ț', 'T'), (0x219, 's', 'ț', 't')):
        if new_code in cmap or ord(donor_ch) not in cmap:
            continue
        body = i18n.ps2p_glyph(base_ch, pil)
        donor_body = i18n.ps2p_glyph(donor_base, pil)
        comma = ImageChops.subtract(i18n.ps2p_glyph(donor_ch, pil), donor_body)
        if comma.getbbox() is None or body.getbbox() is None:
            continue
        bb, bd = body.getbbox(), donor_body.getbbox()
        shifted = Image.new('L', comma.size, 0)
        shifted.paste(comma, (((bb[0] + bb[2]) - (bd[0] + bd[2])) // 2, 0))
        mask = ImageChops.lighter(body, shifted)
        glyph = mask_to_glyph(mask)
        name = 'uni%04X' % new_code
        order = font.getGlyphOrder()
        if name not in order:
            order = order + [name]
            font.setGlyphOrder(order)
            glyf.setGlyphOrder(order)
        glyf[name] = glyph
        glyph.recalcBounds(glyf)
        font['hmtx'][name] = (font['hmtx'][cmap[ord(base_ch)]][0], glyph.xMin)
        for sub in font['cmap'].tables:
            if sub.isUnicode():
                sub.cmap[new_code] = name
        want = {(c, r) for r in range(mask.size[1]) for c in range(mask.size[0]) if mask.getpixel((c, r)) >= 128}
        if pixels_of(font, name) != want:
            sys.exit('guard failed on added U+%04X: outline and mask differ' % new_code)
        added.append(chr(new_code))

    names = font['name']
    for rec in list(names.names):
        if rec.nameID in (1, 16):
            names.setName(FAMILY, rec.nameID, rec.platformID, rec.platEncID, rec.langID)
        elif rec.nameID == 3:
            names.setName('MalibuArcade-Regular;' + str(rec), rec.nameID, rec.platformID, rec.platEncID, rec.langID)
        elif rec.nameID == 4:
            names.setName(FAMILY + ' Regular', rec.nameID, rec.platformID, rec.platEncID, rec.langID)
        elif rec.nameID == 6:
            names.setName('MalibuArcade-Regular', rec.nameID, rec.platformID, rec.platEncID, rec.langID)
    names.setName('Modified from Press Start 2P for Malibu Club: accented Latin and Cyrillic capitals redrawn '
                  'at full height with their marks above the capital line. Not the original Press Start 2P.',
                  10, 3, 1, 0x409)
    font['head'].yMax = max(font['head'].yMax, TOP_Y + i18n.PS2P_TOP * UNIT)
    out = sys.argv[sys.argv.index('--out') + 1] if '--out' in sys.argv else OUT
    font.save(out)
    print('wrote %s: %d capitals redrawn, %d letters added (%s)' % (out, len(changed), len(added), ''.join(added)))
    print(''.join(changed))


if __name__ == '__main__':
    main()
