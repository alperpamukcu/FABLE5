# -*- coding: utf-8 -*-
"""The 55-colour palette (GDD 14 v3 §3), as numbers, a quantizer, and a PNG for PixelLab.

Every drawn pixel in LAST CALL comes from these eleven ramps of five. Values are UITheme.cs
verbatim (the doc is the mirror). Two uses here:

  * quantize(im)   — nearest ramp step per pixel, binary alpha; the chain's palette lock
  * color_image()  — a PNG whose only colours are the 55, handed to create_image_pixflux's
                     `color_image` so the GENERATOR is forced onto the palette rather than
                     the quantizer having to drag an off-palette take onto it afterwards
"""
import os

from PIL import Image

RAMPS = {
    'Night':    ['#0D0813', '#1A1023', '#241830', '#362447', '#4A3160'],
    'Magenta':  ['#5C1B45', '#8F2464', '#C23283', '#E84DA6', '#FF7DC6'],
    'Cyan':     ['#123B45', '#1B5F66', '#26918F', '#3BC8BE', '#7DF0E3'],
    'Amber':    ['#4A2E14', '#8F5A1E', '#C9822B', '#E8A33D', '#F5C97B'],
    'ViceRed':  ['#3D1220', '#6E1B32', '#A62B44', '#D9455C', '#F27D8A'],
    'ClubBlue': ['#131B3D', '#1F2E66', '#2E4699', '#4467CC', '#6E93F0'],
    'Lime':     ['#16331B', '#2A5926', '#479938', '#6FCC4B', '#A8F077'],
    'Cream':    ['#453E38', '#6E6459', '#9C8F80', '#C9BCA8', '#F2E8D5'],
    'Malt':     ['#3A2410', '#6B4416', '#9E6A1D', '#C98F2B', '#E6B959'],
    'Graphite': ['#14161A', '#24272D', '#383D45', '#545A64', '#808893'],
    'Brick':    ['#38161A', '#5C2226', '#7E3130', '#9C4740', '#B96253'],
}


def hex_rgb(h):
    h = h.lstrip('#')
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16))


COLOURS = [hex_rgb(h) for ramp in RAMPS.values() for h in ramp]
assert len(COLOURS) == 55
INK = hex_rgb(RAMPS['Night'][0])          # the one outline colour, #0D0813


def ramp(name, i):
    return hex_rgb(RAMPS[name][i])


def nearest(rgb):
    r, g, b = rgb
    best, bd = COLOURS[0], 1 << 30
    for c in COLOURS:
        # Weighted for the eye: green counts most, blue least.
        d = 3 * (r - c[0]) ** 2 + 4 * (g - c[1]) ** 2 + 2 * (b - c[2]) ** 2
        if d < bd:
            best, bd = c, d
    return best


_CACHE = {}


def quantize(im, alpha_cut=128):
    """Every opaque pixel to its nearest of the 55; alpha made binary at `alpha_cut`."""
    im = im.convert('RGBA')
    px = im.load()
    w, h = im.size
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    op = out.load()
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < alpha_cut:
                continue
            key = (r >> 2, g >> 2, b >> 2)
            c = _CACHE.get(key)
            if c is None:
                c = nearest((r, g, b))
                _CACHE[key] = c
            op[x, y] = c + (255,)
    return out


def off_palette(im):
    """How many opaque pixels are NOT one of the 55 — the audit's palette gate."""
    s = set(COLOURS)
    px = im.convert('RGBA').load()
    w, h = im.size
    n = 0
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a and (r, g, b) not in s:
                n += 1
    return n


def color_image(path):
    """A 55×1 PNG of the palette for PixelLab's `color_image` — it reads colours only."""
    im = Image.new('RGB', (len(COLOURS), 1))
    for i, c in enumerate(COLOURS):
        im.putpixel((i, 0), c)
    # Scaled up so a human can look at it; the generator only reads distinct colours.
    im = im.resize((len(COLOURS) * 8, 8), Image.NEAREST)
    im.save(path)
    return path


if __name__ == '__main__':
    here = os.path.dirname(os.path.abspath(__file__))
    print(color_image(os.path.join(here, 'palette55.png')))
