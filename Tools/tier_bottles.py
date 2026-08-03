# -*- coding: utf-8 -*-
"""
The upper tiers of every spirit, generated per BRAND on PixelLab (2026-08-03).

Astra, Boothby, Coral, Redline and Sonora own the shelf's approved art, and the three
brands above each of them used to wear the same bottle: a 48-dollar vodka looked
exactly like the house pour. Each of the fifteen now has its own vessel, and the
silhouettes climb with the price the way they do on a real back bar - a screw-capped
bottle at the bottom, a cut-crystal decanter with a gold stopper at the top.

Every one was asked for twice, shut and with its closure off, because the pour stage
shows the open shot and cropping the top off a bottle is not an open bottle.

The prompts are in tier_prompts.py, the four-candidate packs they returned are in
tiers_raw, and PICKS records which take was kept. This file is only the quantize
chain: it drops the ground shadow and the stopper PixelLab likes to leave lying beside
a bottle, and trims. The ink and the shared canvas come afterwards, from
uniform_outline.py, so a tier bottle carries exactly the edge every other vessel does.

    python Tools/tier_bottles.py write
"""
from PIL import Image
import os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'tiers_raw')
DEST = 'Assets/Resources/Items'

# brand -> (which shut take was kept, which open take). Where a pack put a chequerboard
# in the glass or laid the stopper on the ground beside the bottle, a cleaner take was
# chosen instead; the loose stopper is dropped by the blob pass anyway.
PICKS = {
    'bourbon_ashfall': (0, 3),
    'bourbon_hollow_oak': (1, 2),
    'bourbon_old_harrow': (2, 2),
    'gin_juniper_crown': (1, 1),
    'gin_thornwood': (2, 2),
    'gin_veilcrest': (2, 1),
    'rum_reina_del_mar': (3, 3),
    'rum_tidewater': (0, 0),
    'rum_windward': (2, 2),
    'tequila_alta_luna': (0, 0),
    'tequila_cielo_roto': (0, 0),
    'tequila_sol_viejo': (0, 3),
    'vodka_leonid': (2, 1),
    'vodka_okhta': (1, 3),
    'vodka_vor': (3, 1),
}


def largest_blob(im):
    """Only the bottle. PixelLab draws a shadow under it and, on the open shots, lays
    the stopper it took off on the ground beside it; neither belongs on a shelf."""
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


def drop_shadow_bar(im):
    """A shadow drawn TOUCHING the base cannot be told from the bottle by connection,
    but it can by width: a bottle does not get half again as wide in its last rows."""
    W, H = im.size
    p = im.load()
    rows = [sum(1 for x in range(W) if p[x, y][3] > 40) for y in range(H)]
    body = sorted(w for w in rows if w > 0)
    if not body:
        return im
    typical = body[len(body) // 2]
    cut = H
    for y in range(H - 1, -1, -1):
        if rows[y] == 0:
            continue
        if rows[y] > typical * 1.35:
            cut = y
        else:
            break
    for y in range(cut, H):
        for x in range(W):
            p[x, y] = (0, 0, 0, 0)
    return im


# The height every spirit bottle is brought to. On the wall a sprite renders at
# 110 x art/canvas, so two bottles are the same height on screen only if their ART is
# the same height in pixels - and the generator will not fill its frame to order. Asked
# to touch the top and bottom edges it came back between 0.79 and 0.99 of the frame,
# which put the tall ones 25% over the short ones. The approved tier-one bottles all
# sit at 156-164, so the generated ones are brought to the same place. It is a resample
# and the shelf resamples again on top of it, which is why it survives: at the size the
# player sees, a bottle scaled from 130 is indistinguishable from one drawn at 158.
STAND = 158


def build(brand, index, state):
    path = os.path.join(RAW, '%s_%s_%d.png' % (brand, state, index))
    im = drop_shadow_bar(largest_blob(Image.open(path).convert('RGBA')))
    bb = im.getbbox()
    if not bb:
        return im
    im = im.crop(bb)
    k = STAND / float(im.size[1])
    return im.resize((max(1, int(round(im.size[0] * k))), STAND), Image.NEAREST)


def run(write):
    for brand, (shut, opened) in PICKS.items():
        a = build(brand, shut, 'shut')
        b = build(brand, opened, 'open')
        if write:
            a.save(os.path.join(DEST, 'bot_%s.png' % brand))
            b.save(os.path.join(DEST, 'bot_%s_open.png' % brand))
        print('%-22s %-10s open %s' % (brand, '%dx%d' % a.size, '%dx%d' % b.size))
    print('%d brands %s' % (len(PICKS), 'written' if write else '(dry run)'))


if __name__ == '__main__':
    run(len(sys.argv) > 1 and sys.argv[1] == 'write')
