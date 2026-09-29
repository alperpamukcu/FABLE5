# -*- coding: utf-8 -*-
"""Mock-ups of the cabinet at 1280x720, for LOOKING before shipping (cabinet.py --preview DIR --assets CHECKOUT).

Never writes into Assets. Reads the author's slab (counter.png rows 0..64), the author's doors and the real v4 cellar
plates from a checkout whose PNGs are real files (a worktree carries Git LFS pointers, hence --assets), draws the body
at exactly 2x where the stage puts it (native-centred: art column c at stage c + 1, the slab tiled to 640 the way
DiegeticStage.Refit tiles it), the bottles where LastCall.Game.CellarCabinet.Plan stands them, and the family words as
the HUD sets them - Silkscreen 16 on the plate, 8 when the word will not fit.
"""
import colorsys
import hashlib
import json
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

import cabinet as C

W, HGT = 1280, 720
OPEN_TOP, SHUT_TOP = 214, 476          # screen row of counter-art row 0, cellar open / shut (720 - 2 x art top)
MARGIN, GAP_MIN, GAP_MAX = 4, 1, 6     # CellarCabinet.Margin / GapMin / GapMax

# ── the family map, as LastCall.Game.CellarCabinet.Family keeps it ───────────────────────────────────────────────
FAMILY_AT = [['vodka', 'gin', 'whiskey', 'rum', 'tequila'], ['liqueur', 'syrup', 'juice', 'soda', 'mixer']]
WORD = {'vodka': 'VODKA', 'gin': 'GIN', 'rum': 'RUM', 'tequila': 'TEQUILA', 'whiskey': 'WHISKY',
        'liqueur': 'LIQUEURS', 'syrup': 'SYRUPS', 'juice': 'JUICES', 'soda': 'SODA & TONIC', 'mixer': 'MIXERS'}
STYLE_RANK = ['lemon', 'lime', 'orange', 'pineapple', 'cranberry', 'soda', 'tonic', 'ginger', 'cola', 'energy',
              'syrup', 'grenadine', 'vermouth', 'amaro', 'triple_sec', 'coffee_liqueur']
LIQ = {
    'vodka': 0xABD7F4, 'soda': 0x99CEF4, 'tonic': 0x85C5F7, 'gin': 0xA4DECE, 'tequila': 0xE0EE8E,
    'triple_sec': 0xFFB248, 'syrup': 0xF9E7A2, 'lemon': 0xEFEC48, 'pineapple': 0xF3CB46, 'energy': 0xCFE53A,
    'lime': 0xA2CE3C, 'ginger': 0xE3C066, 'orange': 0xF5941C, 'rum': 0xAE5F16, 'bourbon': 0xCF7F1D,
    'cranberry': 0xE04466, 'amaro': 0xC9382C, 'vermouth': 0x832746, 'grenadine': 0xB0142E, 'cola': 0x93411B,
    'coffee_liqueur': 0x5C2E0E,
}


def family(card):
    cat = card.get('category')
    if cat and cat not in ('mixer', 'juice'):
        return cat if cat in ('vodka', 'gin', 'rum', 'tequila', 'whiskey') else 'liqueur'
    if cat == 'juice':
        return 'juice'
    if card['type'] in ('Sweet', 'Bitter'):
        return 'syrup'
    if card['type'] == 'Sour':
        return 'juice'
    return 'soda' if card.get('style') in ('soda', 'tonic') else 'mixer'


def niche_of(fam):
    for b, row in enumerate(FAMILY_AT):
        if fam in row:
            return b * 5 + row.index(fam)
    return 5


def shelve(cards):
    def key(c):
        s = c.get('style', '')
        return (niche_of(family(c)), STYLE_RANK.index(s) if s in STYLE_RANK else 99, c.get('tier', 1), c['name'])
    return sorted(cards, key=key)


def plan(widths, niches):
    """CellarCabinet.Plan: (left x of the drawing, width, feet row) per bottle."""
    out = [None] * len(widths)
    i = 0
    while i < len(widths):
        j = i
        while j < len(widths) and niches[j] == niches[i]:
            j += 1
        b, col = divmod(niches[i], 5)
        x0, w = C.NICHE_X[col]
        usable = w - 2 * MARGIN
        ws = widths[i:j]
        k = j - i
        gap = 0 if k < 2 else max(GAP_MIN, min(GAP_MAX, (usable - sum(ws)) // (k - 1)))
        run = sum(ws) + gap * (k - 1)
        left = x0 + MARGIN + (usable - run) // 2
        for n in range(i, j):
            out[n] = (left, widths[n], C.ROWS[b]['feet'])
            left += widths[n] + gap
        i = j
    return out


# ── the bottles, the game's sandwich: back, drink inside the mask, front ────────────────────────────────────────
def liquid(style):
    v = LIQ.get(style, 0xC9BCA8)
    r, g, b = (v >> 16) / 255, ((v >> 8) & 255) / 255, (v & 255) / 255
    h, s, vv = colorsys.rgb_to_hsv(r, g, b)
    r, g, b = colorsys.hsv_to_rgb(h, min(1.0, s * 1.35 + 0.03), min(1.0, vv * 1.03))
    return np.array([int(r * 255), int(g * 255), int(b * 255)])


def load(items, name):
    p = os.path.join(items, name)
    return np.array(Image.open(p).convert('RGBA')) if os.path.exists(p) else None


def bottle(items, card, fill):
    back = load(items, 'v4_%s_back_c.png' % card['id'])
    if back is None:
        return Image.fromarray(load(items, 'v4_%s_c.png' % card['id']))
    mask = load(items, 'v4_%s_mask_c.png' % card['id'])[..., 3] > 127
    front = Image.fromarray(load(items, 'v4_%s_front_c.png' % card['id']))
    out = back.copy()
    rows = np.nonzero(mask.any(axis=1))[0]
    widths = mask.sum(axis=1)
    top, bot = rows.min(), rows.max()
    shoulder = top
    for y in range(int(np.argmax(widths)), top - 1, -1):
        if widths[y] < 0.6 * widths.max():
            shoulder = y + 1
            break
    want, cum, level = fill * widths[shoulder:bot + 1].sum(), 0, bot + 1
    for y in range(bot, shoulder - 1, -1):
        cum += widths[y]
        level = y
        if cum >= want:
            break
    tone = liquid(card.get('style'))
    for y in range(level, bot + 1):
        xs = np.nonzero(mask[y])[0]
        if len(xs):
            out[y, xs, :3] = tone if y != level else np.clip(tone + (255 - tone) * 0.28, 0, 255)
            out[y, xs, 3] = 255
    im = Image.fromarray(out.astype(np.uint8))
    im.alpha_composite(front)
    return im


def opaque(items, card):
    a = load(items, 'v4_%s_front_c.png' % card['id'])
    if a is None:
        a = load(items, 'v4_%s_c.png' % card['id'])
    xs = np.nonzero((a[..., 3] > 0).any(axis=0))[0]
    return int(xs.max() - xs.min() + 1), int(xs.min())


def up(im, k=2):
    return im.resize((im.width * k, im.height * k), Image.NEAREST)


def text(font, s, size, colour):
    f = ImageFont.truetype(font, size)
    tmp = Image.new('RGBA', (len(s) * size + 16, size * 2 + 8), (0, 0, 0, 0))
    d = ImageDraw.Draw(tmp)
    d.fontmode = '1'
    d.text((4, 4), s, font=f, fill=colour)
    bb = tmp.getbbox()
    return tmp.crop(bb) if bb else tmp


def slab_row_640(counter):
    """The slab as Refit draws it at 16:9: 9-sliced at 217/218 and tiled to 640 (the middle repeats 2 columns)."""
    a = np.array(counter)
    return np.concatenate([a[:, :217], a[:, 217:420], a[:, 217:219], a[:, 420:]], axis=1)


def scene(bodies, level, cards, assets, opening, shut=False, door_painted=False):
    items = os.path.join(assets, 'Assets', 'Resources', 'Items')
    font = os.path.join(assets, 'Assets', 'Fonts', 'Silkscreen-Regular.ttf')
    top = SHUT_TOP if shut else OPEN_TOP
    img = Image.new('RGBA', (W, HGT), C.N[2])
    counter = Image.open(os.path.join(assets, 'Assets', 'Art', 'Backgrounds', 'counter.png')).convert('RGBA')
    slab = slab_row_640(counter)[:C.TOP]
    img.alpha_composite(up(Image.fromarray(slab)), (0, top))
    shelved = shelve(cards)
    niches = [niche_of(family(c)) for c in shelved]
    stocked = set(niches)
    lit, pilot = bodies[level]
    body = pilot.copy()
    for n in stocked:
        # the stage lays the niche's rectangle of the lit OVERLAY over the pilot: only its opening is opaque
        x0, y0, x1, y1 = C.niche_rect(n // 5, n % 5)
        ys, xs = slice(y0 - C.TOP, y1 - C.TOP + 1), slice(x0, x1 + 1)
        on = lit[ys, xs, 3] > 0
        body[ys, xs][on] = lit[ys, xs][on]
    by = top + 2 * C.TOP
    if by < HGT:
        img.alpha_composite(up(Image.fromarray(body)).crop((0, 0, 2 * C.ART_W, HGT - by)), (2, by))
    if not shut:
        spans = [opaque(items, c) for c in shelved]
        placed = plan([s[0] for s in spans], niches)
        for c, (left, w, feet), (_, lx) in zip(shelved, placed, spans):
            h = int(hashlib.md5(c['id'].encode()).hexdigest()[:6], 16) / 0xFFFFFF
            fill = 0.80 + 0.20 * h if opening else 0.28 + 0.72 * h
            bt = up(bottle(items, c, fill))
            img.alpha_composite(bt, (2 * (left - lx + 1), top + 2 * (feet - 64)))
        for b in (0, 1):
            for col in range(5):
                x0, w = C.NICHE_X[col]
                pw = C.PLATE_W[col]
                pl = x0 + (w - pw) // 2
                p0, p1 = C.ROWS[b]['plate']
                on = b * 5 + col in stocked
                ink = C.N[1] if on else C.CR[1]
                t = text(font, WORD[FAMILY_AT[b][col]], 16, ink)
                if t.width > 2 * (pw - 4):
                    t = text(font, WORD[FAMILY_AT[b][col]], 8, ink)
                cx = 2 * (pl + 1) + pw
                cy = top + 2 * p0 + (2 * (p1 - p0 + 1) - t.height) // 2
                img.alpha_composite(t, (cx - t.width // 2, cy))
        rail = C.ROWS[1]['lip'][0]
        door = Image.open(os.path.join(assets, 'Assets', 'Resources', 'Scene', 'counter_door_worn.png' if level == 1
                                       else 'counter_door.png')).convert('RGBA')
        img.alpha_composite(up(door.crop((0, 0, door.width, 16))), (48, top + 2 * rail))
    else:
        door = Image.open(os.path.join(assets, 'Assets', 'Resources', 'Scene',
                                       'counter_door.png' if door_painted else 'counter_door_worn.png')).convert('RGBA')
        d2 = up(door)
        img.alpha_composite(d2.crop((0, 0, d2.width, HGT - by)), (48, by))
    return img.convert('RGB')


def run(bodies, out, assets):
    os.makedirs(out, exist_ok=True)
    deck = json.load(open(os.path.join(assets, 'Assets', 'Data', 'bottles', 'base_bar.json'), encoding='utf-8'))
    pour = [c for c in deck['cards'] if c['type'] not in ('Garnish', 'Beer')]
    opening = [c for c in pour if c.get('starting')]
    bands = []
    for level in (1, 2, 3):
        full = scene(bodies, level, pour, assets, False)
        full.save(os.path.join(out, 'cabinet_full_L%d.png' % level))
        scene(bodies, level, opening, assets, True).save(os.path.join(out, 'cabinet_opening_L%d.png' % level))
        bands.append(full.crop((0, 330, W, HGT)))
    scene(bodies, 1, pour, assets, False, shut=True).save(os.path.join(out, 'cabinet_shut_worn_L1.png'))
    scene(bodies, 3, pour, assets, False, shut=True, door_painted=True).save(os.path.join(out, 'cabinet_shut_L3.png'))
    tall = Image.new('RGB', (W, sum(b.height for b in bands)))
    y = 0
    for b in bands:
        tall.paste(b, (0, y))
        y += b.height
    tall.save(os.path.join(out, 'cabinet_levels.png'))
    print('preview ->', out)
