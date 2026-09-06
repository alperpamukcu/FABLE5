# -*- coding: utf-8 -*-
"""The author's own running water, into the game (2026-09-06).

The tap's water was generated (`sink_water_gen.py`, six frames cut out of the basin's own
silhouette) until the author drew it — fourteen frames for each of the two sinks, each frame
the WHOLE basin at 82x35, which is the sink's own size. So this is a strip, not a redraw:
the frames are laid left to right into one sheet per tier and the stage cuts them by the cell
size the fixture row states.

    Fixtures/fx_sink_water.png       the steel basin, 14 x 82x35   (counter_sink)
    Fixtures/fx_sink_gold_water.png  the brass one,   14 x 82x35   (sink_brass)

Because a frame carries the basin as well as the water, it simply covers the still sink art
while the tap runs — same size, same place, so there is no seam to line up and nothing to
mask. The stage plays them on a loop for exactly as long as Core says the sink is busy.

    py -3 Tools/sink_water_ship.py
"""
import os

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SRC = os.path.join(HERE, 'AssetPipeline', 'sources', 'sink')
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Fixtures')

# (source prefix, how many, the sheet the fixture row names)
SETS = [('fx_sink', 14, 'fx_sink_water'), ('fx_sink_gold', 14, 'fx_sink_gold_water')]


def main():
    for prefix, count, out in SETS:
        frames = []
        for i in range(1, count + 1):
            p = os.path.join(SRC, '%s%d.png' % (prefix, i))
            if not os.path.exists(p):
                raise SystemExit('missing frame: ' + p)
            frames.append(Image.open(p).convert('RGBA'))
        w, h = frames[0].size
        for f in frames:
            if f.size != (w, h):
                raise SystemExit('%s: frames are not all %dx%d' % (prefix, w, h))
        sheet = Image.new('RGBA', (w * len(frames), h), (0, 0, 0, 0))
        for i, f in enumerate(frames):
            sheet.alpha_composite(f, (i * w, 0))
        sheet.save(os.path.join(OUT, out + '.png'))
        print('  %-20s %d frames of %dx%d -> %dx%d' % (out, len(frames), w, h, sheet.width, sheet.height))
    print('  the fixture rows say the cell: "cellW": %d, "cellH": %d' % (82, 35))


if __name__ == '__main__':
    main()
