# -*- coding: utf-8 -*-
"""The pick page's pictures for round two of the juice cartons - the real juice brands' trade dress
(juice_fruit.py --round2).

  py -3 -X utf8 Tools/v4_bottles/juice_brand_report.py      -> Docs/reports/juice_brands/
  py -3 -X utf8 Tools/v4_bottles/juice_brand_report.py --stage-picks
                                                            -> staging/brand/ for ship.py (the author's picks)

Every large take goes through the house pipeline as a shipped one would (process_take: palette, ring, the
cap unscrewed for the hand, the shelf copy derived by cellar_box). The round's shelf takes were generated at
64x128 - twice the shelf, the size at which the generator returns whole cartons rather than the crops it
returns at 32x64 - and are brought to 32x64 the way the pipeline brings a master down: the ring peeled, an
area average, holes filled, every colour snapped to the palette, one ring put back.
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
STAGE = os.path.join(HERE, 'staging', 'brand')
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
OUT = os.path.join(ROOT, 'Docs', 'reports', 'juice_brands')
BG = (42, 30, 48, 255)
INK = (242, 232, 213, 255)


def processed(cid, take):
    out = os.path.join(STAGE, cid, os.path.basename(take)[:-4])
    hand = os.path.join(out, 'v4_%s.png' % cid)
    audit = {}
    if not os.path.exists(hand):
        with contextlib.redirect_stdout(io.StringIO()):
            audit = process.process_take(cid, take, out)
        if audit.get('rejected'):
            return None
    else:
        p = os.path.join(out, 'audit.json')
        audit = json.load(open(p, encoding='utf-8')) if os.path.exists(p) else {}
    return (Image.open(hand).convert('RGBA'),
            Image.open(os.path.join(out, 'v4_%s_c.png' % cid)).convert('RGBA'),
            audit.get('spout'))


def native_shelf(cid, path):
    """A 64x128 shelf take brought down to 32x64 the way cellar_box brings a master down."""
    im = Image.open(path).convert('RGBA')
    im, _ = process.restore_body(im, cid)
    im = palette.quantize(im)
    peeled = process.peel_and_ring(im, 0)
    small, _ = process.box_down(peeled, 2, 0.5)
    small = process.fill_holes(small)
    px = small.load()
    for y in range(small.height):
        for x in range(small.width):
            if px[x, y][3]:
                px[x, y] = palette.nearest(px[x, y][:3]) + (255,)
    return process.thin_ring(small, process.peel_and_ring(small.copy(), 1, cut=1, peel=False))


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
    for cid in juice_fruit.BRANDS:
        hands, shelves, natives, notes = [], [], [], {}
        cur_h, cur_s = os.path.join(ITEMS, 'v4_%s.png' % cid), os.path.join(ITEMS, 'v4_%s_c.png' % cid)
        if os.path.exists(cur_h):
            hands.append(tile(Image.open(cur_h).convert('RGBA'), 2, 'NOW'))
        if os.path.exists(cur_s):
            shelves.append(tile(Image.open(cur_s).convert('RGBA'), 4, 'NOW'))
        for d in (0, 1):
            for seed in juice_fruit.SEEDS:
                take = os.path.join(RAW, cid, 'brand_L%d_s%d.png' % (d, seed))
                if not os.path.exists(take):
                    continue
                got = processed(cid, take)
                if not got:
                    continue
                name = '%s%d' % ('AB'[d], seed)
                notes[name] = {'spout': got[2]}
                hands.append(tile(got[0], 2, name + ('' if got[2] else ' CLOSED')))
                shelves.append(tile(got[1], 4, name + ' derived'))
            first = os.path.join(RAW, cid, 'brand_N%d_s%d.png' % (d, juice_fruit.SEEDS[0]))
            extra = [p for p in glob.glob(first.replace('.png', '_c[0-9]*.png')) if not p.endswith('_bg.png')]
            cands = [first] + sorted(extra, key=lambda p: int(p.rsplit('_c', 1)[1][:-4]))
            for i, pth in enumerate(p for p in cands if os.path.exists(p) and not p.endswith('_bg.png')):
                natives.append(tile(native_shelf(cid, pth), 4, '%s-native%d' % ('AB'[d], i + 1)))
        files = {'notes': notes}
        for key, tiles in (('hand', hands), ('shelf', shelves), ('native', natives)):
            if tiles:
                p = os.path.join(OUT, '%s_%s.png' % (cid, key))
                row(tiles).save(p)
                files[key] = os.path.basename(p)
        made[cid] = files
        print(cid, json.dumps(files))
    io.open(os.path.join(OUT, 'files.json'), 'w', encoding='utf-8').write(json.dumps(made, indent=1))
    return made


def stage_picks():
    """Writes what ship.py copies for every branded pick in picks.json (2026-09-25): the hand take's
    processed plates, and the shelf plate its "shelf" names (brand/<cid>/native/<raw take>.png), brought
    down by native_shelf. staging/ is not kept by git; the raw takes are, so this is how a clone gets
    the picks back."""
    picks = json.load(io.open(os.path.join(HERE, 'picks.json'), encoding='utf-8'))
    for cid, pick in picks.items():
        shelf = pick.get('shelf') or ''
        if not shelf.startswith('brand/'):
            continue
        if not processed(cid, os.path.join(RAW, cid, pick['take'] + '.png')):
            print('  !! %s: %s was rejected' % (cid, pick['take'])); continue
        out = os.path.join(HERE, 'staging', *shelf.split('/'))
        os.makedirs(os.path.dirname(out), exist_ok=True)
        native_shelf(cid, os.path.join(RAW, cid, os.path.basename(out))).save(out)
        print('  %-16s hand %s, shelf %s' % (cid, pick['take'], os.path.basename(out)))


if __name__ == '__main__':
    stage_picks() if '--stage-picks' in sys.argv[1:] else build()
