# -*- coding: utf-8 -*-
"""
Open states for the gable-top cartons, derived from the shut art (2026-08-03).

The cartons' open shots were generated as separate takes at first, and the pack drew a
DIFFERENT carton open than shut every single time - washed board colours, the fruit
print faded to a watermark - so the pour stage showed a stranger wearing the same name
(the author, with screenshots, same day the tier bottles had the identical fault). The
bottles' cure applies: derive the open state from the shut art, and the two are the
same carton by construction.

A carton does not open the way a bottle does. The screw cap on the LEFT gable slope
comes off and leaves its threaded collar with a dark pour hole in it; everything else
about the carton - board, fold lines, fruit - stays exactly as drawn. So the surgery
is local: find the cap disc, keep its outline as the collar, and sink its interior
into a bore.

The cap is found, not hand-tabled: the one compact round blob in the gable region
whose colour is alien to the board. A proof sheet comes out next to the write so the
found discs can be eyeballed - the first detector took the whole sunlit gable face of
three cartons for their caps, which is why the shape gates exist.

    python Tools/carton_open_states.py write
"""
from PIL import Image
import os, sys

DEST = 'Assets/Resources/Items'
CARTONS = ['orange', 'lemon', 'lime', 'pineapple', 'cranberry']


def lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def find_cap(im):
    """The cap disc: compact, roughly round, 8-30px, colour far from the board's."""
    W, H = im.size
    p = im.load()
    ys = [y for y in range(H) if any(p[x, y][3] >= 128 for x in range(W))]
    top, bot = ys[0], ys[-1]
    gable_end = top + int((bot - top) * 0.30)
    tally = {}
    for y in range(top + (bot - top) // 3, top + (bot - top) * 2 // 3):
        for x in range(W):
            if p[x, y][3] < 128:
                continue
            tally[p[x, y][:3]] = tally.get(p[x, y][:3], 0) + 1
    board = max(tally, key=tally.get)

    import colorsys

    def hue(c):
        return colorsys.rgb_to_hsv(c[0] / 255.0, c[1] / 255.0, c[2] / 255.0)[0]

    board_hue = hue(board)

    def alien(c):
        """Not the board and not the board lit: the sunlit gable face keeps the board's
        HUE, only brighter, and merging with it is what swallowed two caps whole. A cap
        is either another hue entirely (the olive cap on the orange carton) or plain
        metal with no hue to speak of."""
        if sum(abs(c[i] - board[i]) for i in range(3)) <= 100:
            return False
        if max(c) - min(c) < 30:
            return True                       # achromatic: silver, white, steel
        d = abs(hue(c) - board_hue)
        return min(d, 1.0 - d) > 0.11

    rows = range(top, gable_end + 1)
    mask = {(x, y) for y in rows for x in range(W)
            if p[x, y][3] >= 128 and alien(p[x, y][:3])}
    seen, discs = set(), []
    for start in mask:
        if start in seen:
            continue
        st, comp = [start], []
        seen.add(start)
        while st:
            x, y = st.pop()
            comp.append((x, y))
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    n = (x + dx, y + dy)
                    if n in mask and n not in seen:
                        seen.add(n)
                        st.append(n)
        xs = [x for x, _ in comp]
        yy = [y for _, y in comp]
        w, h = max(xs) - min(xs) + 1, max(yy) - min(yy) + 1
        if not (6 <= w <= 30 and 6 <= h <= 30):
            continue
        if not (0.5 <= w / float(h) <= 2.0):
            continue
        if len(comp) < w * h * 0.45:
            continue
        discs.append((len(comp), comp))
    if not discs:
        return None, board
    return max(discs)[1], board


def open_variant(im):
    cap, board = find_cap(im)
    if cap is None:
        return None
    out = im.copy()
    p = out.load()
    xs = [x for x, _ in cap]
    yy = [y for _, y in cap]
    cx, cy = (min(xs) + max(xs)) / 2.0, (min(yy) + max(yy)) / 2.0
    rx = (max(xs) - min(xs) + 1) / 2.0
    ry = (max(yy) - min(yy) + 1) / 2.0
    bore = tuple(int(v * 0.22) for v in board)
    collar = tuple(min(255, int(v * 0.72)) for v in board)
    for x, y in cap:
        c = p[x, y][:3]
        if lum(c) < 60:
            continue                          # the artist's outline stays the outline
        d = ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2
        if d <= 0.45:
            p[x, y] = bore + (255,)           # the pour hole
        else:
            p[x, y] = collar + (255,)         # the threaded collar the cap left behind
    # the far rim of the hole catches the light, which is what says "open" at 14px
    for x, y in cap:
        d = ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2
        if 0.45 < d <= 0.80 and y < cy - ry * 0.25 and lum(p[x, y][:3]) >= 60:
            p[x, y] = tuple(min(255, int(v * 1.18)) for v in collar) + (255,)
    return out


def run(write):
    proof = []
    for name in CARTONS:
        path = os.path.join(DEST, name + '.png')
        im = Image.open(path).convert('RGBA')
        o = open_variant(im)
        if o is None:
            print('%-10s NO CAP FOUND — untouched' % name)
            continue
        if write:
            o.save(os.path.join(DEST, name + '_open.png'))
        proof.append((name, im, o))
        print('%-10s open written from shut art' % name)
    if not proof:
        return
    scale = 3
    cw = max(im.size[0] for _, im, _ in proof) * scale * 2 + 30
    ch = max(im.size[1] for _, im, _ in proof) * scale + 16
    sheet = Image.new('RGBA', (cw, ch * len(proof)), (16, 26, 38, 255))
    for i, (name, a, b) in enumerate(proof):
        for j, im in enumerate((a, b)):
            up = im.resize((im.size[0] * scale, im.size[1] * scale), Image.NEAREST)
            sheet.alpha_composite(up, (10 + j * (cw // 2), i * ch + 8))
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'carton_open_proof.png')
    sheet.save(out)
    print('proof: %s' % out)


if __name__ == '__main__':
    run(len(sys.argv) > 1 and sys.argv[1] == 'write')
