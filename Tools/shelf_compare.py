# -*- coding: utf-8 -*-
"""The cellar's packing, before and after, drawn on the counter it actually stands in.

WHY THIS IS DRAWN AND NOT SCREENSHOTTED. The NEW shelf has a real photograph - it was
measured and captured in play (42 bottles, all 62 tall, nothing over a post). The OLD one
cannot have a photograph without reverting the code and re-entering play, so both sides
here are RENDERED from the same sprites by the same two algorithms, transcribed from
DiegeticStage. That makes it a diagram, and it is labelled as one; what it is honest
about is the only thing being compared - where each bottle stands and how tall it is
drawn.

Both algorithms are short enough to transcribe without paraphrasing:

  OLD  every bay is cut into `perBay` EQUAL slots, and then the whole shelf is scaled
       down until the widest bottle in stock fits its slot. Two consequences the author
       hit: buying one broad-shouldered bottle shrinks every other bottle in the bar, and
       anything past 36 is simply not drawn.
  NEW  the height is a constant; a slot is a bottle's OWN drawn width; the leftover air
       in a bay is shared out evenly and collapses to one pixel when the bay is full.

    py -3 Tools/shelf_compare.py            # writes Tools/shelf_before_after.png
"""
import glob
import math
import os

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
COUNTER = os.path.join(ROOT, 'Assets', 'Art', 'Backgrounds', 'counter.png')
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
OUT = os.path.join(HERE, 'shelf_before_after.png')

# DiegeticStage's own numbers, verbatim.
BAY_CENTRE = [120.0, 319.0, 517.0]
BAY_W = 175.0
FOOT = [143.0, 233.0]
BOTTLE_H = 62.0
MIN_GAP = 1.0
BAYS = 3
OLD_MIN, OLD_MAX = 3, 6
OLD_FILL = 0.92
OLD_SLOTS = BAYS * 2 * OLD_MAX          # 36
NEW_MAX = 8


def stock(n):
    """Tonight's shelf: the catalogue, repeated if the bar has bought past it."""
    paths = sorted(glob.glob(os.path.join(ITEMS, 'v3_*_flat.png')))
    ims = [Image.open(p).convert('RGBA') for p in paths]
    return [ims[i % len(ims)] for i in range(n)]


def old_layout(bottles):
    """DiegeticStage as it was: equal slots, and the whole shelf scaled to the widest."""
    n = min(len(bottles), OLD_SLOTS)
    per = max(OLD_MIN, min(OLD_MAX, math.ceil(n / float(BAYS * len(FOOT)))))
    slot = BAY_W / per
    h = BOTTLE_H
    for b in bottles[:n]:
        aspect = b.width / float(b.height)
        if aspect > 0.0001:
            h = min(h, slot * OLD_FILL / aspect)
    h = max(8.0, h)
    out = []
    for i in range(n):
        per_shelf = BAYS * per
        shelf, rest = i // per_shelf, i % per_shelf
        bay, pos = rest // per, rest % per
        x = BAY_CENTRE[bay] - BAY_W / 2 + slot * (pos + 0.5)
        out.append((x, h, FOOT[min(shelf, len(FOOT) - 1)]))
    return out, h, len(bottles) - n


def new_layout(bottles):
    """DiegeticStage as it is: one height, a bottle's own width, air that gives."""
    n = len(bottles)
    comp = len(FOOT) * BAYS
    taken, out = 0, []
    for c in range(comp):
        if taken >= n:
            break
        want = min(int(math.ceil((n - taken) / float(comp - c))), NEW_MAX)
        take, sumw = 0, 0.0
        while take < want and taken + take < n:
            b = bottles[taken + take]
            w = BOTTLE_H * b.width / float(b.height)
            if take and sumw + w + take * MIN_GAP > BAY_W:
                break
            sumw += w
            take += 1
        if not take:
            continue
        gap = max(MIN_GAP, (BAY_W - sumw) / (take + 1)) if take > 1 else 0.0
        run = sumw + (take - 1) * gap
        x = BAY_CENTRE[c % BAYS] - run / 2
        for k in range(take):
            b = bottles[taken + k]
            w = BOTTLE_H * b.width / float(b.height)
            out.append((x + w / 2, BOTTLE_H, FOOT[c // BAYS]))
            x += w + gap
        taken += take
    return out, BOTTLE_H, n - len(out)


def draw(bottles, layout):
    plate = Image.open(COUNTER).convert('RGBA')
    for (cx, h, foot), b in zip(layout, bottles):
        w = max(1, int(round(h * b.width / float(b.height))))
        hh = int(round(h))
        small = b.resize((w, hh), Image.NEAREST)
        plate.alpha_composite(small, (int(round(cx - w / 2.0)), int(round(foot - hh))))
    return plate


def gaps(bottles, layout):
    """The narrowest air between two shoulders on one shelf - the number the brief is
    actually about."""
    rows = {}
    for (cx, h, foot), b in zip(layout, bottles):
        w = h * b.width / float(b.height)
        rows.setdefault(foot, []).append((cx - w / 2, cx + w / 2))
    worst = None
    for run in rows.values():
        run.sort()
        for a, c in zip(run, run[1:]):
            g = c[0] - a[1]
            if g > -50 and (worst is None or g < worst):     # ignore the jump between bays
                worst = g
    return worst


def main():
    n = 42
    bottles = stock(n)
    old, oldh, olddrop = old_layout(bottles)
    new, newh, newdrop = new_layout(bottles)

    panels = [
        ('ONCE  —  esit yuvalar, raf kuculuyor', draw(bottles[:len(old)], old),
         'sise boyu %.1f px   ·   en dar aralik %.1f px   ·   cizilmeyen %d sise'
         % (oldh, gaps(bottles, old) or 0, olddrop)),
        ('SONRA  —  boy sabit, aralik veriyor', draw(bottles[:len(new)], new),
         'sise boyu %.1f px   ·   en dar aralik %.1f px   ·   cizilmeyen %d sise'
         % (newh, gaps(bottles, new) or 0, newdrop)),
    ]

    z, pad = 2, 16
    pw, ph = panels[0][1].width * z, panels[0][1].height * z
    out = Image.new('RGB', (pw + pad * 2, (ph + 46) * len(panels) + pad), (0x1A, 0x10, 0x23))
    d = ImageDraw.Draw(out)
    y = pad
    for title, im, caption in panels:
        d.text((pad, y), '%s   (%d sise)' % (title, n), fill=(0xE8, 0x4D, 0xA6))
        y += 15
        out.paste(im.convert('RGB').resize((pw, ph), Image.NEAREST), (pad, y))
        y += ph + 5
        d.text((pad, y), caption, fill=(0xC9, 0xBC, 0xA8))
        y += 26
    out.save(OUT)
    print('%s  %dx%d' % (os.path.relpath(OUT, ROOT), out.width, out.height))
    for title, _, caption in panels:
        print('  %-40s %s' % (title, caption))


if __name__ == '__main__':
    main()
