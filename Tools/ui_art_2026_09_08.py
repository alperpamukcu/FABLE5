# -*- coding: utf-8 -*-
"""The author's 2026-09-08 art, cut into the pieces the game loads (nothing here is generated;
every pixel comes from the author's own files on the Desktop, copied into Resources first).

  SPEECH  The author gave ten 32x32 white bubbles: tails below (1-3), above (4-6), left (7-9)
          and none (10). The balloon over a head is a 9-sliced BODY plus a TAIL the layout moves
          along the underside (TycoonHud.Seats.SeparateSays), so:
            speech_body.png  = bubble_white_10 as it is; sliced with an 8px border so the
                               rounded corners never stretch (the body spans 4..27, corners ~4)
            speech_tail.png  = the tail cut out of bubble_white_2 with the body's bottom outline
                               row kept on top of it, so it fuses with the body when overlapped
                               by one pixel

  CURSOR  Hand3.png is a 16x15 drawing shown at 3x (48x45), an index finger pointing up. The
          author asked for a PRESSED and a GRAB frame as well. Both are cut from the same
          drawing at the 16x15 cell grid and re-scaled 3x, so they are the same hand:
            cursor_hand.png          the drawing as given (hotspot: the fingertip, 28,0)
            cursor_hand_pressed.png  the hand one cell lower and the finger one cell shorter -
                                     a press is a finger that has gone down
            cursor_hand_grab.png     the index finger folded into the fist: the finger's cells
                                     above the knuckle removed and the knuckle capped, which is
                                     what a hand closing on something looks like at this size

  py -3 -X utf8 Tools/ui_art_2026_09_08.py
"""
import os

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')


def speech():
    body = Image.open(os.path.join(ITEMS, 'bubble_white_10.png')).convert('RGBA')
    body.save(os.path.join(ITEMS, 'speech_body.png'))
    src = Image.open(os.path.join(ITEMS, 'bubble_white_2.png')).convert('RGBA')
    # the tail of bubble 2 sits in columns 9..16, rows 26..31; row 26/27 is the body's bottom
    # outline, kept so the tail carries its own join
    tail = src.crop((8, 26, 18, 32))
    tail.save(os.path.join(ITEMS, 'speech_tail.png'))
    print('speech_body 32x32 (slice 8), speech_tail %dx%d' % tail.size)


def cells(im):
    """The 16x15 cell grid under the 3x drawing: each cell is the top-left pixel of its 3x3."""
    w, h = im.size
    px = im.load()
    return [[px[x * 3, y * 3] for x in range(w // 3)] for y in range(h // 3)]


def blow(grid, k=2, canvas=(32, 32)):
    """The cell grid at k x, on a canvas — 32x32, because that is the one size a Windows
    HARDWARE cursor takes: handed the 3x drawing (48x45), Unity scaled it down and the
    hotspot with it, so the click landed near the middle of the hand; drawn by Unity as a
    software cursor instead, the hand was captured in every screenshot and the look tests
    went red on it (2026-09-08). At 2x the 16x15 drawing is 32x30, a whole multiple."""
    h, w = len(grid), len(grid[0])
    out = Image.new('RGBA', canvas, (0, 0, 0, 0))
    o = out.load()
    for y in range(h):
        for x in range(w):
            for dy in range(k):
                for dx in range(k):
                    if x * k + dx < canvas[0] and y * k + dy < canvas[1]:
                        o[x * k + dx, y * k + dy] = grid[y][x]
    return out


def cursors():
    src = os.path.join(ROOT, 'Tools', 'cursor_hand_3x.png')   # the author's Hand3 at 3x, the source
    hand = Image.open(src).convert('RGBA')
    g = cells(hand)                      # 15 rows x 16 cols
    H, W = len(g), len(g[0])
    clear = (0, 0, 0, 0)

    def ink_of():
        for row in g:
            for c in row:
                if c[3] > 128 and sum(c[:3]) < 150:
                    return c
        return (0, 0, 0, 255)
    ink = ink_of()

    # PRESSED: everything one cell down, the finger's top cell gone (rows 0..2 of the finger
    # column are the fingertip; drop its first cell), the rest as drawn.
    pressed = [[clear] * W for _ in range(H)]
    for y in range(H - 1):
        pressed[y + 1] = list(g[y])
    # the fingertip is the topmost inked cell; cap it one lower
    for x in range(W):
        if g[0][x][3] > 128:
            pressed[1][x] = clear
    # re-cap the finger: the cell now at the top of the finger becomes outline
    for x in range(W):
        if pressed[2][x][3] > 128 and pressed[1][x][3] <= 128:
            pressed[2][x] = ink
    blow(pressed).save(os.path.join(ITEMS, 'cursor_hand_pressed.png'))
    blow(g).save(os.path.join(ITEMS, 'cursor_hand.png'))
    tip = [x for x in range(W) if g[0][x][3] > 128]
    print('fingertip cells x %s -> hotspot at 2x (%d, 0)' % (tip, (min(tip) + max(tip) + 1)))

    # GRAB: the index finger folded. The finger is the inked run in the top rows between the
    # thumb (left) and the fist (right): rows 0..2 hold the fingertip, rows 3..5 the finger's
    # length above the knuckle line (row 3 is where the fist's outline begins). Everything of
    # the finger above the fist's top outline goes, and the gap is capped with outline.
    grab = [list(r) for r in g]
    finger_cols = [x for x in range(W) if g[0][x][3] > 128]
    if finger_cols:
        x0, x1 = min(finger_cols), max(finger_cols)
        # The palm's top outline runs across cell row 3 ("###ooooooooooooooo###" in the
        # drawing); the finger stands three cells above it. Measured off the grid rather than
        # found: the thumb also starts higher than the palm, so its first inked row is not
        # the fist's top (the first cut of this folded one cell and left the finger standing).
        top_of_fist = 3
        for y in range(top_of_fist):
            for x in range(x0 - 1, x1 + 2):
                if 0 <= x < W:
                    grab[y][x] = clear
        # cap: the row at top_of_fist across the finger's columns becomes outline
        for x in range(x0 - 1, x1 + 2):
            if 0 <= x < W and grab[top_of_fist][x][3] > 128:
                grab[top_of_fist][x] = ink
    blow(grab).save(os.path.join(ITEMS, 'cursor_hand_grab.png'))
    print('cursor_hand / _pressed / _grab written at 2x on 32x32')


if __name__ == '__main__':
    speech()
    cursors()
