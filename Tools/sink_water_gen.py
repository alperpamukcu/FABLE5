# -*- coding: utf-8 -*-
"""THE SINK'S WATER (2026-09-05, GDD 27 §4.3 / PLAN_house_and_law H4): the stream that runs
while the glasses are washed, drawn as a frame sheet the stage cuts by the cell size the
fixture's data states (`water`, `cellW`, `cellH` on the drain fixtures in fixtures.json).

Procedural on purpose — the house rule is that chrome and effects are drawn, and a running
tap is a few pixels of palette-locked cyan falling from the spout into the basin, not an
illustration. The sheet is one row of frames, each the sink's own canvas (82x35), so the
overlay lands on the basin at (0, 0) with the basin's own pivot and nothing has to be aligned
by hand; the spout and the basin's water line are READ off fx_sink.png rather than typed.

  py -3 -X utf8 Tools/sink_water_gen.py            # writes Assets/Resources/Fixtures/fx_sink_water.png
  py -3 -X utf8 Tools/sink_water_gen.py probe      # prints the sink's silhouette and the two rows it found
  py -3 -X utf8 Tools/sink_water_gen.py preview    # ...and a 6x sheet of the frames over the sink (Tools/, ignored)
"""
import os, sys
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SINK = os.path.join(ROOT, 'Assets', 'Resources', 'Fixtures', 'fx_sink.png')
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Fixtures', 'fx_sink_water.png')
FRAMES = 6

# GDD 14 v3 palette, the cyan ramp and the cream cap (UITheme verbatim)
CYAN = [(0x12, 0x3B, 0x45), (0x1B, 0x5F, 0x66), (0x26, 0x91, 0x8F), (0x3B, 0xC8, 0xBE), (0x7D, 0xF0, 0xE3)]
CREAM4 = (0xF2, 0xE8, 0xD5)
INK = (0x0D, 0x08, 0x13)


def silhouette():
    im = Image.open(SINK).convert('RGBA')
    w, h = im.size
    px = im.load()
    rows = []
    for y in range(h):
        rows.append(''.join('#' if px[x, y][3] > 127 else '.' for x in range(w)))
    return im, rows


def measure(im):
    """Read off the silhouette (see `probe`): the basin is the slab — the first row that is
    at least 60% opaque — and the tap stands over it as a hooked neck whose LEFT prong is
    the spout. The stream starts one row under the prong's last pixel and lands on the
    slab's top row; the splash stays inside the slab, clear of the neck on the right."""
    w, h = im.size
    px = im.load()

    def opaque(x, y):
        return px[x, y][3] > 127

    counts = [sum(1 for x in range(w) if opaque(x, y)) for y in range(h)]
    basin_y = next(y for y in range(h) if counts[y] >= 0.6 * w)
    # The hook's NECK is thin; the flange it stands on (the two rows over the slab) is
    # wide. Only the thin rows say where the spout is.
    tap_rows = [y for y in range(basin_y) if 0 < counts[y] <= 9]
    left = min(x for y in tap_rows for x in range(w) if opaque(x, y))
    mouth_x = left + 1
    mouth_y = max(y for y in tap_rows if opaque(mouth_x, y)) + 1
    x0 = min(x for x in range(w) if opaque(x, basin_y))
    x1 = max(x for x in range(w) if opaque(x, basin_y))
    # The slab's top row is the basin's RIM; the water lands in the bowl below it, which
    # the drawing carries about a third of the way down the slab.
    water_y = basin_y + 8
    return mouth_x, mouth_y, water_y, x0 + 8, x1 - 14


def hash01(x, y, s):
    n = (x * 374761393 + y * 668265263 + s * 1442695041) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFFFF) / 65535.0


def frame(im, i, spout_x, spout_y, basin_y, bx0, bx1):
    w, h = im.size
    f = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    p = f.load()
    # THE STREAM: two pixels wide from just under the spout to the water line, the lit
    # core walking down a pixel a frame so it reads as falling rather than as a rod.
    top = spout_y + 6
    for y in range(top, basin_y):
        for dx in (0, 1):
            x = spout_x - 1 + dx
            if 0 <= x < w:
                core = ((y - top + i) % 4) == 0
                p[x, y] = CYAN[4] if core else CYAN[3]
    # THE SPLASH where it lands: a few drops that spread and thin over the cycle, kept
    # inside the basin's walls.
    spread = [1, 2, 3, 3, 2, 1][i % 6]
    for k in range(-spread, spread + 1):
        x = spout_x + k * 2 + (1 if k > 0 else 0)
        if bx0 <= x <= bx1 and k != 0:
            y = basin_y - (1 if abs(k) == spread else 0)
            p[x, y] = CREAM4 if hash01(x, y, i) > 0.5 else CYAN[4]
    # THE WATER LINE: the basin's own surface, one row, shifting its highlights.
    for x in range(bx0 + 1, bx1):
        if hash01(x, basin_y + 1, i) > 0.72:
            p[x, basin_y + 1] = CYAN[3]
    # a ring pushing out from the stream on the surface
    r = 3 + (i % 3) * 3
    for x in (spout_x - r, spout_x + r):
        if bx0 + 1 <= x <= bx1 - 1:
            p[x, basin_y + 1] = CYAN[4]
    return f


def build():
    im, rows = silhouette()
    spout_x, spout_y, basin_y, bx0, bx1 = measure(im)
    w, h = im.size
    sheet = Image.new('RGBA', (w * FRAMES, h), (0, 0, 0, 0))
    for i in range(FRAMES):
        sheet.paste(frame(im, i, spout_x, spout_y, basin_y, bx0, bx1), (i * w, 0))
    return im, sheet, (spout_x, spout_y, basin_y, bx0, bx1)


if __name__ == '__main__':
    mode = sys.argv[1] if len(sys.argv) > 1 else 'write'
    im, rows = silhouette()
    if mode == 'probe':
        for r in rows: print(r)
        print('measure (spout_x, spout_y, basin_y, bx0, bx1):', measure(im))
        sys.exit(0)
    im, sheet, m = build()
    if mode == 'preview':
        w, h = im.size
        prev = Image.new('RGBA', (w * FRAMES, h), (90, 90, 100, 255))
        for i in range(FRAMES):
            prev.alpha_composite(im, (i * w, 0))
        prev.alpha_composite(sheet, (0, 0))
        prev = prev.resize((prev.width * 6, prev.height * 6), Image.NEAREST)
        out = os.path.join(HERE, 'sink_water_preview.png')
        prev.save(out); print('preview', out, m)
        sys.exit(0)
    sheet.save(OUT)
    print('wrote', OUT, sheet.size, 'measured', m)
