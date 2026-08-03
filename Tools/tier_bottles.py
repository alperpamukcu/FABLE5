# -*- coding: utf-8 -*-
"""
The upper tiers of every spirit, generated per BRAND on PixelLab (2026-08-03).

Astra, Boothby, Coral, Redline and Sonora own the shelf's approved art, and the three
brands above each of them used to wear the same bottle: a 48-dollar vodka looked
exactly like the house pour. Each of the fifteen now has its own vessel, and the
silhouettes climb with the price the way they do on a real back bar - a screw-capped
bottle at the bottom, a cut-crystal decanter with a gold stopper at the top.

Every one was asked for twice, shut and with its closure off — and the open shots are
NOT installed. The packs drew a different bottle open than shut often enough that the
pour stage stopped matching the shelf (the author, 2026-08-03, with screenshots:
Thornwood was a shaker-shaped bottle on the wall and a slim wine bottle in the hand;
a width check had passed the pair at -6% because width cannot see shape). The open
states are DERIVED from the installed shut art by bottle_open_states.py, which makes
the two the same bottle by construction; the pack opens stay in tiers_raw as reference.

The prompts are in tier_prompts.py, the four-candidate packs they returned are in
tiers_raw, and PICKS records which shut take was kept. This file is only the quantize
chain: it drops the ground shadow and the stopper PixelLab likes to leave lying beside
a bottle, and trims. The chain order matters:

    python Tools/tier_bottles.py write              the shut bottles, from the raws
    python Tools/bottle_open_states.py write bots   their opens, derived from those
    python Tools/uniform_outline.py write           one ink and one canvas for all
"""
from PIL import Image
import os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'tiers_raw')
DEST = 'Assets/Resources/Items'

# brand -> (which shut take was kept, which open take).
#
# The first pass picked on how much of the frame a take filled and whether a stray
# stopper lay beside it. That got the heights and the strays right and said nothing
# about the thing the user actually complained about, which is whether a bottle reads
# as round or as a cut-out. Two attempts at scoring that automatically are not in this
# file, and should not be: counting tones per row ranked the cut-crystal decanter the
# roundest object on the shelf (facets are not form), and correlating each row against
# the mean column profile ranked the APPROVED gin as flat. The four takes are already
# paid for, so they were laid out side by side with the shipped bottle and picked by
# eye - which is how every art call in this project has actually been made.
#
# What the eye threw out: a chequerboard where the glass should be (alta_luna wore one
# on the shelf), a body so dark and mottled it had no form left (hollow_oak, windward),
# a wax capsule rendered as a black blob with drips, and a bottle with no label at all.
#
# Takes 0-3 are the pack a brand came back with; 4-7 are the round asked for again on
# 2026-08-03, for the four brands where all four of the originals failed the proportion
# test below. The number says which round it came from, which is why they share a
# namespace. Only the SHUT take is recorded: the open art is derived, not picked.
PICKS = {
    'bourbon_ashfall': 4,
    'bourbon_hollow_oak': 4,
    'bourbon_old_harrow': 2,
    'gin_juniper_crown': 1,
    'gin_thornwood': 0,
    'gin_veilcrest': 0,
    'rum_reina_del_mar': 1,
    'rum_tidewater': 4,
    'rum_windward': 1,
    'tequila_alta_luna': 1,
    'tequila_cielo_roto': 0,
    'tequila_sol_viejo': 3,
    'vodka_leonid': 5,
    'vodka_okhta': 1,
    'vodka_vor': 3,
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

# Pinning the height leaves the WIDTH free, and that is the next thing the eye catches:
# a squat take stretched to the shared height arrives on the shelf as a jug standing
# next to a stick. The approved five run 1.8:1 to 2.6:1 tall-to-wide, so a take outside
# that band is reported - four brands came back with all four takes outside it and had
# to be asked for again with the proportion written into the brief. The band is the
# approved range (gin is the widest at 1.82, tequila the narrowest at 2.59) opened a
# little, not a round number chosen to be tidy.
SLIM, STOUT = 3.1, 1.75

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
    off = 0
    for brand, shut in sorted(PICKS.items()):
        a = build(brand, shut, 'shut')
        if write:
            a.save(os.path.join(DEST, 'bot_%s.png' % brand))
        ratio = a.size[1] / float(a.size[0])
        bad = ratio > SLIM or ratio < STOUT
        off += bad
        print('%-22s %-9s %.1f:1%s' % (brand, '%dx%d' % a.size, ratio,
                                       '  proportion' if bad else ''))
    print('%d brands %s, %d outside %.2f-%.2f tall-to-wide; opens are derived, '
          'run bottle_open_states next'
          % (len(PICKS), 'written' if write else '(dry run)', off, STOUT, SLIM))


if __name__ == '__main__':
    run(len(sys.argv) > 1 and sys.argv[1] == 'write')
