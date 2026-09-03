# -*- coding: utf-8 -*-
"""A 3×5 pixel capital face for label wordmarks.

PixelLab cannot write text — "LAST CALL" came back "LAST COLL", "loca cola" came back
mirrored (memory pixellab-mcp-constraints). So the wordmark is never asked of the generator:
the label carries the brand word struck by this face, in the brand's own ramp, at 2× when it
fits the plate and 1× when it does not. Deterministic, legible at 6×10 per glyph, and the same
hand on every bottle — which is the point of the whole v4 pass.
"""
from PIL import Image

# 3 wide × 5 tall, row strings, '#' = ink.
GLYPHS = {
    'A': ['.#.', '#.#', '###', '#.#', '#.#'],
    'B': ['##.', '#.#', '##.', '#.#', '##.'],
    'C': ['.##', '#..', '#..', '#..', '.##'],
    'D': ['##.', '#.#', '#.#', '#.#', '##.'],
    'E': ['###', '#..', '##.', '#..', '###'],
    'F': ['###', '#..', '##.', '#..', '#..'],
    'G': ['.##', '#..', '#.#', '#.#', '.##'],
    'H': ['#.#', '#.#', '###', '#.#', '#.#'],
    'I': ['###', '.#.', '.#.', '.#.', '###'],
    'J': ['..#', '..#', '..#', '#.#', '.#.'],
    'K': ['#.#', '#.#', '##.', '#.#', '#.#'],
    'L': ['#..', '#..', '#..', '#..', '###'],
    'M': ['#.#', '###', '###', '#.#', '#.#'],
    'N': ['##.', '#.#', '#.#', '#.#', '#.#'],
    'O': ['.#.', '#.#', '#.#', '#.#', '.#.'],
    'P': ['##.', '#.#', '##.', '#..', '#..'],
    'Q': ['.#.', '#.#', '#.#', '.#.', '..#'],
    'R': ['##.', '#.#', '##.', '#.#', '#.#'],
    'S': ['.##', '#..', '.#.', '..#', '##.'],
    'T': ['###', '.#.', '.#.', '.#.', '.#.'],
    'U': ['#.#', '#.#', '#.#', '#.#', '.#.'],
    'V': ['#.#', '#.#', '#.#', '.#.', '.#.'],
    'W': ['#.#', '#.#', '###', '###', '#.#'],
    'X': ['#.#', '#.#', '.#.', '#.#', '#.#'],
    'Y': ['#.#', '#.#', '.#.', '.#.', '.#.'],
    'Z': ['###', '..#', '.#.', '#..', '###'],
    '0': ['.#.', '#.#', '#.#', '#.#', '.#.'],
    '1': ['.#.', '##.', '.#.', '.#.', '###'],
    '2': ['##.', '..#', '.#.', '#..', '###'],
    '3': ['##.', '..#', '.#.', '..#', '##.'],
    '4': ['#.#', '#.#', '###', '..#', '..#'],
    '5': ['###', '#..', '##.', '..#', '##.'],
    '6': ['.##', '#..', '##.', '#.#', '.#.'],
    '7': ['###', '..#', '.#.', '.#.', '.#.'],
    '8': ['.#.', '#.#', '.#.', '#.#', '.#.'],
    '9': ['.#.', '#.#', '.##', '..#', '##.'],
    "'": ['.#.', '.#.', '...', '...', '...'],
    '&': ['.#.', '#.#', '.#.', '#.#', '.##'],
    '-': ['...', '...', '###', '...', '...'],
    '.': ['...', '...', '...', '...', '.#.'],
    ' ': ['...', '...', '...', '...', '...'],
}

GW, GH, GAP = 3, 5, 1


def width(text, scale=1):
    n = len(text)
    return (n * GW + (n - 1) * GAP) * scale if n else 0


def render(text, colour, scale=1, shadow=None):
    """The word as an RGBA image, glyphs `scale`× up, optional 1-px drop shadow colour."""
    text = text.upper()
    w = width(text, scale) + (scale if shadow else 0)
    h = GH * scale + (scale if shadow else 0)
    im = Image.new('RGBA', (max(1, w), max(1, h)), (0, 0, 0, 0))
    px = im.load()
    x0 = 0
    for ch in text:
        rows = GLYPHS.get(ch, GLYPHS[' '])
        for gy, row in enumerate(rows):
            for gx, c in enumerate(row):
                if c != '#':
                    continue
                for sy in range(scale):
                    for sx in range(scale):
                        X = x0 + gx * scale + sx
                        Y = gy * scale + sy
                        if shadow is not None:
                            px[X + scale, Y + scale] = shadow + (255,)
                        px[X, Y] = colour + (255,)
        x0 += (GW + GAP) * scale
    return im


if __name__ == '__main__':
    render('SMIRKOFF 48', (242, 232, 213), 2, shadow=(13, 8, 19)).resize((300, 40), Image.NEAREST).save('fontpx_demo.png')
