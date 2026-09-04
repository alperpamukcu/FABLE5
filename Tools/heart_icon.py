# -*- coding: utf-8 -*-
"""The heart, drawn in the star's own language (2026-09-04).

The author gave one icon set for the whole game — the 3D star that already stands in the
top-right standing row, and a heart to match it: "bundan sonra oyunda kalp ve yıldız iconu
olarak her yerde bunları kullanacaksın". There was no heart, so this draws one to the
star's construction rather than inventing a second style:

  * the same 32x32 canvas, the same one-pixel INK keyline INSIDE the silhouette;
  * the same three-tone body with the light coming from the upper left, and the same
    single sparkle where that light lands;
  * two states, exactly as the star has them: the LIT heart in the ViceRed ramp and the
    SOCKET in the same violets star3d_socket uses, so an unearned heart reads as an empty
    slot rather than a grey heart.

Nothing here is generated art — it is the procedural kit, like every other mark in the UI
(the house rule, memory art-direction-rules).

  py -3 -X utf8 Tools/heart_icon.py          # writes the two PNGs into Resources/Items
  py -3 -X utf8 Tools/heart_icon.py preview  # …and a 6x contact sheet beside the star
"""
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
S = 32
INK = (0x0D, 0x08, 0x13)
SPARK = (0xF2, 0xE8, 0xD5)

# The ViceRed ramp (UITheme), light to deep — the same five the palette carries.
LIT = [(0xF2, 0x7D, 0x8A), (0xD9, 0x45, 0x5C), (0xA6, 0x2B, 0x44), (0x6E, 0x1B, 0x32)]
# The socket's own violets, read off star3d_socket.png so the two empty states match.
SOCKET = [(0x4A, 0x31, 0x60), (0x36, 0x24, 0x47), (0x24, 0x18, 0x30), (0x1A, 0x10, 0x23)]


def silhouette():
    """Two lobes and a point — the way a pixel artist builds a heart, not an implicit curve.

    The curve version came out as a shield: its cleft is a dimple at this size and its
    shoulders are square. Two discs and the triangle under them give the notch a depth you
    can see at 32 px, and every edge stays a hard pixel edge (no anti-aliasing, house rule).
    Sampled 2x2 and thresholded at half, so the outline lands where the eye puts it."""
    R = 7.4
    CY = 12.0
    LX, RX = 16.0 - R * 0.96, 16.0 + R * 0.96
    APEX = 29.0
    cells = []
    for ty in range(S):
        row = []
        for x in range(S):
            hits = 0
            for sy in range(2):
                for sx in range(2):
                    u = x + 0.25 + sx * 0.5
                    v = ty + 0.25 + sy * 0.5
                    inside = ((u - LX) ** 2 + (v - CY) ** 2 <= R * R
                              or (u - RX) ** 2 + (v - CY) ** 2 <= R * R)
                    if not inside and v >= CY:
                        # the point: half-width runs from the lobes' own tangents to nothing
                        k = (v - CY) / (APEX - CY)
                        half = (RX - LX) * 0.5 + R * (1.0 - k) - R * k * 0.10
                        inside = k <= 1.0 and abs(u - 16.0) <= half * (1.0 - k)
                    if inside:
                        hits += 1
            row.append(hits >= 2)
        cells.append(row)
    return cells


def draw(ramp):
    """Silhouette → ink ring, three tones of body, one sparkle."""
    cells = silhouette()
    im = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    px = im.load()
    # The light runs down-right: the shade of a cell is how far it is along that axis,
    # measured from the silhouette's own extremes so the ramp always spans the whole shape.
    xs = [x for ty in range(S) for x in range(S) if cells[ty][x]]
    ys = [ty for ty in range(S) for x in range(S) if cells[ty][x]]
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    span = (x1 - x0) + (y1 - y0)
    for ty in range(S):
        for x in range(S):
            if not cells[ty][x]:
                continue
            edge = (ty == 0 or ty == S - 1 or x == 0 or x == S - 1
                    or not cells[ty - 1][x] or not cells[ty + 1][x]
                    or not cells[ty][x - 1] or not cells[ty][x + 1])
            if edge:
                px[x, ty] = INK + (255,)
                continue
            # Light from a POINT off the upper-left shoulder, not along a diagonal: the
            # bands then curve round the lobes the way the star's curve round its arms,
            # instead of striping the shape corner to corner.
            d = (((x - (x0 + (x1 - x0) * 0.28)) ** 2
                  + (ty - (y0 + (y1 - y0) * 0.18)) ** 2) ** 0.5)
            t = min(1.0, d / float(max(1, span) * 0.62))
            i = 0 if t < 0.34 else 1 if t < 0.62 else 2 if t < 0.90 else 3
            px[x, ty] = ramp[i] + (255,)
    # The sparkle, where the light lands: a plus, five pixels, exactly as the star wears it.
    cx = x0 + (x1 - x0) // 4 + 1
    cy = y0 + (y1 - y0) // 4 + 2
    for dx, dy in ((0, 0), (-1, 0), (1, 0), (0, -1), (0, 1)):
        if cells[cy + dy][cx + dx]:
            px[cx + dx, cy + dy] = SPARK + (255,)
    return im


def main():
    lit = draw(LIT)
    socket = draw(SOCKET)
    # The socket keeps the shape and loses the light: its sparkle would be a lamp in an
    # empty slot, so it is painted out with the socket's own brightest violet.
    sp = socket.load()
    for y in range(S):
        for x in range(S):
            if sp[x, y][:3] == SPARK:
                sp[x, y] = SOCKET[0] + (255,)
    lit.save(os.path.join(ITEMS, 'heart3d.png'))
    socket.save(os.path.join(ITEMS, 'heart3d_socket.png'))
    print('heart3d.png / heart3d_socket.png written (%d opaque)'
          % sum(1 for p in lit.getdata() if p[3] > 128))

    if len(sys.argv) > 1 and sys.argv[1] == 'preview':
        star = Image.open(os.path.join(ITEMS, 'star3d.png')).convert('RGBA')
        ssock = Image.open(os.path.join(ITEMS, 'star3d_socket.png')).convert('RGBA')
        k = 6
        sheet = Image.new('RGBA', (4 * (S * k + 8) + 8, S * k + 16), (36, 24, 48, 255))
        for i, im in enumerate([ssock, star, socket, lit]):
            b = im.resize((S * k, S * k), Image.NEAREST)
            sheet.paste(b, (8 + i * (S * k + 8), 8), b)
        out = os.path.join(os.environ.get('TEMP', '.'), 'heart_preview.png')
        sheet.save(out)
        print('preview', out)


if __name__ == '__main__':
    main()
