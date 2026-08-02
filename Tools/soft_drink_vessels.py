# -*- coding: utf-8 -*-
"""
The soft-drink shelf, generated on PixelLab (the author, 2026-08-03).

The juices, the cola, the energy drink and the soda water used to be glass bottles
wearing different label colours, and none of them read as what it was. They are now
what they would be behind a real bar: the juices come in CARTONS with their fruit
printed on the front, the cola in a big ribbed PET bottle, the energy drink in a
slim can, and the soda in a clear bottle.

The cartons and the can are opaque, and the cola and the soda came back drawn full,
so none of those has a drink painted into it by the game - which is the point of
putting a juice in a box. BottleArt lists those eight as sealed and the hover card
carries what is left in one. The house syrup went the other way: it was asked for as
EMPTY glass, so the game still draws the syrup inside it.

This file is the quantize chain, not the generator: the prompts are in
vessels_prompts.py, the four-candidate packs they returned are in vessels_raw, and
PICKS records which take was kept. It drops the ground shadow PixelLab likes to draw
under a vessel, trims, and rings the silhouette in ink so a bottle cannot sink into
the back bar's dark panelling.

    python Tools/soft_drink_vessels.py write
"""
from PIL import Image
import os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'vessels_raw')
CARTON_RAW = os.path.join(HERE, 'cartons_raw')
DEST = 'Assets/Resources/Items'
INK = (12, 10, 16, 255)

# style -> the candidate that was kept. A pack is four takes on one brief, and the
# fruit PixelLab reaches for is not always the fruit that was asked for, so the lemon
# was picked out of the lime's pack.
PICKS = {
    'cola': 'cola_2',
    'energy': 'energy_0',
    # the first soda had the transparency chequerboard painted into its glass; this
    # take carries far less of it (0.7% of the sprite against 2.4%)
    'soda': 'soda2_0',
    # The house syrup is the odd one out: it is asked for EMPTY, because it stands on
    # the back bar among spirits that all show their level, and a sealed one would be
    # the only bottle up there that never went down. It comes fitted with a pour spout,
    # so it needs no capless variant either.
    'syrup': 'syrup_1',
}


def largest_blob(im):
    """Only the vessel. PixelLab draws a shadow on the ground under it and sometimes a
    loose smear of pixels beside the base; neither belongs on a shelf."""
    W, H = im.size
    p = im.load()
    ok = [[p[x, y][3] > 40 for y in range(H)] for x in range(W)]
    seen = [[False] * H for _ in range(W)]
    best = []
    for sx in range(W):
        for sy in range(H):
            if not ok[sx][sy] or seen[sx][sy]:
                continue
            st, comp = [(sx, sy)], []
            seen[sx][sy] = True
            while st:
                x, y = st.pop()
                comp.append((x, y))
                for dx in (-1, 0, 1):
                    for dy in (-1, 0, 1):
                        nx, ny = x + dx, y + dy
                        if 0 <= nx < W and 0 <= ny < H and ok[nx][ny] and not seen[nx][ny]:
                            seen[nx][ny] = True
                            st.append((nx, ny))
            if len(comp) > len(best):
                best = comp
    out = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    op = out.load()
    for x, y in best:
        op[x, y] = p[x, y]
    return out


def ring(im, thickness=1):
    """Ink outside the existing edge. With the art's own dark outline that reads as
    two pixels, which is what keeps a vessel off a dark wall."""
    pad = thickness
    W, H = im.size
    src = im.load()
    out = Image.new('RGBA', (W + pad * 2, H + pad * 2), (0, 0, 0, 0))
    op = out.load()
    for x in range(W):
        for y in range(H):
            if src[x, y][3] > 40:
                op[x + pad, y + pad] = src[x, y]
    WO, HO = out.size
    solid = [[op[x, y][3] > 40 for y in range(HO)] for x in range(WO)]
    for x in range(WO):
        for y in range(HO):
            if solid[x][y]:
                continue
            near = any(0 <= x + dx < WO and 0 <= y + dy < HO and solid[x + dx][y + dy]
                       for dx in range(-thickness, thickness + 1)
                       for dy in range(-thickness, thickness + 1))
            if near:
                op[x, y] = INK
    return out


# The cartons were generated a second time (carton_prompts.py). The first set put the
# screw cap wherever it felt like - one hard right, one dead centre - and had no cap-off
# state at all; a carton's roof is flat and the cap is painted onto it, so no amount of
# measuring finds the cap across five different drawings. Both were asked for instead.
# The fruit a pack returns is still not always the fruit it was asked for: the lemon
# carton with its cap on the left came out of the lime's pack.
CARTONS = {
    # every take in the orange pack came back half again as wide as the rest of the
    # shelf, so the orange carton is the one from the lime's pack instead
    'orange': ('lime_shut_2', 'orange_open_0'),
    'lemon': ('lime_shut_1', 'lemon_open_0'),
    'lime': ('lime_shut_0', 'lime_open_0'),
    'pineapple': ('pineapple_shut_0', 'pineapple_open_2'),
    'cranberry': ('cranberry_shut_0', 'cranberry_open_2'),
}


def drop_shadow_bar(im):
    """PixelLab sometimes draws the vessel's ground shadow TOUCHING its base, where the
    largest-blob pass cannot tell the two apart. It gives itself away by its width: a
    carton does not suddenly get half again as wide in its last few rows."""
    W, H = im.size
    p = im.load()
    rows = [sum(1 for x in range(W) if p[x, y][3] > 40) for y in range(H)]
    body = [w for w in rows if w > 0]
    if not body:
        return im
    body.sort()
    typical = body[len(body) // 2]
    cut = H
    for y in range(H - 1, -1, -1):
        if rows[y] == 0:
            continue
        if rows[y] > typical * 1.35:
            cut = y
        else:
            break
    if cut >= H:
        return im
    for y in range(cut, H):
        for x in range(W):
            p[x, y] = (0, 0, 0, 0)
    return im


def build(style, raw=RAW, pick=None):
    src = Image.open(os.path.join(raw, (pick or PICKS[style]) + '.png')).convert('RGBA')
    im = drop_shadow_bar(largest_blob(src))
    bb = im.getbbox()
    if bb:
        im = im.crop(bb)
    return ring(im)


def run(write):
    for style in PICKS:
        im = build(style)
        if write:
            im.save(os.path.join(DEST, style + '.png'))
        print('%-10s %s' % (style, im.size))
    for style, (shut, opened) in CARTONS.items():
        a = build(style, CARTON_RAW, shut)
        b = build(style, CARTON_RAW, opened)
        if write:
            a.save(os.path.join(DEST, style + '.png'))
            b.save(os.path.join(DEST, style + '_open.png'))
        print('%-10s %s  open %s' % (style, a.size, b.size))
    print('%d vessels %s' % (len(PICKS) + len(CARTONS), 'written' if write else '(dry run)'))


if __name__ == '__main__':
    run(len(sys.argv) > 1 and sys.argv[1] == 'write')
