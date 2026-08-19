# -*- coding: utf-8 -*-
"""The top strip, fourth cut - designed by hand end to end (2026-08-19, the author:
"Oluşturulan takvim görseli bozuk duruyor, elinden geldiğince kendin tasarımını yap.
Profesyonel bir UI/UX designer gibi düşün ve üst barın tasarımını tekrardan ele al.").

The design decisions this mock exists to judge:

  - WELLS, not cases: the two instruments are recessed INTO the beam (shadowed top
    edge, lit bottom lip, chamfered corners), not boxes standing on it. A recess
    says "machined into the console"; a box says "widget".
  - ONE DISPLAY LANGUAGE: the calendar drops its lamp row. The seven day names sit
    on the same dark glass the clock's digits sit on, and TONIGHT'S NAME IS LIT the
    way a digit is lit - amber, with a miniature neon tube burning under it (the
    beam's own foot light, miniaturized). Saturday carries the magenta star fitting
    under its name instead; Sunday carries the shutter. Spent nights go dim glass,
    nights ahead read cream.
  - THE GRID: 16-unit outer margins, both wells 40 tall centred on the beam's
    centre line, glass floors 32, day pitch 52, star row and gear on the same line.

    python Tools/topbar_mock2.py [out.png]
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

import clock_digits as cd

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
FONTS = os.path.join(ROOT, 'Assets', 'Fonts')

W, H = 1280, 54
CY = 27                                   # the beam's centre line


def hx(s):
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


NIGHT = [hx(c) for c in ('0D0813', '1A1023', '241830', '362447', '4A3160')]
AMBER = [hx(c) for c in ('4A2E14', '8F5A1E', 'C9822B', 'E8A33D', 'F5C97B')]
CYAN = [hx(c) for c in ('123B45', '1B5F66', '26918F', '3BC8BE', '7DF0E3')]
CREAM = [hx(c) for c in ('453E38', '6E6459', '9C8F80', 'C9BCA8', 'F2E8D5')]
MAGENTA = [hx(c) for c in ('5C1B45', '8F2464', 'C23283', 'E84DA6', 'FF7DC6')]
GLASS = (8, 14, 19, 255)


def body(px):
    return ImageFont.truetype(os.path.join(FONTS, 'Silkscreen-Regular.ttf'), px)


def display(px):
    return ImageFont.truetype(os.path.join(FONTS, 'PressStart2P-Regular.ttf'), px)


def text(im, xy, s, font, fill, anchor='mm'):
    ImageDraw.Draw(im).text(xy, s, font=font, fill=fill, anchor=anchor)


def well(im, x, y, w, h):
    """A recess routed into the beam: the glass floor, a shadow gathering under its
    top edge, a lit lip along its bottom, chamfered corners. Light is from above -
    a recess's TOP edge is the dark one, which is exactly backwards from a box, and
    is the whole difference between machined-in and stuck-on."""
    d = ImageDraw.Draw(im)
    ch = 2                                                   # chamfer
    # floor
    d.rectangle((x + 1, y + 1, x + w - 2, y + h - 2), fill=GLASS)
    # corners cut: paint the beam back over the corner pixels
    for cx_, cy_ in ((x, y), (x + w - ch, y), (x, y + h - ch), (x + w - ch, y + h - ch)):
        d.rectangle((cx_, cy_, cx_ + ch - 1, cy_ + ch - 1), fill=NIGHT[1])
    # the cut edge: dark across the top and down the sides
    d.line((x + ch, y, x + w - 1 - ch, y), fill=NIGHT[0])
    d.line((x, y + ch, x, y + h - 1 - ch), fill=NIGHT[0])
    d.line((x + w - 1, y + ch, x + w - 1, y + h - 1 - ch), fill=NIGHT[0])
    # the shadow the beam throws onto its own floor
    d.line((x + 1 + ch, y + 1, x + w - 2 - ch, y + 1), fill=(4, 7, 10, 255))
    d.line((x + 1, y + 2, x + w - 2, y + 2), fill=(6, 10, 14, 255))
    # the lit lip at the bottom of the cut
    d.line((x + ch, y + h - 1, x + w - 1 - ch, y + h - 1), fill=NIGHT[3])
    # chamfer diagonals
    d.point((x + 1, y + 1), fill=NIGHT[0])
    d.point((x + w - 2, y + 1), fill=NIGHT[0])
    d.point((x + 1, y + h - 2), fill=NIGHT[3])
    d.point((x + w - 2, y + h - 2), fill=NIGHT[3])


def star_mark(d, cx, cy, c):
    """ChromeArt.Mark('star'), close enough for the mock: 11px star."""
    pts = [(cx, cy - 5), (cx + 2, cy - 2), (cx + 5, cy - 2), (cx + 3, cy + 1),
           (cx + 4, cy + 5), (cx, cy + 2), (cx - 4, cy + 5), (cx - 3, cy + 1),
           (cx - 5, cy - 2), (cx - 2, cy - 2)]
    d.polygon(pts, fill=c)


def glow_band(im, cx, cy, w, hue):
    """Banded halo behind a lit thing - LampGlow's law, stretched wide."""
    for rw, rh, a in ((w // 2 + 10, 9, 12), (w // 2 + 5, 7, 30), (w // 2, 5, 60)):
        o = Image.new('RGBA', im.size, (0, 0, 0, 0))
        ImageDraw.Draw(o).ellipse((cx - rw, cy - rh, cx + rw, cy + rh),
                                  fill=hue[:3] + (a,))
        im.alpha_composite(o)


def compose(out=None, tonight=0, rating=3.4, hhmm='18:40', week='01'):
    im = Image.new('RGBA', (W, H), NIGHT[1])
    d = ImageDraw.Draw(im)
    # ── the beam ────────────────────────────────────────────────────────────
    d.rectangle((0, 0, W, 2), fill=NIGHT[3])
    d.line((0, 3, W, 3), fill=(0, 0, 0, 115))
    d.rectangle((0, H - 3, W, H - 1), fill=NIGHT[0])
    d.rectangle((0, H - 2, W, H - 1), fill=AMBER[4])

    # ── the hour: one well, the digits on its floor ─────────────────────────
    well(im, 16, CY - 20, 134, 40)
    digits = cd.readout(hhmm)
    digits = digits.resize((digits.width * 2, digits.height * 2), Image.NEAREST)
    im.alpha_composite(digits, (16 + (134 - digits.width) // 2, CY - digits.height // 2))

    # ── the week: one well, the days lit ON the glass ───────────────────────
    days = ('MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT', 'SUN')
    head_w, pitch = 64, 52
    glass_w = 8 + head_w + 8 + len(days) * pitch + 8
    wx = 640 - (glass_w + 2) // 2
    well(im, wx, CY - 20, glass_w + 2, 40)

    hx_ = wx + 8 + head_w // 2
    text(im, (hx_, CY - 7), 'WEEK', body(8), CREAM[3])
    text(im, (hx_, CY + 7), week, display(16), CYAN[3])
    rx = wx + 8 + head_w + 4
    d.line((rx, CY - 11, rx, CY + 11), fill=CYAN[4][:3] + (56,))

    for i, day in enumerate(days):
        cx = wx + 16 + head_w + i * pitch + pitch // 2
        closed = i == 6
        lit = i == tonight
        worked = not closed and i < tonight
        sat = i == 5
        # the word, upper row of the slot
        col = NIGHT[4] if closed else AMBER[4] if lit else \
            NIGHT[4] if worked else CREAM[3]
        text(im, (cx, CY - 5), day, body(16), col)
        # the sign under it
        if closed:
            for s_ in range(2):
                d.rectangle((cx - 10, CY + 7 + s_ * 4, cx + 10, CY + 8 + s_ * 4),
                            fill=NIGHT[3])
        elif sat:
            c = MAGENTA[4] if lit else MAGENTA[4][:3] + (150,)
            o = Image.new('RGBA', im.size, (0, 0, 0, 0))
            star_mark(ImageDraw.Draw(o), cx, CY + 10, c)
            im.alpha_composite(o)
        elif lit:
            d.rectangle((cx - 12, CY + 8, cx + 12, CY + 9), fill=AMBER[4])
            d.line((cx - 12, CY + 10, cx + 12, CY + 10), fill=AMBER[2][:3] + (90,))

    # ── the standing: free on the beam ──────────────────────────────────────
    star = Image.open(os.path.join(HERE, 'topbar_raw', 'star_final.png'))
    sock = Image.open(os.path.join(HERE, 'topbar_raw', 'star_socket.png'))
    sx = 1222 - 170
    row = Image.new('RGBA', (170, 32), (0, 0, 0, 0))
    full = Image.new('RGBA', (170, 32), (0, 0, 0, 0))
    for i in range(5):
        row.alpha_composite(sock, (i * 34 + 1, 0))
        full.alpha_composite(star, (i * 34 + 1, 0))
    cut = int(rating / 5.0 * 170)
    row.paste(full.crop((0, 0, cut, 32)), (0, 0), full.crop((0, 0, cut, 32)))
    im.alpha_composite(row, (sx, CY + 5 - 16))
    text(im, (sx + 85, CY - 18), 'TONIGHT · REGULARS', body(8), CREAM[3])

    # ── the key ─────────────────────────────────────────────────────────────
    kd = ImageDraw.Draw(im)
    kd.rectangle((1238, CY - 13, 1263, CY + 12), fill=NIGHT[2])
    kd.line((1238, CY - 13, 1263, CY - 13), fill=NIGHT[3])
    kd.line((1238, CY + 12, 1263, CY + 12), fill=(0, 0, 0, 140))
    text(im, (1251, CY), '*', body(16), CREAM[4])

    out = out or os.path.join(HERE, 'topbar_raw', 'strip_v4.png')
    im.save(out)
    im.resize((W * 2, H * 2), Image.NEAREST).save(out.replace('.png', '_2x.png'))
    print('mock ->', out)


if __name__ == '__main__':
    compose(*(sys.argv[1:] or ()))
