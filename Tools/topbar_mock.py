# -*- coding: utf-8 -*-
"""The top strip, composited in Python at 1:1 canvas units - the drafting table for
the 2026-08-19 redesign. Uses the game's own fonts, the clock's own digit masks
(clock_digits) and the generated plate/star takes, so a layout can be JUDGED before
Unity compiles it. It is a mock, not a renderer: no lights, no animation.

    python Tools/topbar_mock.py [plate2_c] [out.png]
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

import clock_digits as cd

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
FONTS = os.path.join(ROOT, 'Assets', 'Fonts')

W, H = 1280, 54


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


def case(im, x, y, w, h, body_c=NIGHT[2]):
    d = ImageDraw.Draw(im)
    d.rectangle((x, y, x + w - 1, y + h - 1), fill=body_c)
    d.line((x, y, x + w - 1, y), fill=NIGHT[3])
    d.line((x, y + h - 1, x + w - 1, y + h - 1), fill=(0, 0, 0, 140))
    d.line((x, y, x, y + h - 1), fill=NIGHT[3])
    d.line((x + w - 1, y, x + w - 1, y + h - 1), fill=(0, 0, 0, 140))


def lamp(im, cx, cy, lit, star=False, hue=AMBER[4]):
    """The 16px bulb / star fitting, approximated: a disc (or the star mark is close
    enough as a diamond) with LampGlow's banded halo when lit."""
    d = ImageDraw.Draw(im)
    if lit:
        for r, a in ((10, 12), (8, 34), (6, 78)):
            o = Image.new('RGBA', im.size, (0, 0, 0, 0))
            ImageDraw.Draw(o).ellipse((cx - r, cy - r, cx + r, cy + r),
                                      fill=hue[:3] + (a,))
            im.alpha_composite(o)
    c = hue if lit else (NIGHT[2] if not star else
                         (MAGENTA[4][0], MAGENTA[4][1], MAGENTA[4][2], 158))
    if star:
        pts = [(cx, cy - 5), (cx + 2, cy - 1), (cx + 5, cy - 1), (cx + 3, cy + 2),
               (cx + 4, cy + 5), (cx, cy + 3), (cx - 4, cy + 5), (cx - 3, cy + 2),
               (cx - 5, cy - 1), (cx - 2, cy - 1)]
        d.polygon(pts, fill=c)
    else:
        d.ellipse((cx - 4, cy - 4, cx + 4, cy + 4), fill=c)


def compose(plate_name='plate2_c', out=None, tonight=0, rating=3.4, hhmm='18:40'):
    im = Image.new('RGBA', (W, H), NIGHT[1])
    d = ImageDraw.Draw(im)
    # the beam: lit face, turn, foot, neon
    d.rectangle((0, 0, W, 2), fill=NIGHT[3])
    d.line((0, 3, W, 3), fill=(0, 0, 0, 115))
    d.rectangle((0, H - 3, W, H - 1), fill=NIGHT[0])
    d.rectangle((0, H - 2, W, H - 1), fill=AMBER[4])

    # ── the hour ────────────────────────────────────────────────────────────
    case(im, 12, 5, 142, 44)
    d.rectangle((20, 11, 145, 42), fill=GLASS)
    digits = cd.readout(hhmm)                     # 1x, glass-coloured ground
    digits = digits.resize((digits.width * 2, digits.height * 2), Image.NEAREST)
    im.alpha_composite(digits, (22, 27 - digits.height // 2))

    # ── the week, on the generated plate ────────────────────────────────────
    plate = Image.open(os.path.join(HERE, 'topbar_raw', plate_name + '.png')).convert('RGBA')
    # a raw 224x32 take is cropped to the plate band; a finished 224x22 is used whole
    crop = plate.crop((0, 5, 224, 27)) if plate.height == 32 else plate
    crop = crop.resize((448, 44), Image.NEAREST)
    px, py = 640 - 224, 5
    im.alpha_composite(crop, (px, py))

    heads = body(8)
    names = body(16)
    disp = display(16)
    text(im, (px + 30, py + 15), 'WEEK', heads, CREAM[3])
    text(im, (px + 30, py + 30), '01', disp, CYAN[3])
    d.line((px + 56, py + 11, px + 56, py + 33), fill=CYAN[4][:3] + (56,))
    days = ('MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT', 'SUN')
    for i, day in enumerate(days):
        cx = px + 64 + i * 52 + 26
        closed = i == 6
        lit = i == tonight
        if i > 0:
            d.line((px + 64 + i * 52, py + 12, px + 64 + i * 52, py + 32),
                   fill=NIGHT[3][:3] + (204,))
        if closed:
            for s_ in range(4):
                y = py + 8 + s_ * 4
                d.rectangle((cx - 11, y, cx + 11, y + 1), fill=NIGHT[3])
        else:
            lamp(im, cx, py + 14, lit, star=(i == 5))
        col = NIGHT[4] if closed else AMBER[4] if lit else \
            NIGHT[4] if i < tonight else CREAM[3]
        text(im, (cx, py + 29), day, names, col)

    # ── the standing ────────────────────────────────────────────────────────
    star = Image.open(os.path.join(HERE, 'topbar_raw', 'star_final.png'))
    sock = Image.open(os.path.join(HERE, 'topbar_raw', 'star_socket.png'))
    sx = 1226 - 170
    row = Image.new('RGBA', (170, 32), (0, 0, 0, 0))
    full = Image.new('RGBA', (170, 32), (0, 0, 0, 0))
    for i in range(5):
        row.alpha_composite(sock, (i * 34 + 1, 0))
        full.alpha_composite(star, (i * 34 + 1, 0))
    cut = int(rating / 5.0 * 170)
    row.paste(full.crop((0, 0, cut, 32)), (0, 0), full.crop((0, 0, cut, 32)))
    im.alpha_composite(row, (sx, 27 + 5 - 16))
    text(im, (sx + 85, 27 - 18), 'TONIGHT · REGULARS', body(8), CREAM[3])

    # ── the key ─────────────────────────────────────────────────────────────
    case(im, 1240, 14, 26, 26)
    text(im, (1253, 27), '*', body(16), CREAM[4])

    out = out or os.path.join(HERE, 'topbar_raw', 'strip_mock_%s.png' % plate_name)
    big = im.resize((W, H * 1), Image.NEAREST)
    big.save(out)
    zoom = im.resize((W * 2, H * 2), Image.NEAREST)
    zoom.save(out.replace('.png', '_2x.png'))
    print('mock ->', out)
    return out


if __name__ == '__main__':
    args = sys.argv[1:]
    compose(*(args or ()))
