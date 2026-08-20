# -*- coding: utf-8 -*-
"""The pressed key, built from the idle one rather than generated (2026-08-21).

A raised key and a pressed key are the same geometry with the light on the other side.
That is not an approximation - it is how the shape works: the lit lip is lit because it
faces up, so pressing the key turns it away and lights the lower lip instead. Deriving it
costs nothing, cannot drift from the idle art, and stays correct if the idle key is ever
redrawn.

Generating a second key would have cost 40 and produced a DIFFERENT button that has to be
matched by eye. This project has already learned that lesson twice - the pressed plate and
the tab pair both live under it - so the pressed state is computed.

What the transform does, by role rather than by pixel:
    the white lit lip        -> the mid side tone      (the top edge stops facing the light)
    the face                 -> one step darker        (it sits deeper, in its own shadow)
    everything else          -> untouched

THE DARKEST TONE IS LEFT ALONE, and finding out why cost one bad attempt. The first version
also mapped darkest -> mid, on the theory that a pressed key lights its lower lip. But the
darkest tone here does TWO jobs: it is the shadow lip AND the outer outline, and they are
the same hex. Recolouring it dissolved the outline and the key came out looking chewed. A
role-based recolour only works on colours that have ONE role, so the transform now touches
only the highlight, which does.

The face is darkened by scaling toward black rather than by picking a new hex, so it holds
whatever the idle key's face happens to be if the art is regenerated.
"""
import collections, os, sys
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'scene_cast_raw')


def luma(c):
    return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]


def press(src, dst, darken=0.86):
    im = Image.open(src).convert('RGBA')
    px = im.load()
    W, H = im.size
    cols = collections.Counter(px[x, y][:3] for y in range(H) for x in range(W)
                               if px[x, y][3] > 40)
    ordered = sorted(cols, key=luma)
    darkest = ordered[0]                       # the outline / shadow lip
    lightest = ordered[-1]                     # the lit lip
    # A mid tone that already exists in the art, so the pressed key stays inside the same
    # palette instead of inventing a colour: the darkest of the SIDE tones, which is what
    # a lower lip catching light actually looks like here.
    mids = [c for c in ordered if luma(c) > luma(darkest) + 20][:max(1, len(ordered) // 3)]
    mid = mids[-1] if mids else lightest

    out = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    op = out.load()
    for y in range(H):
        for x in range(W):
            r, g, b, a = px[x, y]
            if a <= 40:
                continue
            c = (r, g, b)
            # BANDS, NOT EXACT HEXES. Matching `c == lightest` left the lip speckled: the
            # generator drew the highlight in four near-whites (#fffaff, #fefefe and two
            # more) and only one of them was the single brightest, so three survived as
            # white confetti. A role is a range of tones, not a value.
            if luma(c) >= luma(lightest) - 14:
                c = mid                    # the lit lip stops being lit
            elif luma(c) <= luma(darkest) + 14:
                pass                       # outline and shadow lip: never touched
            else:
                c = tuple(max(0, int(v * darken)) for v in c)
            op[x, y] = c + (a,)
    out.save(dst)
    print('pressed -> %s   (lit %s -> %s, face x%.2f, outline kept)'
          % (dst, '#%02x%02x%02x' % lightest, '#%02x%02x%02x' % mid, darken))
    return out


if __name__ == '__main__':
    src = os.path.join(RAW, 'ui_key.png')
    if not os.path.exists(src):
        sys.exit('run ui_key_clean.py first')
    press(src, os.path.join(RAW, 'ui_key_down.png'))
