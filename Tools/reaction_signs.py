# -*- coding: utf-8 -*-
"""The crowd's reaction signs (2026-09-22, the author's seventh list: "emojileri sen oluştur ve sen ona göre
kullan ... emoji yerine kalp gibi ünlem gibi işaretler de kullanılabilir belki de daha iyi olabilir her durum
için").

One family, drawn by rule rather than by hand so every sign agrees with every other: a filled shape on the
game's palette, one ramp per sign (light / body / shade), lit from the upper left, shaded on the lower right,
and ringed in one pixel of the night ink. 16x16, shown at the stage's 1:1.

    py -3 -X utf8 Tools/reaction_signs.py            -> Assets/Resources/Emotes/sign_<id>.png + a preview sheet
"""
import math, os, sys
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Emotes')
N = 16

def hexc(v): return ((v >> 16) & 255, (v >> 8) & 255, v & 255, 255)
INK = hexc(0x0D0813)
RAMPS = {   # (shade, body, light), all from UITheme's ramps
    'magenta': (hexc(0x8F2464), hexc(0xE84DA6), hexc(0xFF7DC6)),
    'amber':   (hexc(0xC9822B), hexc(0xE8A33D), hexc(0xF5C97B)),
    'red':     (hexc(0x6E1B32), hexc(0xD9455C), hexc(0xF27D8A)),
    'cyan':    (hexc(0x26918F), hexc(0x3BC8BE), hexc(0x7DF0E3)),
    'blue':    (hexc(0x2E4699), hexc(0x4467CC), hexc(0x6E93F0)),
    'lime':    (hexc(0x479938), hexc(0x6FCC4B), hexc(0xA8F077)),
    'cream':   (hexc(0x9C8F80), hexc(0xC9BCA8), hexc(0xF2E8D5)),
    'graphite':(hexc(0x383D45), hexc(0x545A64), hexc(0x808893)),
}

def blank(): return [[0] * N for _ in range(N)]

def from_rows(rows):
    m = blank()
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            if ch == '#': m[y][x] = 1
    return m

def from_fn(f):
    m = blank()
    for y in range(N):
        for x in range(N):
            if f(x + 0.5, y + 0.5): m[y][x] = 1
    return m

def heart_mask(scale=1.0):
    def f(px, py):
        x = (px - 8.0) / (6.6 * scale); y = -(py - 8.6) / (6.6 * scale)
        return (x * x + y * y - 1) ** 3 - x * x * y ** 3 <= 0
    return from_fn(f)

def star_mask():
    pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5
        r = 7.2 if i % 2 == 0 else 3.1
        pts.append((8 + r * math.cos(a), 8.6 + r * math.sin(a)))
    def inside(px, py):
        c = False; j = len(pts) - 1
        for i in range(len(pts)):
            xi, yi = pts[i]; xj, yj = pts[j]
            if (yi > py) != (yj > py) and px < (xj - xi) * (py - yi) / (yj - yi) + xi: c = not c
            j = i
        return c
    return from_fn(inside)

def drop_mask():
    def f(px, py):
        x = px - 8; y = py - 9.5
        if y >= 0: return x * x + y * y <= 20
        return abs(x) <= (y + 8.5) * 0.53 and y > -8.5
    return from_fn(f)

def cloud_mask():
    circles = [(5.5, 8.5, 3.2), (9.0, 6.6, 3.8), (12.0, 9.0, 2.8), (8.0, 10.0, 3.0)]
    return from_fn(lambda x, y: any((x - cx) ** 2 + (y - cy) ** 2 <= r * r for cx, cy, r in circles) and y <= 12.3)

def disc_mask(r=6.6):
    return from_fn(lambda x, y: (x - 8) ** 2 + (y - 8) ** 2 <= r * r)

SIGNS = {}

# drawn, not solved: at sixteen pixels the heart curve has no lobes left and reads as a shield
HEART = from_rows([
    '................',
    '................',
    '...####..####...',
    '..############..',
    '.##############.',
    '.##############.',
    '.##############.',
    '..############..',
    '...##########...',
    '....########....',
    '.....######.....',
    '......####......',
    '.......##.......',
    '................',
    '................',
    '................'])
SIGNS['heart'] = ('magenta', HEART, None)
SIGNS['star'] = ('amber', star_mask(), None)
SIGNS['drop'] = ('cyan', drop_mask(), None)
SIGNS['exclaim'] = ('amber', from_rows([
    '................',
    '......####......',
    '......####......',
    '......####......',
    '......####......',
    '......####......',
    '.......##.......',
    '.......##.......',
    '.......##.......',
    '................',
    '................',
    '......####......',
    '......####......',
    '................',
    '................',
    '................']), None)
SIGNS['question'] = ('blue', from_rows([
    '................',
    '.....#####......',
    '....#######.....',
    '...###...###....',
    '...##.....##....',
    '..........##....',
    '.........###....',
    '.......####.....',
    '......###.......',
    '......##........',
    '................',
    '......###.......',
    '......###.......',
    '................',
    '................',
    '................']), None)
SIGNS['note'] = ('cyan', from_rows([
    '................',
    '........#.......',
    '........###.....',
    '........####....',
    '........#.###...',
    '........#..##...',
    '........#...#...',
    '........#.......',
    '........#.......',
    '....#####.......',
    '...######.......',
    '...######.......',
    '....####........',
    '................',
    '................',
    '................']), None)
# the manga anger mark: four arcs bowing out of one centre
def anger_mask():
    # the manga anger mark: four quarter-arcs, each centred on a far corner, so they bow in toward the middle
    m = blank()
    for cx, cy in ((1.0, 1.0), (15.0, 1.0), (1.0, 15.0), (15.0, 15.0)):
        for y in range(N):
            for x in range(N):
                px, py = x + 0.5, y + 0.5
                if (px < 8) != (cx < 8) or (py < 8) != (cy < 8): continue
                if abs(px - 8) < 1.6 or abs(py - 8) < 1.6: continue
                d = math.hypot(px - cx, py - cy)
                if 4.2 <= d <= 7.3: m[y][x] = 1
    return m
SIGNS['anger'] = ('red', anger_mask(), None)
# a broken heart: the heart with a zig-zag cut down its middle
bh = [row[:] for row in HEART]
for y, x in [(2, 8), (3, 8), (4, 7), (5, 7), (6, 8), (7, 9), (8, 9), (9, 8), (10, 7), (11, 8), (12, 8), (3, 7), (6, 7), (8, 8), (10, 8)]:
    bh[y][x] = 0
SIGNS['broken'] = ('red', bh, None)
# the storm: a grey cloud and an amber bolt under it (two ramps, drawn as two layers)
bolt = from_rows([
    '................',
    '................',
    '................',
    '................',
    '................',
    '................',
    '................',
    '................',
    '................',
    '................',
    '.........##.....',
    '........##......',
    '.......#####....',
    '.........##.....',
    '........##......',
    '.......#........'])
SIGNS['storm'] = ('graphite', cloud_mask(), ('amber', bolt))
# the angry face: a red disc, and the features cut into it in ink
SIGNS['angry'] = ('red', disc_mask(), 'FACE_ANGRY')
# the glad face, same family, for the moments that want a face at all
SIGNS['glad'] = ('amber', disc_mask(), 'FACE_GLAD')

FACES = {
    'FACE_ANGRY': [(4, 5), (5, 6), (6, 7), (11, 5), (10, 6), (9, 7),      # brows, pulled down to the middle
                   (5, 8), (6, 8), (10, 8), (9, 8),                       # eyes
                   (5, 12), (6, 11), (7, 11), (8, 11), (9, 11), (10, 12)],  # the mouth, turned down
    'FACE_GLAD': [(5, 6), (5, 7), (10, 6), (10, 7),                        # eyes
                  (4, 9), (5, 10), (6, 11), (7, 11), (8, 11), (9, 11), (10, 10), (11, 9)],   # the smile
}

def paint(mask, ramp):
    shade, body, light = RAMPS[ramp]
    img = [[None] * N for _ in range(N)]
    on = lambda x, y: 0 <= x < N and 0 <= y < N and mask[y][x]
    for y in range(N):
        for x in range(N):
            if not mask[y][x]: continue
            c = body
            if not on(x - 1, y) or not on(x, y - 1): c = light        # lit from the upper left
            if not on(x + 1, y) or not on(x, y + 1): c = shade        # shaded on the lower right
            if not on(x - 1, y) and not on(x + 1, y + 1): c = light
            img[y][x] = c
    return img

def outline(layers):
    anyon = [[any(l[y][x] is not None for l in layers) for x in range(N)] for y in range(N)]
    ink = [[None] * N for _ in range(N)]
    for y in range(N):
        for x in range(N):
            if anyon[y][x]: continue
            if any(0 <= x + dx < N and 0 <= y + dy < N and anyon[y + dy][x + dx]
                   for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy):
                ink[y][x] = INK
    return ink

def build(sid):
    ramp, mask, extra = SIGNS[sid]
    layers = [paint(mask, ramp)]
    if isinstance(extra, tuple):
        layers.append(paint(extra[1], extra[0]))
    ink = outline(layers)
    im = Image.new('RGBA', (N, N), (0, 0, 0, 0))
    for y in range(N):
        for x in range(N):
            c = ink[y][x]
            for l in layers:
                if l[y][x] is not None: c = l[y][x]
            if c: im.putpixel((x, y), c)
    if isinstance(extra, str):
        for x, y in FACES[extra]: im.putpixel((x, y), INK)
    return im

def main():
    os.makedirs(OUT, exist_ok=True)
    sheet = Image.new('RGBA', (len(SIGNS) * (N * 6 + 12) + 12, N * 6 + 24), (29, 24, 35, 255))
    for i, sid in enumerate(SIGNS):
        im = build(sid)
        im.save(os.path.join(OUT, 'sign_%s.png' % sid))
        sheet.alpha_composite(im.resize((N * 6, N * 6), Image.NEAREST), (12 + i * (N * 6 + 12), 12))
    if len(sys.argv) > 1: sheet.save(sys.argv[1])
    print('signs:', ', '.join(SIGNS))

if __name__ == '__main__':
    main()
