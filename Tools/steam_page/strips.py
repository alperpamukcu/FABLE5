# -*- coding: utf-8 -*-
"""CONTENT STRIPS for the About section, in the author's steam_kit header frame (1440 wide, a 4 px grid).

    bottles: five FULL alcohol bottles (spirits, amaro, vermouth, liqueurs) of the game's v4 cellar art (32x64, the game's own liquid colours) on a marble
             shelf with the kit's neon lip; one at a time a bottle sinks behind the shelf and another rises.
    guests:  five patrons' portraits (the game's own face.png, 64x64) behind the bar, swapping the same way.

No text in either, so one file serves every language.

    python3 strips.py        ->  out/about/strip_bottles.gif, out/about/strip_guests.gif
"""
import json, os
import numpy as np
from PIL import Image

import plate as PL
import kit

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
RES = os.path.join(ROOT, 'Assets', 'Resources')
OUT = os.path.join(HERE, 'out', 'about')
hx = PL.hx
W = 360                  # native width (x4 = 1440)
SCALE = 4

# UITheme.LiquidColors, by bottle style (Assets/Scripts/UI/UITheme.cs)
LIQUID = {'vodka': 'ABD7F4', 'soda': '99CEF4', 'tonic': '85C5F7', 'gin': 'A4DECE', 'tequila': 'E0EE8E',
          'triple_sec': 'FFB248', 'syrup': 'F9E7A2', 'lemon': 'EFEC48', 'pineapple': 'F3CB46', 'energy': 'CFE53A',
          'lime': 'A2CE3C', 'mint': '6FC85C', 'olive': 'B4BC6B', 'ginger': 'E3C066', 'lager': 'E7B443',
          'orange': 'F5941C', 'pale_ale': 'C46A12', 'rum': 'AE5F16', 'bourbon': 'CF7F1D', 'cranberry': 'E04466',
          'amaro': 'C9382C', 'vermouth': '832746', 'grenadine': 'B0142E', 'cola': '93411B',
          'coffee_liqueur': '5C2E0E', 'stout': '48181A'}


def frame(h):
    """The kit header's frame and strip at native size: border, a dimmed sunset field, the neon rule."""
    a = np.zeros((h, W, 3), np.uint8)
    a[:] = hx('#241830')
    st = kit.strip(w=W - 2, h=h - 7, seed=5)
    a[1:h - 6, 1:W - 1] = st[:h - 7, :W - 2]
    for k, c in enumerate(('#8F2464', '#C23283', '#FF7DC6', '#C23283')):
        a[h - 6 + k, 1:W - 1] = hx(c)
    a[h - 2, 1:W - 1] = hx('#A26831')
    a[h - 1] = hx('#241830')
    return a


def shelf(a, y, depth=4):
    """ClubBlue marble shelf top with the magenta lip under it (the key art's counter, in small)."""
    a[y:y + depth, 1:W - 1] = hx('#131B3D')
    a[y, 1:W - 1] = hx('#383D45')
    rng = np.random.RandomState(3)
    for _ in range(10):
        x, yy = rng.randint(2, W - 20), y + 1 + rng.randint(0, max(1, depth - 1))
        a[yy, x:x + rng.randint(5, 16)] = hx('#1F2E66')
    a[y + depth] = hx('#E84DA6'); a[y + depth + 1] = hx('#FF7DC6'); a[y + depth + 2] = hx('#C23283')
    a[y + depth, 0] = a[y + depth, W - 1] = hx('#241830')


def bottle(card, style):
    back = Image.open(os.path.join(RES, 'Items', 'v4_%s_back_c.png' % card)).convert('RGBA')
    mask = np.asarray(Image.open(os.path.join(RES, 'Items', 'v4_%s_mask_c.png' % card)).convert('RGBA'))[..., 3] > 0
    front = Image.open(os.path.join(RES, 'Items', 'v4_%s_front_c.png' % card)).convert('RGBA')
    col = hx('#' + LIQUID.get(style, 'CF7F1D'))
    ys = np.nonzero(mask.any(1))[0]
    line = ys.min() + max(1, int((ys.max() - ys.min()) * 0.10))          # full: a finger of air at the neck
    a = np.zeros(mask.shape + (4,), np.uint8)
    k = mask.copy(); k[:line] = False
    a[k] = col + (255,)
    surf = np.nonzero(k.any(1))[0]
    if len(surf):                                                           # the meniscus, one step lighter
        r = surf.min()
        a[r][k[r]] = tuple(min(255, int(c * 1.18 + 18)) for c in col) + (255,)
    out = back.copy()
    out.alpha_composite(Image.fromarray(a, 'RGBA'))
    out.alpha_composite(front)
    return out.crop(out.getbbox())


def bottles():
    data = json.load(open(os.path.join(ROOT, 'Assets', 'Data', 'bottles', 'base_bar.json')))['cards']
    style = {c['id']: c.get('style', '') for c in data}
    tier = {c['id']: c.get('tier', 1) for c in data}
    alcohol = {c['id'] for c in data if c['type'] == 'Spirit' or c.get('style') in ('amaro', 'vermouth', 'triple_sec',
                                                                                       'coffee_liqueur')}
    cards = [c['id'] for c in data if c['id'] in alcohol
             and os.path.exists(os.path.join(RES, 'Items', 'v4_%s_front_c.png' % c['id']))]
    # spirits first, by tier, so the run moves from house pours to the top shelf
    cards.sort(key=lambda c: (tier[c], c))
    return [bottle(c, style[c]) for c in cards]


def guests():
    out = []
    for d in sorted(os.listdir(os.path.join(RES, 'Patron'))):
        p = os.path.join(RES, 'Patron', d, 'face.png')
        if os.path.exists(p):
            f = Image.open(p).convert('RGBA')
            out.append(f.crop(f.getbbox()))
    rng = np.random.RandomState(11)                                         # a mixed crowd, not alphabetical
    idx = rng.permutation(len(out))
    return [out[i] for i in idx]


def strip(items, name, h, base_y, behind=True, hold=6, move=5, ms=110):
    """Five slots; each swap the slot's item sinks `item height` rows behind the shelf and the next rises."""
    bg = frame(h)
    shelf_y = base_y
    slots = 5
    xs = [int(round((i + 0.5) * W / slots)) for i in range(slots)]
    cur = list(range(slots))
    nxt = slots
    frames = []

    def draw(offsets):
        a = bg.copy()
        img = Image.fromarray(a, 'RGB').convert('RGBA')
        for i in range(slots):
            it = items[cur[i] % len(items)]
            y = shelf_y - it.height + 1 + offsets[i]
            layer = Image.new('RGBA', img.size, (0, 0, 0, 0))
            layer.alpha_composite(it, (xs[i] - it.width // 2, max(-it.height, y)) if y > -it.height else (0, 0))
            la = np.asarray(layer).copy()
            if behind:
                la[shelf_y + 1:] = 0                                        # hidden behind the shelf
            img.alpha_composite(Image.fromarray(la, 'RGBA'))
        a2 = np.asarray(img.convert('RGB')).copy()
        shelf(a2, shelf_y + 1)
        return Image.fromarray(a2, 'RGB').resize((W * SCALE, h * SCALE), Image.NEAREST)

    total = len(items)
    swaps = max(total - slots, 0) + slots          # every item shows at least once, ending where it began
    order = [i % slots for i in range(swaps)]
    for s in order:
        for _ in range(hold):
            frames.append(draw([0] * slots))
        it_h = items[cur[s] % len(items)].height
        for k in range(1, move + 1):                                        # sink
            off = [0] * slots; off[s] = int(round(it_h * k / move)); frames.append(draw(off))
        cur[s] = nxt; nxt += 1
        it_h = items[cur[s] % len(items)].height
        for k in range(move - 1, -1, -1):                                   # rise
            off = [0] * slots; off[s] = int(round(it_h * k / move)); frames.append(draw(off))
    os.makedirs(OUT, exist_ok=True)
    p = os.path.join(OUT, name + '.gif')
    sheet = Image.new('RGB', (frames[0].width, frames[0].height * 6))
    for i, f in enumerate(frames[::max(1, len(frames) // 6)][:6]):
        sheet.paste(f, (0, i * frames[0].height))
    master = sheet.quantize(colors=255, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    pal = [f.quantize(palette=master, dither=Image.Dither.NONE) for f in frames]
    pal[0].save(p, save_all=True, append_images=pal[1:], duration=ms, loop=0, optimize=True, disposal=1)
    print('%-20s %dx%d  %d frames  %.2f MB' % (name, frames[0].width, frames[0].height, len(frames),
                                               os.path.getsize(p) / 1e6))


def main():
    b = bottles()
    strip(b, 'strip_bottles', h=82, base_y=72)
    g = guests()
    strip(g, 'strip_guests', h=82, base_y=72)


if __name__ == '__main__':
    main()
