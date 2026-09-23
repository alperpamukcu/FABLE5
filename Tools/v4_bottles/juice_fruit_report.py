# -*- coding: utf-8 -*-
"""The pick page for the fruit-printed juice cartons (see juice_fruit.py).

  py -3 -X utf8 Tools/v4_bottles/juice_fruit_report.py      -> Docs/reports/juice_cartons/

Every large take goes through the house pipeline exactly as a shipped one would (process_take: the
body the background removal took is given back, the palette, the ring, the cap unscrewed for the hand,
the shelf copy derived by cellar_box) into staging/fruit/, so what the page shows is what would ship.
The native small takes are shown as they came, with only the same body restore, palette and ring - a
closed carton, which is what the shelf holds anyway.

Images carry ASCII labels only: PIL's built-in face has no Turkish letters, and a label drawn in boxes
reads as a bug on the author's page.
"""
import contextlib
import glob
import io
import json
import os
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
import juice_fruit  # noqa: E402
import palette      # noqa: E402
import process      # noqa: E402

RAW = os.path.join(HERE, 'raw')
STAGE = os.path.join(HERE, 'staging', 'fruit')
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
OUT = os.path.join(ROOT, 'Docs', 'reports', 'juice_cartons')
BG = (42, 30, 48, 255)
INK = (242, 232, 213, 255)


def processed(cid, take):
    """The large take through process_take; returns (hand sprite, derived shelf copy) or None."""
    out = os.path.join(STAGE, cid, os.path.basename(take)[:-4])
    hand = os.path.join(out, 'v4_%s.png' % cid)
    if not os.path.exists(hand):
        with contextlib.redirect_stdout(io.StringIO()):
            audit = process.process_take(cid, take, out)
        if audit.get('rejected'):
            return None
    return (Image.open(hand).convert('RGBA'),
            Image.open(os.path.join(out, 'v4_%s_c.png' % cid)).convert('RGBA'))


def native_small(cid, path):
    """A 32x64 take cleaned the way a master is, short of the steps that need the 96x192 canvas."""
    im = Image.open(path).convert('RGBA')
    im, _ = process.restore_body(im, cid)
    im = palette.quantize(im)
    return process.peel_and_ring(im, 1)


def tile(im, k, label):
    w, h = im.width * k, im.height * k
    t = Image.new('RGBA', (max(w, 7 * len(label)) + 12, h + 26), BG)
    t.alpha_composite(im.resize((w, h), Image.NEAREST), ((t.width - w) // 2, 20))
    ImageDraw.Draw(t).text((6, 4), label, fill=INK)
    return t


def row(tiles, gap=10):
    h = max(t.height for t in tiles)
    out = Image.new('RGBA', (sum(t.width for t in tiles) + gap * (len(tiles) + 1), h + gap * 2), BG)
    x = gap
    for t in tiles:
        out.alpha_composite(t, (x, gap + h - t.height))
        x += t.width + gap
    return out


def build():
    os.makedirs(OUT, exist_ok=True)
    made = {}
    for cid in juice_fruit.JUICES:
        hands, shelves = [], []
        cur_h = os.path.join(ITEMS, 'v4_%s.png' % cid)
        cur_s = os.path.join(ITEMS, 'v4_%s_c.png' % cid)
        if os.path.exists(cur_h):
            hands.append(tile(Image.open(cur_h).convert('RGBA'), 2, 'NOW'))
        if os.path.exists(cur_s):
            shelves.append(tile(Image.open(cur_s).convert('RGBA'), 4, 'NOW'))
        for d in (0, 1):
            for seed in juice_fruit.SEEDS:
                take = os.path.join(RAW, cid, 'fruit_L%d_s%d.png' % (d, seed))
                if not os.path.exists(take):
                    continue
                got = processed(cid, take)
                if not got:
                    continue
                name = '%s%d' % ('AB'[d], seed)
                hands.append(tile(got[0], 2, name))
                shelves.append(tile(got[1], 4, name + ' derived'))
        natives = []
        for d in (0, 1):
            first = os.path.join(RAW, cid, 'fruit_S%d_s%d.png' % (d, juice_fruit.SEEDS[0]))
            cands = [first] + sorted(glob.glob(first.replace('.png', '_c*.png')),
                                     key=lambda p: int(p.rsplit('_c', 1)[1][:-4]))
            for i, pth in enumerate(p for p in cands if os.path.exists(p)):
                if i >= 8:
                    break
                natives.append(tile(native_small(cid, pth), 4, '%s-n%d' % ('AB'[d], i + 1)))
        files = {}
        if hands:
            p = os.path.join(OUT, '%s_hand.png' % cid); row(hands).save(p); files['hand'] = os.path.basename(p)
        if shelves:
            p = os.path.join(OUT, '%s_shelf.png' % cid); row(shelves).save(p); files['shelf'] = os.path.basename(p)
        if natives:
            half = (len(natives) + 1) // 2
            for j, part in enumerate((natives[:half], natives[half:])):
                if part:
                    p = os.path.join(OUT, '%s_native%d.png' % (cid, j)); row(part).save(p)
                    files.setdefault('native', []).append(os.path.basename(p))
        made[cid] = files
        print(cid, files)
    io.open(os.path.join(OUT, 'files.json'), 'w', encoding='utf-8').write(json.dumps(made, indent=1))
    return made


if __name__ == '__main__':
    build()
