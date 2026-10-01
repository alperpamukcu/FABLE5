# -*- coding: utf-8 -*-
"""A title-sign set DRAWN at any scale, for the one Steam slot no shipped set fits (the small capsule).

The sign's own builder (Tools/title_sign/build_logo.py) samples two master drawings onto a set's grid and grows
the four flat glow bands at that size - nothing is resized from another set. The masters live only on the
author's machine, so this feeds the builder pseudo-masters read off the shipped x4 map (tube = classes 5-7,
core = 7, lining = 6-7, liquid = 8; groups 1-12 the mark, 13-32 the genre line), placed 1:1 at 0.25 canvas
units per x4 texel. The result is a map like the shipped ones, lit by logo_render's ink tables.
"""
import os, sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
TS = os.path.join(HERE, '..', 'title_sign')
sys.path.insert(0, TS)
import build_logo as BL                     # noqa: E402

MAP = os.path.join(HERE, '..', '..', 'Assets', 'Resources', 'Logo', 'logo_x4_map.bytes')


def pseudo_masters():
    m = np.asarray(Image.open(MAP).convert('RGB'))
    cls, grp = m[..., 0], m[..., 1]
    tube = (cls >= 5) & (cls <= 7)
    mark = (grp >= 1) & (grp <= 12)
    line = grp >= 13
    return dict(m_tube=tube & mark, m_core=(cls == 7) & mark, m_lining=((cls == 6) | (cls == 7)) & mark,
                m_groups=np.where(mark, grp, 0).astype(np.uint8), m_liquid=(cls == 8),
                l_tube=tube & line, l_core=(cls == 7) & line, l_groups=np.where(line, grp, 0).astype(np.uint8))


def build_map(width):
    BL.PLACE = {'mark': dict(scale=0.25, m0=(0, 0), t0=(0.0, 0.0)),
                'line': dict(scale=0.25, m0=(0, 0), t0=(0.0, 0.0))}
    b = BL.build(width / 560.0, pseudo_masters())
    return b


_cache = {}


def render(width):
    """RGBA picture of a set drawn natively at `width` px (cached per width)."""
    if width not in _cache:
        from logo_render import render_map
        b = build_map(width)
        m = np.stack([b['cls'], b['grp'], b['phase']], -1).astype(np.uint8)
        _cache[width] = render_map(m)
    return _cache[width]
