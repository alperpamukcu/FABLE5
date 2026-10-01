# -*- coding: utf-8 -*-
"""Render the shipped MALIBU CLUB title sign exactly as the game lights it (TitleSignLight, dress Lit),
from Resources/Logo/logo_<set>_map.bytes. Never redraws the letters: the map is the logo."""
import os, sys
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'title_sign'))
from logo_lib import RAMPS

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')
A4, A3, A2, A1 = 150, 78, 34, 12


def t(r, s): h = RAMPS[r][s]; return ((h >> 16) & 255, (h >> 8) & 255, h & 255)


# class 1..7 -> (ramp, step, alpha) in the Lit dress; 8 liquid
MAG = [('Magenta', 3, A1), ('Magenta', 3, A2), ('Magenta', 3, A3), ('Magenta', 3, A4),
       ('Magenta', 3, 255), ('Magenta', 4, 255), ('Cream', 4, 255)]
CYA = [('Cyan', 4, A1), ('Cyan', 4, A2), ('Cyan', 4, A3), ('Cyan', 4, A4),
       ('Cyan', 4, 255), ('Cyan', 4, 255), ('Cream', 4, 255)]


def render(scale='x4', glow=True, liquid=True):
    m = np.asarray(Image.open(os.path.join(ROOT, 'Assets/Resources/Logo/logo_%s_map.bytes' % scale)).convert('RGB'))
    cls, grp = m[..., 0], m[..., 1]
    out = np.zeros(m.shape[:2] + (4,), np.uint8)
    for c in range(1, 8):
        if not glow and c <= 4:
            continue
        for table, sel in ((MAG, (grp >= 1) & (grp < 12)), (CYA, grp >= 12)):
            r, s, a = table[c - 1]
            k = (cls == c) & sel
            out[k, :3] = t(r, s); out[k, 3] = a
    if liquid:
        k = cls == 8
        out[k, :3] = t('Cyan', 2); out[k, 3] = 190
    return Image.fromarray(out, 'RGBA')


if __name__ == '__main__':
    render(sys.argv[1] if len(sys.argv) > 1 else 'x4').save(sys.argv[2] if len(sys.argv) > 2 else 'logo.png')
