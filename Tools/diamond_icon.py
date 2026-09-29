# -*- coding: utf-8 -*-
"""COMFORT'S DIAMOND (2026-09-29, the author: "Konfor ikonu elmas iconu ile değiştirilsin sanat tarzı servis iconu
olan kalp ile yıldız ikonlarında olsun kullanıldığı yerlerdeki boylarına göre yap aynı servis(kalp) ikonunda olduğu
gibi").

The medallion (Tools/medallion_icon.py, medal3d*) is retired from the screen; the comfort mark is a cut gem drawn in the
language of the heart and the stars the author drew themselves (Items/heart_lit, star_small, star_big):

  * the same 12x12 cell as the heart, the silhouette's outer ring one pixel of white (the socket's ring light grey);
  * facets in flat tones, no anti-aliasing: a light crown, a darker girdle band, a pavilion falling to one point;
  * the light from the upper right and the shade at the lower left, exactly as on the heart;
  * two states as the heart has them: LIT in the palette's Cyan ramp with a Cream[4] glint, the SOCKET in the heart
    socket's three greys (ring, inner edge, fill) so an unearned diamond reads as an empty slot.

Two sizes, each on the canvas of the box it is drawn in, so no caller has to know the drawing's size and it is never
scaled by a fraction: the 12 at 1x centred on a 16 canvas for the 16 boxes (the strips, the buffs, the jobs, the
fittings), and the same drawing at exactly 2x centred on a 32 canvas for the 32 boxes (the night's tape - where the
heart is drawn at 24, the same 2x -, the market's comfort band, the TOMORROW board).

  py -3 -X utf8 Tools/diamond_icon.py            write Items/diamond_{lit,socket}[_32].png (+ metas, from heart_lit's)
  py -3 -X utf8 Tools/diamond_icon.py preview    ...and a contact sheet beside the heart and the stars
Idempotent: a file whose pixels already match is left alone.
"""
import hashlib
import io
import os
import re
import sys
import uuid

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
META_FROM = os.path.join(ITEMS, 'heart_lit.png.meta')

# A = ring, D = glint, C = light facet, B = mid facet, E = shade, F = deep shade; '.' = clear.
GRID = [
    "............",
    "...AAAAAA...",
    "..ACCDDDCA..",
    ".ACBCDDDCDA.",
    "AEBCCBCDDCBA",
    "AFEEBBBBCCBA",
    ".AFEBCBCCDA.",
    "..AFEBCCDA..",
    "...AEBCDA...",
    "....AECA....",
    ".....AA.....",
    "............",
]

WHITE = (0xFF, 0xFF, 0xFF, 255)
# UITheme: Cream[4] and the Cyan ramp 4..1 (light to deep).
LIT = {'A': WHITE, 'D': (0xF2, 0xE8, 0xD5, 255), 'C': (0x7D, 0xF0, 0xE3, 255), 'B': (0x3B, 0xC8, 0xBE, 255),
       'E': (0x26, 0x91, 0x8F, 255), 'F': (0x1B, 0x5F, 0x66, 255)}
# heart_socket's own three greys: ring, inner edge, fill.
S_RING, S_EDGE, S_FILL = (0xB4, 0xB4, 0xB4, 255), (0x4C, 0x4C, 0x4C, 255), (0x85, 0x85, 0x85, 255)


def lit():
    im = Image.new('RGBA', (12, 12), (0, 0, 0, 0))
    for y, row in enumerate(GRID):
        for x, ch in enumerate(row):
            if ch != '.':
                im.putpixel((x, y), LIT[ch])
    return im


def socket():
    im = Image.new('RGBA', (12, 12), (0, 0, 0, 0))
    ring = lambda x, y: 0 <= x < 12 and 0 <= y < 12 and GRID[y][x] == 'A'
    for y, row in enumerate(GRID):
        for x, ch in enumerate(row):
            if ch == '.':
                continue
            if ch == 'A':
                im.putpixel((x, y), S_RING)
            elif any(ring(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                im.putpixel((x, y), S_EDGE)
            else:
                im.putpixel((x, y), S_FILL)
    return im


def on16(drawing):
    im = Image.new('RGBA', (16, 16), (0, 0, 0, 0))
    im.paste(drawing, (2, 2))
    return im


def on32(drawing):
    im = Image.new('RGBA', (32, 32), (0, 0, 0, 0))
    im.paste(drawing.resize((24, 24), Image.NEAREST), (4, 4))
    return im


def digest(im):
    return hashlib.sha1(im.convert('RGBA').tobytes()).hexdigest()


def ship(name, im):
    out = os.path.join(ITEMS, name + '.png')
    if os.path.exists(out) and digest(Image.open(out)) == digest(im):
        return False
    im.save(out)
    meta = out + '.meta'
    if not os.path.exists(meta):
        src = io.open(META_FROM, encoding='utf-8', newline='').read()
        guid = uuid.uuid5(uuid.NAMESPACE_URL, 'lastcall/' + name).hex
        io.open(meta, 'w', encoding='utf-8', newline='').write(re.sub(r'guid: [0-9a-f]{32}', 'guid: ' + guid, src, count=1))
    return True


def main():
    l, s = lit(), socket()
    shots = {'diamond_lit': on16(l), 'diamond_socket': on16(s), 'diamond_lit_32': on32(l), 'diamond_socket_32': on32(s)}
    for name, im in shots.items():
        print(('wrote ' if ship(name, im) else 'kept  ') + name)
    if 'preview' in sys.argv[1:]:
        row = [Image.open(os.path.join(ITEMS, n + '.png')).convert('RGBA') for n in
               ('heart_lit', 'heart_socket', 'star_small', 'star_small_socket')] + [l, s]
        sheet = Image.new('RGBA', (len(row) * 120 + 20, 140), (0x24, 0x18, 0x30, 255))
        for i, im in enumerate(row):
            z = im.resize((im.width * 8, im.height * 8), Image.NEAREST)
            sheet.paste(z, (20 + i * 120, 20), z)
        path = os.path.join(ROOT, 'Temp', 'diamond_preview.png')
        sheet.save(path)
        print('preview', path)


if __name__ == '__main__':
    main()
