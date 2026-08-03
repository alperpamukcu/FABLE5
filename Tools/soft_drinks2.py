# -*- coding: utf-8 -*-
"""
The soft drinks, mixers and garnish vessels, regenerated with the tier bottles' brief
(2026-08-03).

The first soft-drink round predates the 2.5D language, and next to the tier bottles
the cartons and the can read as paper cut-outs. Every vessel was asked for again with
the viewpoint spelled out (seen from slightly above, elliptical tops, one visible side
face on the cartons) and the proportion stated as a number - and every one was asked
for OPEN as well, because the author's rule is now one sentence: every vessel on the
shelf shares one perspective and every one has an open state.

Two content rules ride along. Soda and cola are generated FULL - the game never draws
a level into them (BottleArt.Sealed), and an empty clear bottle is where the generator
paints its transparency chequer, which is exactly the grey squares the author kept
reporting. Tonic is generated EMPTY, because the game does draw its level.

The raw four-take packs are in soft_raw; PICKS records what was kept. The chain here
is tier_bottles' chain: the biggest blob, the ground shadow dropped, the art stood to
its class height. Ink and canvas come from uniform_outline.py afterwards, so every
vessel carries exactly the edge the rest of the shelf does.

    python Tools/soft_drinks2.py write
"""
from PIL import Image
import os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'soft_raw')
DEST = 'Assets/Resources/Items'

# What height each class of vessel stands at, in art pixels. The bottles match the
# spirits (158); a can is shorter than a bottle, a carton shorter still, a garnish jar
# is a small thing, and the tray pieces keep the sizes the prep tray was laid out for.
STAND = {
    'soda': 158, 'tonic': 158, 'cola': 158,
    'energy': 108,
    'orange': 122, 'lemon': 122, 'lime': 122, 'pineapple': 122, 'cranberry': 122,
    'mint': 110, 'olive': 110,
    'ice': 64, 'salt': 40, 'sugar': 40, 'prep_lemon': 54,
}

# id -> (shut take, open take). The numbers were scored first (chequer, stray blobs,
# silhouette mirror-difference - the cartons exempt from symmetry, their side face is
# asymmetry on purpose) and then picked by EYE off contact sheets, because the scorer
# is colour-blind: it ranked an amber soda over the pale blue one and an orange cola
# over the dark one, and on the cartons it cannot see that a take grew the wrong
# fruit - each carton pack obliged the requested fruit about one take in four.
PICKS = {
    'soda': (0, None),
    'tonic': (1, None),
    'cola': (0, None),
    'energy': (0, 0),
    'orange': (0, 0),
    'lemon': (0, 0),
    'lime': (0, 0),
    'pineapple': (0, 0),
    'cranberry': (0, 1),
    'mint': (0, 0),
    'olive': (1, 0),
    'ice': (0, None),
    'salt': (0, None),
    'sugar': (0, None),
    'prep_lemon': (0, None),
}

TWO_STATE = {'soda', 'tonic', 'cola', 'energy', 'mint', 'olive',
             'orange', 'lemon', 'lime', 'pineapple', 'cranberry'}

# Bottles whose open state is DERIVED from the shut art (bottle_open_states.py, mode
# "named") instead of generated: a separately generated open is a different bottle
# often enough that the pour stage stopped matching the shelf, and these three are
# plain bottles whose closure the seam finder handles. The can, the jars and the
# gable-top cartons keep their generated opens - a pulled tab, leaves poking out of
# a jar mouth and an open spout are things the deriver cannot draw.
DERIVED = {'soda', 'tonic', 'cola'}


def largest_blob(im):
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


def build(vid, state, index):
    path = os.path.join(RAW, '%s_%s_%d.png' % (vid, state, index))
    im = drop_shadow_bar(largest_blob(Image.open(path).convert('RGBA')))
    bb = im.getbbox()
    if not bb:
        return im
    im = im.crop(bb)
    stand = STAND[vid]
    k = stand / float(im.size[1])
    return im.resize((max(1, int(round(im.size[0] * k))), stand), Image.NEAREST)


def run(write):
    for vid, (shut, opened) in sorted(PICKS.items()):
        a = build(vid, 'shut', shut)
        b = build(vid, 'open', opened) if vid in TWO_STATE and opened is not None else None
        if write:
            a.save(os.path.join(DEST, '%s.png' % vid))
            if b is not None:
                b.save(os.path.join(DEST, '%s_open.png' % vid))
        tail = '  open %dx%d' % b.size if b is not None else \
               ('  open derived' if vid in DERIVED else '')
        print('%-12s %-9s%s' % (vid, '%dx%d' % a.size, tail))
    print('%d vessels %s' % (len(PICKS), 'written' if write else '(dry run)'))


if __name__ == '__main__':
    run(len(sys.argv) > 1 and sys.argv[1] == 'write')
