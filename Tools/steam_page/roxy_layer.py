# -*- coding: utf-8 -*-
"""ROXY'S KEY-ART LAYER: one cut-out, used by every store and library picture.

Source: the Nano Banana Pro main-capsule take (out/nano/raw/main_capsule_pro_1.png) snapped to its true grid by
pixelsnap (479x266 native). Roxy is lifted with GrabCut (run on a 4x nearest enlargement so its edges land on the
native grid), then: interior holes filled (eyes, teeth, hoops), only the largest piece kept (no shelf bottles),
the coupe restored by its outline polygon (its glass is see-through, GrabCut drops it), and everything below the
counter line removed - the key art's own counter covers her from the waist down. Her hand rests on that counter.

    python3 roxy_layer.py      ->  out/layers/roxy_coupe.png (RGBA, native pixels) + roxy_coupe.json (anchors)
"""
import json, os
import numpy as np
import cv2
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, 'out', 'nano', 'snap_main_capsule_pro_1.png')
OUT = os.path.join(HERE, 'out', 'layers')
RECT = (245, 22, 452, 236)          # x0, y0, x1, y1 in native pixels
COUNTER_Y = 233                     # the source's counter top under her resting hand
GLASS = [  # polygons (native) that make up the coupe: bowl, stem, foot
    [(413, 97), (448, 97), (436, 118), (422, 118)],
    [(424, 116), (432, 116), (432, 141), (424, 141)],
    [(418, 140), (435, 140), (435, 147), (418, 147)],
]
# shelf bottles that touch her arm: inside these boxes (native) a pixel whose blue beats its green is glass or
# bottle, never skin, hair or her outline (all warm) - it goes, unless it is the coupe
# hand-checked strays (native source px, x0, y0, x1, y1): two shelf-edge strands that hang under her elbow
ERASE = [(414, 178, 420, 201), (413, 184, 414, 201), (403, 192, 410, 199), (413, 183, 414, 184)]
SCRUB = [(396, 70, 414, 180), (400, 176, 422, 205), (435, 136, 452, 160), (405, 92, 414, 101)]


def fill_holes(m):
    h, w = m.shape
    ff = (m * 255).astype(np.uint8).copy()
    msk = np.zeros((h + 2, w + 2), np.uint8)
    cv2.floodFill(ff, msk, (0, 0), 128)
    return (ff != 128).astype(np.uint8)


def _lum(c):
    return c[..., 0] * 0.299 + c[..., 1] * 0.587 + c[..., 2] * 0.114


def cleanup(a):
    """Hand-checked fixes on the cut-out (layer coordinates), from the critique pass:
    the source counter's shadow under the resting hand, one-neighbour spurs, the eyes' grey-blue noise,
    and the lapels one step lighter so the V stops out-shouting the face."""
    a = a.copy()
    rgb = a[..., :3].astype(int)
    # (b) the source counter's shadow slab under the resting hand
    box = np.zeros(a.shape[:2], bool); box[196:203, 0:46] = True
    slab = box & (np.abs(rgb - np.array([0x3C, 0x26, 0x38])).sum(-1) < 30) | \
        box & (np.abs(rgb - np.array([0x38, 0x07, 0x31])).sum(-1) < 30)
    a[slab, 3] = 0
    # (c) spurs: an opaque texel with at most one opaque 4-neighbour goes, until none is left
    while True:
        o = a[..., 3] > 0
        n = np.zeros(o.shape, int)
        n[1:] += o[:-1]; n[:-1] += o[1:]; n[:, 1:] += o[:, :-1]; n[:, :-1] += o[:, 1:]
        spur = o & (n <= 1)
        if not spur.any():
            break
        a[spur, 3] = 0
    # the eyes: light texels become the sclera, cool ones the iris (no grey-blue left on the face)
    for x0, y0, x1, y1 in ((80, 38, 93, 44), (102, 37, 117, 44)):
        sub = a[y0:y1, x0:x1]
        c = sub[..., :3].astype(int)
        lum = _lum(c)
        light = (lum > 140) & (np.abs(c[..., 0] - c[..., 2]) < 70)     # grey/cream, never skin
        cool = (c[..., 2] > c[..., 0] + 6) & ~light
        sub[light, :3] = (0xF2, 0xE8, 0xD5)
        sub[cool, :3] = (0x3C, 0x26, 0x38)
    # the lapels: their near-black inks one step up the Night ramp, the darkest kept as the outline
    c = a[..., :3].astype(int)
    lum = _lum(c)
    lap = np.zeros(a.shape[:2], bool); lap[70:140, 55:150] = True
    neutral = np.abs(c[..., 0] - c[..., 2]) < 18                 # the satin is grey-violet, the hair is red
    dark = lap & neutral & (lum < 48) & (a[..., 3] > 0)
    a[dark & (lum >= 22), :3] = (0x36, 0x24, 0x47)
    a[dark & (lum < 22), :3] = (0x24, 0x18, 0x30)
    return a


def main():
    n = np.asarray(Image.open(SRC).convert('RGB'))
    H, W = n.shape[:2]
    k = 4
    big = cv2.resize(n, (W * k, H * k), interpolation=cv2.INTER_NEAREST)
    mask = np.zeros(big.shape[:2], np.uint8)
    x0, y0, x1, y1 = RECT
    bgd = np.zeros((1, 65), np.float64); fgd = np.zeros((1, 65), np.float64)
    cv2.grabCut(cv2.cvtColor(big, cv2.COLOR_RGB2BGR), mask, (x0 * k, y0 * k, (x1 - x0) * k, (y1 - y0) * k),
                bgd, fgd, 8, cv2.GC_INIT_WITH_RECT)
    fg = ((mask == 1) | (mask == 3)).astype(np.uint8)
    m = (cv2.resize(fg.astype(np.float32), (W, H), interpolation=cv2.INTER_AREA) > 0.5).astype(np.uint8)
    for poly in GLASS:
        cv2.fillPoly(m, [np.array(poly, np.int32)], 1)
    m[COUNTER_Y:] = 0
    glass = np.zeros_like(m)
    for poly in GLASS:
        cv2.fillPoly(glass, [np.array(poly, np.int32)], 1)
    r, g, b = [n[..., i].astype(int) for i in range(3)]
    cool = (b > g + 8) | ((r < 140) & (g < 120) & (b > 90))
    for sx0, sy0, sx1, sy1 in SCRUB:
        box = np.zeros_like(m); box[sy0:sy1, sx0:sx1] = 1
        m[(box == 1) & cool & (glass == 0)] = 0
    # largest piece only, then its holes
    cnt, lab, stats, _ = cv2.connectedComponentsWithStats(m, connectivity=4)
    keep = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
    m = (lab == keep).astype(np.uint8)
    m = fill_holes(m)
    m[COUNTER_Y:] = 0
    for ex0, ey0, ex1, ey1 in ERASE:
        m[ey0:ey1, ex0:ex1] = 0
    ys, xs = np.nonzero(m)
    bx0, by0, bx1, by1 = xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
    rgba = np.dstack([n, m * 255]).astype(np.uint8)[by0:by1, bx0:bx1]
    rgba = cleanup(rgba)
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray(rgba, 'RGBA').save(os.path.join(OUT, 'roxy_coupe.png'))
    anchors = {'size': [int(bx1 - bx0), int(by1 - by0)], 'counter_y': int(COUNTER_Y - by0),
               'face': [int(355 - bx0), int(75 - by0)], 'source': os.path.basename(SRC)}
    json.dump(anchors, open(os.path.join(OUT, 'roxy_coupe.json'), 'w'), indent=1)
    print('roxy_coupe.png', anchors)


if __name__ == '__main__':
    main()
