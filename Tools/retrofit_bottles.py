# -*- coding: utf-8 -*-
"""Open the glass on the bottles that already shipped, so their level can be seen.

Kept in the repo because the measured panels below are the expensive part — they were
read off a coordinate grid, one bottle at a time, and nothing re-derives them.

    python Tools/retrofit_bottles.py            # measure only
    python Tools/retrofit_bottles.py --write    # install

Like bake_v5_bottle.py, this leans on the pilot chain's modules (audit, degrid6,
v4_stack, v5_stack) and so needs V5_PILOT pointing at that working directory if the
default scratchpad is gone.
"""
import os
import sys

PILOT = os.environ.get('V5_PILOT') or os.path.join(
    os.path.expanduser('~'), 'AppData', 'Local', 'Temp', 'claude',
    'c--My-project--2-', '2ee56b43-3292-45a5-b9f4-ae2667166af5', 'scratchpad')
sys.path.insert(0, PILOT)

from PIL import Image

import audit
import degrid6
import v4_stack as V
import v5_stack as V5

DEST = r'c:\My project (2)\Assets\Resources\Items'

# Already through the full chain; leave them alone.
DONE = {'vodka_vor', 'vodka_leonid', 'gin_boothby', 'gin_juniper_crown', 'vodka_astra'}

# These six have brand-new art waiting in shelf10d — retrofitting the old sprite would
# only be thrown away. Handled by bake_v5_bottle.py --install once they are signed off.
PENDING = {'gin_thornwood', 'rum_cane_coral', 'rum_tidewater',
           'bourbon_redline', 'bourbon_old_harrow', 'tequila_sonora'}


# Four bottles wear a label over most of their body, so the detector's brake fires —
# it is designed to refuse a mask that claims 62% or more, because a mask that big is
# usually a mask that has failed. Here it has not: Mason's Mark really is a label from
# the shoulder down. Read off ruler.py at zoom 5, on the CAPPED sprite.
PANEL = {
    'bourbon_ashfall': [(3, 63, 57, 136)],
    # Van Wrinkle is a scroll label from the shoulder down over a painted white neck.
    # Widening the panel does not help and nothing else will: there is no plain glass
    # anywhere on it to turn into a window, so it stays opaque and is reported. That
    # is a REDRAW, the way Grey Gander was, not a retrofit.
    'bourbon_hollow_oak': [(2, 67, 45, 146)],
    'tequila_sol_viejo': [(3, 76, 60, 137)],
    # A beer bottle: the big yellow label AND the yellow neck foil. Without both, the
    # glass tone lands on the label's yellow, every brown pixel reads as deviant, and
    # the bottle comes back 1% see-through — opaque, exactly what we are fixing.
    'ginger_kicker': [(1, 99, 55, 172), (9, 27, 47, 49)],
}


def open_shift(capped_path, open_path):
    """How far the capless art sits up the page from the capped one.

    The two were cropped separately when they shipped, so the capless is shorter by
    however much cap was removed — amaro_notte is 142 rows against 119 — and a panel
    measured on one lands in the wrong place on the other. Matched on the silhouette
    rather than assumed from the height difference, because the crop is not always
    all from the top.
    """
    import graft
    a = Image.open(capped_path).convert('RGBA')
    b = Image.open(open_path).convert('RGBA')
    got = graft.align(a.crop(a.getbbox()), b.crop(b.getbbox()), span=40)
    return (got[2] if got else 0)


def glass_tone(im, top, base, mask):
    """Plain glass: the body median with the label's own pixels taken out of it.

    Taking the median of everything is a coin toss on a bottle wearing a big label —
    John Runner's flipped to its ribbon's brown between two builds and turned the whole
    bottle opaque. The mask already knows which pixels are print.
    """
    W, H = im.size
    px = im.load()

    def luma(c):
        return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]

    skip = set(mask or ())
    out = [px[x, y][:3] for y in range(top, base + 1) for x in range(W)
           if px[x, y][3] > 0 and (x, y) not in skip]
    if len(out) < 40:
        return None
    out.sort(key=luma)
    return out[len(out) // 2]


def treat(path, panels=()):
    im = Image.open(path).convert('RGBA')
    box = im.getbbox()
    im = im.crop(box)
    before_grid = audit.chequer(im)
    im, _notes = degrid6.clean(im)
    rows, top, base, shoulder, widest = V.cavity(im)
    px = im.load()
    W, H = im.size
    mask, frac = V5._label_mask(px, W, H, top, base)
    if panels:
        # A measured panel replaces the detector rather than joining it: where we know
        # the label, the guess has nothing to add.
        mask = [(x, y) for (l, t, r, b) in panels
                for y in range(max(0, t), min(H, b + 1))
                for x in range(max(0, l), min(W, r + 1))
                if px[x, y][3] > 0]
    glass, _n = V5.glass_layer_v2(im, top, base,
                                  base_tone=glass_tone(im, top, base, mask))
    if panels and mask:
        gp = glass.load()
        for x, y in mask:
            gp[x, y] = gp[x, y][:3] + (255,)
    gp = glass.load()
    see = [gp[x, y][3] for y in range(top, base + 1) for x in range(W)
           if gp[x, y][3] > 0]
    clear = 100.0 * sum(1 for a in see if a < 250) / len(see) if see else 0.0
    solid = (100.0 * sum(1 for x, y in mask if gp[x, y][3] == 255) / len(mask)
             if mask else None)
    return glass, dict(size=glass.size, grid_before=before_grid,
                       grid_after=audit.chequer(glass), clear=clear,
                       mask=len(mask) if mask else 0, frac=frac, solid=solid)


def main(write=False, only=None):
    names = sorted(f for f in os.listdir(DEST)
                   if f.startswith('v3_') and f.endswith('_flat.png'))
    print('%-22s %-9s %-7s %-7s %-8s %s'
          % ('id', 'size', 'see', 'grid', 'label', 'note'))
    for f in names:
        bid = f[3:-9]
        if bid in DONE or bid in PENDING:
            continue
        if only and bid not in only:
            continue
        capped = os.path.join(DEST, 'v3_%s_flat.png' % bid)
        shift = 0
        if bid in PANEL:
            shift = open_shift(capped, os.path.join(DEST, 'v3_%s_flat_open.png' % bid))
        for suffix in ('_flat', '_flat_open'):
            p = os.path.join(DEST, 'v3_%s%s.png' % (bid, suffix))
            if not os.path.exists(p):
                print('%-22s missing %s' % (bid, suffix))
                continue
            panels = PANEL.get(bid, ())
            if panels and suffix == '_flat_open':
                panels = [(l, t - shift, r, b - shift) for (l, t, r, b) in panels]
            glass, m = treat(p, panels)
            note = ''
            if not m['mask']:
                note = 'NO LABEL MASK (%.0f%%)' % (100 * m['frac'])
            elif m['solid'] is not None and m['solid'] < 99:
                note = 'label only %.0f%% solid' % m['solid']
            if m['clear'] < 8:
                note = (note + ' ' if note else '') + 'STILL OPAQUE - needs a redraw'
            if write and not note:
                glass.save(p)
            print('%-22s %-9s %6.1f%% %5.1f%% %-8s %s'
                  % (bid + suffix.replace('_flat', ''), '%dx%d' % m['size'],
                     m['clear'], m['grid_after'],
                     '%d px' % m['mask'] if m['mask'] else '-', note))


if __name__ == '__main__':
    main('--write' in sys.argv,
         set(a for a in sys.argv[1:] if not a.startswith('-')) or None)
