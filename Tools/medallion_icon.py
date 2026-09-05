# -*- coding: utf-8 -*-
"""The medallion — comfort's symbol — drawn in the star's own language (2026-09-05, GDD 27 §2.5).

The author gave the house two ratings and asked for two symbols ("iki farklı sembol bulmalıyız,
örneğin madalyon ve kalp vs. gibi"). The heart already exists (Tools/heart_icon.py) and is
SERVICE; this is COMFORT — what the room is worth — and it is built exactly as the star and the
heart are, so the three read as one set:

  * the same 32x32 canvas, the same one-pixel INK keyline INSIDE the silhouette;
  * the same three-tone body lit from the upper left, and the same five-pixel sparkle;
  * two states: the LIT medallion in the Cyan ramp with a brass (Amber) rim and a ViceRed ribbon,
    and the SOCKET in the violets star3d_socket uses, so an unearned medallion reads as an empty
    slot rather than a grey medal.

The shape is a disc on two ribbon tails — a medal, not a coin: the tails are what keep it from
reading as a button at 16 px. Nothing here is generated art; it is the procedural kit, like every
other mark in the UI (memory art-direction-rules). The 16 px pair is derived by
Tools/icon_sizes.py, which lists the medallion beside the star and the heart.

  py -3 -X utf8 Tools/medallion_icon.py          # writes the two PNGs into Resources/Items
  py -3 -X utf8 Tools/medallion_icon.py preview  # ...and a 6x contact sheet beside the star and heart
"""
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
S = 32
INK = (0x0D, 0x08, 0x13)
SPARK = (0xF2, 0xE8, 0xD5)

# UITheme.Cyan, light to deep (the Flavor colour — the room's, not the money's).
LIT = [(0x7D, 0xF0, 0xE3), (0x3B, 0xC8, 0xBE), (0x26, 0x91, 0x8F), (0x1B, 0x5F, 0x66)]
# UITheme.Amber for the rim — brass, the house's metal.
RIM = [(0xF5, 0xC9, 0x7B), (0xE8, 0xA3, 0x3D), (0xC9, 0x82, 0x2B), (0x8F, 0x5A, 0x1E)]
# UITheme.ViceRed for the ribbon.
RIBBON = [(0xF2, 0x7D, 0x8A), (0xD9, 0x45, 0x5C), (0xA6, 0x2B, 0x44), (0x6E, 0x1B, 0x32)]
# The socket's own violets, read off star3d_socket.png so the three empty states match.
SOCKET = [(0x4A, 0x31, 0x60), (0x36, 0x24, 0x47), (0x24, 0x18, 0x30), (0x1A, 0x10, 0x23)]

CX, CY, R = 16.0, 12.5, 10.6          # the disc
RIM_W = 2.2                            # the brass band, measured inward from the edge
TAIL_TOP, TAIL_BOT = 19.0, 30.0        # the ribbon tails, under the disc


def region(u, v):
    """'disc' / 'rim' / 'ribbon' / None for a sample point."""
    d = ((u - CX) ** 2 + (v - CY) ** 2) ** 0.5
    if d <= R:
        return 'rim' if d >= R - RIM_W else 'disc'
    if TAIL_TOP <= v <= TAIL_BOT:
        # two tails, each a slanted band 5 wide, spreading from under the disc
        k = (v - TAIL_TOP) / (TAIL_BOT - TAIL_TOP)
        for side in (-1, 1):
            centre = CX + side * (3.2 + 2.6 * k)
            if abs(u - centre) <= 2.6:
                # a notched end, the way a ribbon is cut
                if v >= TAIL_BOT - 1.5 and abs(u - centre) < 1.0:
                    return None
                return 'ribbon'
    return None


def cells():
    """Sampled 2x2 and thresholded at half, so the outline lands where the eye puts it."""
    grid = []
    for ty in range(S):
        row = []
        for x in range(S):
            hits = {}
            for sy in range(2):
                for sx in range(2):
                    r = region(x + 0.25 + sx * 0.5, ty + 0.25 + sy * 0.5)
                    if r:
                        hits[r] = hits.get(r, 0) + 1
            if sum(hits.values()) < 2:
                row.append(None)
            else:
                # the disc wins ties with the rim so the band stays thin; the rim wins over
                # the ribbon so the tails tuck under the metal
                for pick in ('disc', 'rim', 'ribbon'):
                    if hits.get(pick, 0) >= 2 or (pick == 'ribbon' and hits.get(pick, 0) >= 1 and sum(hits.values()) >= 2):
                        row.append(pick)
                        break
                else:
                    row.append(max(hits, key=hits.get))
        grid.append(row)
    return grid


def shade(grid, x0, x1, y0, y1, x, ty):
    """0..3: how far a cell is from a light point off the upper-left shoulder."""
    span = (x1 - x0) + (y1 - y0)
    d = (((x - (x0 + (x1 - x0) * 0.30)) ** 2 + (ty - (y0 + (y1 - y0) * 0.22)) ** 2) ** 0.5)
    t = min(1.0, d / float(max(1, span) * 0.62))
    return 0 if t < 0.34 else 1 if t < 0.62 else 2 if t < 0.90 else 3


def draw(body, rim, ribbon):
    grid = cells()
    im = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    px = im.load()
    # The disc and the ribbon are lit as two bodies: the shade of a disc cell is measured
    # against the DISC's own extremes (not the whole medal's, or the tails drag the light
    # point down and the disc goes flat), the tails against theirs.
    def bounds(kinds):
        pts = [(x, ty) for ty in range(S) for x in range(S) if grid[ty][x] in kinds]
        return (min(p[0] for p in pts), max(p[0] for p in pts),
                min(p[1] for p in pts), max(p[1] for p in pts))
    disc_b = bounds(('disc', 'rim'))
    tail_b = bounds(('ribbon',))
    for ty in range(S):
        for x in range(S):
            kind = grid[ty][x]
            if not kind:
                continue
            edge = (ty == 0 or ty == S - 1 or x == 0 or x == S - 1
                    or not grid[ty - 1][x] or not grid[ty + 1][x]
                    or not grid[ty][x - 1] or not grid[ty][x + 1])
            if edge:
                px[x, ty] = INK + (255,)
                continue
            b = tail_b if kind == 'ribbon' else disc_b
            i = shade(grid, b[0], b[1], b[2], b[3], x, ty)
            ramp = body if kind == 'disc' else rim if kind == 'rim' else ribbon
            px[x, ty] = ramp[i] + (255,)
    # The sparkle, where the light lands on the disc: a plus, five pixels.
    cx = int(CX - R * 0.42)
    cy = int(CY - R * 0.36)
    for dx, dy in ((0, 0), (-1, 0), (1, 0), (0, -1), (0, 1)):
        if grid[cy + dy][cx + dx] == 'disc':
            px[cx + dx, cy + dy] = SPARK + (255,)
    return im


def main():
    lit = draw(LIT, RIM, RIBBON)
    socket = draw(SOCKET, SOCKET, SOCKET)
    sp = socket.load()
    for y in range(S):
        for x in range(S):
            if sp[x, y][:3] == SPARK:
                sp[x, y] = SOCKET[0] + (255,)
    lit.save(os.path.join(ITEMS, 'medal3d.png'))
    socket.save(os.path.join(ITEMS, 'medal3d_socket.png'))
    print('medal3d.png / medal3d_socket.png written (%d opaque)'
          % sum(1 for p in lit.getdata() if p[3] > 128))

    if len(sys.argv) > 1 and sys.argv[1] == 'preview':
        names = ['star3d_socket', 'star3d', 'heart3d_socket', 'heart3d']
        ims = [Image.open(os.path.join(ITEMS, n + '.png')).convert('RGBA') for n in names] + [socket, lit]
        k = 6
        sheet = Image.new('RGBA', (len(ims) * (S * k + 8) + 8, S * k + 16 + S + 8), (36, 24, 48, 255))
        for i, im in enumerate(ims):
            b = im.resize((S * k, S * k), Image.NEAREST)
            sheet.paste(b, (8 + i * (S * k + 8), 8), b)
            sheet.paste(im, (8 + i * (S * k + 8), S * k + 14), im)
        out = os.path.join(os.environ.get('TEMP', '.'), 'medal_preview.png')
        sheet.save(out)
        print('preview', out)


if __name__ == '__main__':
    main()
