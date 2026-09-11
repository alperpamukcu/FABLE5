# -*- coding: utf-8 -*-
"""The right wall ladder (2026-09-12, the author: "4 tier sağ duvar getirdim bunları da ekleyeceğiz").

The author drew the room's right wall four ways, each cut to the room's own pixels
(214x297, the wall, its door frame and the threshold). They are laid over the back wall's
plate as a layer of their own, so the right wall climbs its own ladder instead of changing
with the back wall.

  sources   Tools/AssetPipeline/sources/konsept_art/right_wall_t{1..4}.png
  out       Assets/Resources/Fixtures/fx_walls_right_{1..4}.png         640x360 layers
            Assets/Resources/Fixtures/fx_walls_right_swatch_{1..4}.png  64x48 market swatches

WHERE: at (426, 0) of the 640x360 room, found by matching tier 1 against fx_walls_1, whose
right wall it is — 64% of its pixels land on the plate's own, the rest is the door.

THE WHOLE DRAWING, DOOR AND ALL (2026-09-12, the author: "yeni yüklenen sağ duvarın tamamını
kullan kesme"). Inside the door frame the drawings carry a flat beige panel — the same 8,580
pixels in all four — where the plate has the dark recess and the "+20 ONLY" sign the door law
hangs on (GDD 28). The wall is laid down as drawn, so that sign is no longer on the door; it
would come back as a plate of its own over this layer. KEEP_DRAWN_DOOR = False clears the door
region instead (it is measured as: where tier 1 differs from the plate it was cut from, right
of the jamb and below the lintel), which leaves the room's own door showing through every rung.

  py -3 -X utf8 Tools/right_wall_ship.py
"""
import os

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SRC = os.path.join(HERE, 'AssetPipeline', 'sources', 'konsept_art')
FIXT = os.path.join(ROOT, 'Assets', 'Resources', 'Fixtures')
AT = (426, 0)                 # the drawing's top-left in the room, y down
JAMB_X, LINTEL_Y = 146, 55    # the door region starts here, in the drawing's own pixels
SWATCH = (64, 48)
KEEP_DRAWN_DOOR = True


def load(i):
    return np.array(Image.open(os.path.join(SRC, 'right_wall_t%d.png' % i)).convert('RGBA'))


def door_mask(t1):
    """Where the drawing is the DOOR: tier 1 against the plate it was drawn from."""
    h, w = t1.shape[:2]
    plate = np.array(Image.open(os.path.join(FIXT, 'fx_walls_1.png')).convert('RGBA'))
    under = plate[AT[1]:AT[1] + h, AT[0]:AT[0] + w].astype(int)
    differs = (np.abs(under[:, :, :3] - t1[:, :, :3].astype(int)).max(axis=2) > 2) & (t1[:, :, 3] > 200)
    region = np.zeros_like(differs)
    region[LINTEL_Y:, JAMB_X:] = True
    return differs & region


def swatch_of(tier, mask):
    """The busiest fully drawn 64x48 window on the wall's face, clear of the door — and of
    the grey floor, ceiling and frame lines, which are the room's and not the wall's (a plain
    wall's busiest window is otherwise the one that catches the floor line's corner)."""
    h, w = tier.shape[:2]
    sw, sh = SWATCH
    rgb = tier[:, :, :3].astype(int)
    grey = ((np.abs(rgb[:, :, 0] - rgb[:, :, 1]) < 8) & (np.abs(rgb[:, :, 1] - rgb[:, :, 2]) < 8)
            & (rgb[:, :, 0] > 60))
    best, at = -1.0, None
    for y in range(0, h - sh, 2):
        for x in range(0, JAMB_X - sw, 2):
            win = tier[y:y + sh, x:x + sw]
            if (win[:, :, 3] < 250).any() or mask[y:y + sh, x:x + sw].any() \
                    or grey[y:y + sh, x:x + sw].any():
                continue
            score = float(win[:, :, :3].astype(float).std(axis=(0, 1)).sum())
            if score > best:
                best, at = score, (x, y)
    if at is None:
        raise SystemExit('no clean %dx%d window on the wall face' % SWATCH)
    x, y = at
    return Image.fromarray(tier[y:y + sh, x:x + sw].copy())


def main():
    tiers = [load(i) for i in (1, 2, 3, 4)]
    mask = door_mask(tiers[0])
    print('door region: %d px' % mask.sum())
    for i, t in enumerate(tiers, 1):
        layer = t.copy()
        if not KEEP_DRAWN_DOOR:
            layer[mask, 3] = 0
        room = Image.new('RGBA', (640, 360), (0, 0, 0, 0))
        room.alpha_composite(Image.fromarray(layer), AT)
        room.save(os.path.join(FIXT, 'fx_walls_right_%d.png' % i))
        swatch_of(t, mask).save(os.path.join(FIXT, 'fx_walls_right_swatch_%d.png' % i))
        print('tier %d  ->  fx_walls_right_%d.png + swatch' % (i, i))

    # Rung 1 over the plate it was drawn from must BE that plate: the room opens unchanged.
    plate = Image.open(os.path.join(FIXT, 'fx_walls_1.png')).convert('RGBA')
    over = plate.copy()
    over.alpha_composite(Image.open(os.path.join(FIXT, 'fx_walls_right_1.png')))
    d = np.abs(np.array(over).astype(int) - np.array(plate).astype(int))[:, :, :3].max(axis=2)
    print('rung 1 over fx_walls_1: %d px differ (the drawing\'s own edge pixels), max %d'
          % ((d > 2).sum(), d.max()))


if __name__ == '__main__':
    main()
