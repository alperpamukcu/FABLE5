# -*- coding: utf-8 -*-
"""Every raw take -> the v4 plates, by derivation only. Nothing here calls the generator.

  py -3 Tools/v4_bottles/process.py vodka_astra            all takes of a card -> staging/
  py -3 Tools/v4_bottles/process.py vodka_astra --outline 2  the outline variant for the report

Chain (PLAN §9): centre on the 96x192 canvas (NEVER rescale; an oversize take is rejected)
-> quantize to the 55 -> peel the generator's own ink and ring 1px Night[0] -> measure the
camera -> cavity by GEOMETRY (the body is generated label-less, so the cavity is the span
between the walls, wall = 2px, from the shoulder to the base) -> back / mask / front plates
-> press the label (zemin + emblem + wordmark) ONTO the front at 100% -> open state at the
cap seam (Tools/bottle_open_states.open_variant, on the opaque master) -> the cellar copies
at exactly 1/3 by palette-preserving mode-downsample -> audits -> staging/<id>/<take>/.

Two rules from the memories are load-bearing:
  * derivation, never a second generation (open-states-derive);
  * running this twice on the same input writes identical bytes (sprite-pipeline-idempotence).
"""
import hashlib
import io
import json
import os
import sys
from collections import Counter

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
TOOLS = os.path.dirname(HERE)
sys.path.insert(0, TOOLS)
sys.path.insert(0, HERE)
import brief                                  # noqa: E402
import fontpx                                 # noqa: E402
import palette                                # noqa: E402

RAW = os.path.join(HERE, 'raw')
STAGING = os.path.join(HERE, 'staging')
W, H = brief.CANVAS['width'], brief.CANVAS['height']
CW, CH = brief.CELLAR['width'], brief.CELLAR['height']
INK = palette.INK + (255,)
WALL = 2                  # glass wall thickness at 192 grain
BASE = 3                  # rows of glass under the cavity
FILM_ALPHA = 77           # 30%: the cavity seen through the front glass
STREAK_ALPHA = 200        # the specular streak stays nearly solid


# ── helpers ─────────────────────────────────────────────────────────────────

def alpha_bbox(im):
    return im.split()[3].getbbox()


def lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def opaque(im):
    return im.split()[3].point(lambda a: 255 if a >= 128 else 0)


def spans(im):
    """Per row: (x0, x1) of the opaque run, or None. The silhouette in numbers."""
    a = opaque(im).load()
    out = []
    for y in range(im.height):
        xs = [x for x in range(im.width) if a[x, y]]
        out.append((xs[0], xs[-1]) if xs else None)
    return out


def centre(im):
    """On the 96x192 canvas, horizontally centred, foot two rows above the bottom.
    A take larger than the canvas is REJECTED — rescaling is what this whole plan forbids."""
    bb = alpha_bbox(im)
    if bb is None:
        raise ValueError('empty take')
    crop = im.crop(bb)
    if crop.width > W or crop.height > H - 2:
        raise ValueError('oversize take %dx%d (canvas %dx%d) — rejected, not rescaled'
                         % (crop.width, crop.height, W, H))
    out = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    out.paste(crop, ((W - crop.width) // 2, H - 2 - crop.height), crop)
    return out


def peel_and_ring(im, thickness=1):
    """Take the generator's own dark rim off the silhouette, then ring it in INK.

    The peel removes boundary pixels darker than lum 46 (ink-like) so an outline never
    thickens by accumulation; the ring adds exactly `thickness` px of Night[0] outside the
    remaining silhouette. thickness=0 gives the lineless variant for the report."""
    px = im.load()
    a = opaque(im).load()
    w, h = im.size
    # peel
    for _ in range(2):
        drop = []
        for y in range(h):
            for x in range(w):
                if not a[x, y]:
                    continue
                edge = (x == 0 or y == 0 or x == w - 1 or y == h - 1 or
                        not a[x - 1, y] or not a[x + 1, y] or not a[x, y - 1] or not a[x, y + 1])
                if edge and lum(px[x, y][:3]) < 46:
                    drop.append((x, y))
        if not drop:
            break
        for x, y in drop:
            px[x, y] = (0, 0, 0, 0)
        a = opaque(im).load()
    # ring
    for _ in range(thickness):
        add = []
        for y in range(h):
            for x in range(w):
                if a[x, y]:
                    continue
                if ((x > 0 and a[x - 1, y]) or (x < w - 1 and a[x + 1, y]) or
                        (y > 0 and a[x, y - 1]) or (y < h - 1 and a[x, y + 1])):
                    add.append((x, y))
        for x, y in add:
            px[x, y] = INK
        a = opaque(im).load()
    return im


# ── measurement (the camera gate) ───────────────────────────────────────────

def measure(im):
    """Cap ellipse height / cap width, base bow / body width. GDD 25 §1 wants 0.30 and 0.15."""
    sp = spans(im)
    rows = [i for i, s in enumerate(sp) if s]
    if not rows:
        return {}
    top, bot = rows[0], rows[-1]
    widths = [(sp[y][1] - sp[y][0] + 1) for y in rows]
    body_w = max(widths)
    # cap: the first run of rows narrower than 60% of the body, from the top
    cap_rows = 0
    for y in rows:
        if (sp[y][1] - sp[y][0] + 1) < 0.6 * body_w:
            cap_rows += 1
        else:
            break
    cap_w = max([(sp[y][1] - sp[y][0] + 1) for y in rows[:max(cap_rows, 1)]])
    # base bow: how many of the last rows are narrower than the body (the curve down)
    bow = 0
    for y in reversed(rows):
        if (sp[y][1] - sp[y][0] + 1) < 0.97 * body_w:
            bow += 1
        else:
            break
    return {'height': bot - top + 1, 'body_w': body_w, 'cap_w': cap_w, 'cap_rows': cap_rows,
            'base_bow_rows': bow, 'bow_ratio': round(bow / float(body_w), 3),
            'ratio': round((bot - top + 1) / float(body_w), 2)}


# ── cavity, plates ──────────────────────────────────────────────────────────

def cavity(im):
    """The glass interior as a mask image: the span between the walls, from the shoulder
    (where the silhouette first reaches 90% of its body width, walking down) to BASE rows
    above the foot. Pure geometry — there is no label to confuse it (PLAN §4a)."""
    sp = spans(im)
    rows = [i for i, s in enumerate(sp) if s]
    body_w = max((sp[y][1] - sp[y][0] + 1) for y in rows)
    shoulder = next(y for y in rows if (sp[y][1] - sp[y][0] + 1) >= 0.90 * body_w)
    foot = rows[-1]
    m = Image.new('RGBA', im.size, (0, 0, 0, 0))
    mp = m.load()
    for y in range(shoulder + WALL, foot - BASE + 1):
        s = sp[y]
        if not s:
            continue
        x0, x1 = s[0] + WALL, s[1] - WALL
        for x in range(x0, x1 + 1):
            mp[x, y] = (255, 255, 255, 255)
    return m, shoulder


def glass_tone(im, mask):
    """The median colour of the cavity in the master — the glass as the generator drew it."""
    px, mp = im.load(), mask.load()
    cols = [px[x, y][:3] for y in range(im.height) for x in range(im.width) if mp[x, y][3]]
    if not cols:
        return (200, 210, 220)
    cols.sort(key=lum)
    return cols[len(cols) // 2]


def plates(master, mask, glass):
    """back (opaque cavity, cool interior gradient), front (cavity as a 30% film,
    streak kept, everything else 100%)."""
    w, h = master.size
    mp = mask.load()
    # The interior. Clear glass (high luma, low chroma) is read as PALE BLUE-GREY — the
    # 75/25 mix of v3 came out beige on the pilot, which is paper and not glass. Tinted or
    # dark glass keeps its own hue and just goes darker. Snapped to the 55 either way.
    gl = lum(glass); gch = max(glass) - min(glass)
    k = 0.60 if (gl > 150 and gch < 40) else 0.25
    cool = tuple(int(glass[i] * (1 - k) + (150, 200, 235)[i] * k) for i in range(3))
    light = tuple(min(255, int(c * 0.90)) for c in cool)
    dark = tuple(int(c * 0.58) for c in cool)
    back = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    bp = back.load()
    front = master.copy()
    fp = front.load()
    sp = spans(mask)
    for y in range(h):
        s = sp[y]
        if not s:
            continue
        x0, x1 = s
        span = max(1, x1 - x0)
        for x in range(x0, x1 + 1):
            t = abs((x - x0) / float(span) - 0.5) * 2.0       # 0 centre .. 1 wall
            c = tuple(int(light[i] * (1 - t) + dark[i] * t) for i in range(3))
            bp[x, y] = palette.nearest(c) + (255,)
            r, g, b, a = fp[x, y]
            if a:
                bright = lum((r, g, b)) > lum(glass) + 40
                fp[x, y] = (r, g, b, STREAK_ALPHA if bright else FILM_ALPHA)
    return back, front


# ── the label ───────────────────────────────────────────────────────────────

BRAND_WORD = {
    'vodka_astra': 'SMIRKOFF', 'vodka_vor': 'ABSOLVE', 'vodka_leonid': 'GANDER', 'vodka_okhta': 'WHALE',
    'gin_boothby': "GARDEN'S", 'gin_juniper_crow': 'LEAFEATER', 'gin_thornwood': "HENDRAKE'S", 'gin_veilcrest': 'GIBBON 48',
    'rum_cane_coral': 'WHITE BAT', 'rum_tidewater': 'ADMIRAL', 'rum_windward': 'KRAKATOA', 'rum_reina_del_mar': 'MALIBOO',
    'bourbon_redline': 'WALKER', 'bourbon_old_harrow': 'SPANIEL', 'bourbon_ashfall': "MASON'S", 'bourbon_hollow_oak': 'WRINKLE',
    'tequila_sonora': 'CUERDO', 'tequila_alta_luna': '1810', 'tequila_sol_viejo': 'JULEP', 'tequila_cielo_rojo': 'AZULEJO',
    'amaro_notte': 'CUMPARI', 'vermouth_velvet': 'VELVET', 'liqueur_delia': 'MARINER', 'liqueur_kafa': 'KOALA',
    'beer_kestrel': 'KRONA', 'beer_collier': 'GOODNESS', 'beer_marigold': 'BRASS',
    'cola_marlow': 'LOCA', 'energy_volt': 'BLUE OX', 'orange_grove': 'GROVE', 'lemon_fresh': 'LEMONADE',
    'lime_fresh': 'LIMEADE', 'cranberry_north': 'NORTH', 'pineapple_isla': 'ISLA',
    'tonic_quinbury': "QUINN'S", 'soda_klara': 'KLARA', 'ginger_kicker': 'KICKER', 'syrup_house': 'HOUSE',
}


def label_rect(im, fam, want_h):
    """Where the label goes: on the body's widest band, a fraction of the body's width, and
    as TALL as its content needs (want_h) — a ratio-sized label squeezed the emblem to a
    third of itself on the pilot, which is ten pixels of noise."""
    sp = spans(im)
    rows = [i for i, s in enumerate(sp) if s]
    body_w = max((sp[y][1] - sp[y][0] + 1) for y in rows)
    shoulder = next(y for y in rows if (sp[y][1] - sp[y][0] + 1) >= 0.90 * body_w)
    foot = rows[-1]
    body_h = foot - shoulder
    wf, yc = {'whiskey': (0.72, 0.60), 'liqueur': (0.60, 0.50), 'gin': (0.70, 0.52),
              'can': (0.88, 0.50), 'carton': (0.82, 0.50), 'beer': (0.64, 0.58)}.get(fam, (0.66, 0.55))
    lw = int(body_w * wf)
    lh = min(want_h, int(body_h * 0.62))
    cy = shoulder + int(body_h * yc)
    cx = (sp[cy][0] + sp[cy][1]) // 2
    return cx - lw // 2, cy - lh // 2, lw, lh


def press_label(front, card_id, emblem=None, scale_hint=2):
    """Zemin + emblem + band + wordmark, pressed at 100% alpha. Deterministic.

    Top to bottom: 2 margin, emblem (32px at half = 16), 1, band 3, 2, wordmark, 2. The
    wordmark is 2x (6x10 glyphs) when it fits the plate's width and 1x otherwise."""
    fam, _, _, lr, br, _ = brief.CARDS[card_id]
    word = BRAND_WORD.get(card_id, card_id.split('_')[0].upper())
    e = None
    if emblem is not None:
        e = palette.quantize(emblem)
        eb = alpha_bbox(e)
        e = e.crop(eb) if eb else None
        if e is not None and (e.width > 16 or e.height > 16):
            e = mode_downsample(e, 2)
    eh = e.height if e is not None else 0
    # the width the plate needs is decided by the wordmark; try 2x, fall back to 1x
    probe_x0, probe_y0, plw, _ = label_rect(front, fam, 40)
    sc = scale_hint if fontpx.width(word, scale_hint) + 4 <= plw else 1
    ink_txt = palette.ramp(br, 4) if lr != br else palette.ramp(lr, 0)
    tw = fontpx.render(word, ink_txt, sc, shadow=palette.ramp(lr, 0))
    want_h = 2 + (eh + 1 if e is not None else 0) + 3 + 2 + tw.height + 2
    x0, y0, lw, lh = label_rect(front, fam, want_h)
    field = palette.ramp(lr, 3) + (255,)
    edge = palette.ramp(lr, 0) + (255,)
    band = palette.ramp(br, 2) + (255,)
    fp = front.load()
    for y in range(y0, y0 + lh):
        for x in range(x0, x0 + lw):
            if not (0 <= x < front.width and 0 <= y < front.height):
                continue
            corner = (x in (x0, x0 + lw - 1)) and (y in (y0, y0 + lh - 1))
            if corner:
                continue
            border = x in (x0, x0 + lw - 1) or y in (y0, y0 + lh - 1)
            fp[x, y] = edge if border else field
    y = y0 + 2
    if e is not None:
        front.paste(e, (x0 + (lw - e.width) // 2, y), e)
        y += eh + 1
    for yy in range(y, y + 3):
        for x in range(x0 + 1, x0 + lw - 1):
            if 0 <= yy < front.height:
                fp[x, yy] = band
    y += 3 + 2
    if tw.width > lw - 2:
        tw = fontpx.render(word, ink_txt, 1, shadow=palette.ramp(lr, 0))
    front.paste(tw, (x0 + (lw - tw.width) // 2, y), tw)
    return front


# ── the cellar copy ─────────────────────────────────────────────────────────

def mode_downsample(im, f):
    """Each f×f block -> its most frequent OPAQUE colour (alpha by majority). Palette-preserving:
    no colour is invented, which is why this and not a box filter (PLAN §3)."""
    w, h = im.width // f, im.height // f
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    src, dst = im.load(), out.load()
    for y in range(h):
        for x in range(w):
            cnt = Counter()
            alphas = 0
            for dy in range(f):
                for dx in range(f):
                    r, g, b, a = src[x * f + dx, y * f + dy]
                    if a >= 128:
                        alphas += 1
                        cnt[(r, g, b, 255 if a >= 128 else a)] += 1
            if alphas * 2 >= f * f:                      # majority opaque
                c = cnt.most_common(1)[0][0]
                dst[x, y] = c
    return out


def downsample_front(front, f=3):
    """The front's alpha is three-valued (film / streak / solid); the mode keeps that."""
    w, h = front.width // f, front.height // f
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    src, dst = front.load(), out.load()
    for y in range(h):
        for x in range(w):
            cnt = Counter(); n = 0
            for dy in range(f):
                for dx in range(f):
                    p = src[x * f + dx, y * f + dy]
                    if p[3]:
                        n += 1; cnt[p] += 1
            if n * 2 >= f * f:
                dst[x, y] = cnt.most_common(1)[0][0]
    return out


# ── audits ──────────────────────────────────────────────────────────────────

def liquid_rows(master, mask, glass):
    """Rows in the cavity that look like poured liquid: chroma > glass+16 AND luma < glass-20."""
    px, mp = master.load(), mask.load()
    gl = lum(glass); gch = max(glass) - min(glass)
    bad = 0
    for y in range(master.height):
        xs = [x for x in range(master.width) if mp[x, y][3]]
        if not xs:
            continue
        cs = [px[x, y][:3] for x in xs]
        ch = sum(max(c) - min(c) for c in cs) / len(cs)
        lu = sum(lum(c) for c in cs) / len(cs)
        if ch > gch + 16 and lu < gl - 20:
            bad += 1
    return bad


def sha(im):
    return hashlib.sha1(im.tobytes()).hexdigest()[:12]


# ── the run ─────────────────────────────────────────────────────────────────

def process_take(card_id, take_path, out_dir, outline=1, emblem=None):
    os.makedirs(out_dir, exist_ok=True)
    fam = brief.family(card_id)
    raw = Image.open(take_path).convert('RGBA')
    audit = {'card': card_id, 'take': os.path.basename(take_path), 'outline': outline}
    try:
        im = centre(raw)
    except ValueError as e:
        audit['rejected'] = str(e)
        io.open(os.path.join(out_dir, 'audit.json'), 'w', encoding='utf-8').write(json.dumps(audit, indent=1))
        return audit
    im = palette.quantize(im)
    im = peel_and_ring(im, outline)
    audit['measure'] = measure(im)
    audit['off_palette'] = palette.off_palette(im)
    master = im

    if fam in brief.SEALED:
        sprite = press_label(master.copy(), card_id, emblem)
        sprite.save(os.path.join(out_dir, 'v4_%s.png' % card_id))
        mode_downsample(sprite, 3).save(os.path.join(out_dir, 'v4_%s_c.png' % card_id))
        audit['plates'] = ['sprite', 'cellar']
    else:
        mask, shoulder = cavity(master)
        glass = glass_tone(master, mask)
        audit['glass'] = glass
        audit['liquid_rows'] = liquid_rows(master, mask, glass)
        audit['cavity_rows'] = sum(1 for s in spans(mask) if s)
        back, front = plates(master, mask, glass)
        front = press_label(front, card_id, emblem)
        # open state, derived at the cap seam on the OPAQUE master with the label on it,
        # then given the same film — never a second generation
        try:
            import bottle_open_states as bos
            opened = bos.open_variant(press_label(master.copy(), card_id, emblem), force=True)
            _, front_open = plates(opened, mask, glass)
            front_open = press_label(front_open, card_id, emblem)
        except Exception as e:                      # the seam finder can refuse a shape
            audit['open_state'] = 'derivation failed: %s' % e
            front_open = front.copy()
        back.save(os.path.join(out_dir, 'v4_%s_back.png' % card_id))
        mask.save(os.path.join(out_dir, 'v4_%s_mask.png' % card_id))
        front.save(os.path.join(out_dir, 'v4_%s_front.png' % card_id))
        front_open.save(os.path.join(out_dir, 'v4_%s_front_open.png' % card_id))
        # the cellar copies: exactly one third, palette-preserving
        mode_downsample(back, 3).save(os.path.join(out_dir, 'v4_%s_back_c.png' % card_id))
        mode_downsample(mask, 3).save(os.path.join(out_dir, 'v4_%s_mask_c.png' % card_id))
        downsample_front(front, 3).save(os.path.join(out_dir, 'v4_%s_front_c.png' % card_id))
        downsample_front(front_open, 3).save(os.path.join(out_dir, 'v4_%s_front_open_c.png' % card_id))
        # the liquid proof: red and blue composites must agree on every label pixel and
        # disagree on the cavity
        proof = composite_proof(back, mask, front)
        audit.update(proof)
        audit['plates'] = ['back', 'mask', 'front', 'front_open', '+cellar ×4']
    audit['hash'] = sha(master)
    io.open(os.path.join(out_dir, 'audit.json'), 'w', encoding='utf-8').write(json.dumps(audit, indent=1))
    return audit


def liquid_plate(back, mask, colour, fill):
    """The drink as the game draws it: the BACK plate's own wall gradient multiplied by the
    drink colour (so it is shaded like something inside a cylinder, not a flat bar), cut at
    the fill line, with a meniscus — the surface ellipse's near edge, bowed down in the
    middle by the same 15% the base bows, one shade lighter."""
    liq = Image.new('RGBA', back.size, (0, 0, 0, 0))
    lp, bp, mp = liq.load(), back.load(), mask.load()
    sp = spans(mask)
    rows = [y for y, s in enumerate(sp) if s]
    if not rows or fill <= 0:
        return liq
    top, bot = rows[0], rows[-1]
    line = bot - int(round((bot - top) * fill))
    for y in range(line, bot + 1):
        s = sp[y]
        if not s:
            continue
        for x in range(s[0], s[1] + 1):
            if not mp[x, y][3]:
                continue
            r, g, b, _ = bp[x, y]
            shade = lum((r, g, b)) / 255.0
            shade = 0.55 + 0.45 * shade                     # never fully black at the walls
            c = tuple(min(255, int(colour[i] * shade)) for i in range(3))
            lp[x, y] = palette.nearest(c) + (255,)
    # the meniscus: a 2px lighter band along the surface, bowed DOWN toward the centre
    s = sp[line] if line < len(sp) else None
    if s:
        x0, x1 = s
        wdt = max(1, x1 - x0)
        bow = max(1, int(round(wdt * 0.15 * 0.5)))
        lit = palette.nearest(tuple(min(255, int(colour[i] * 1.18 + 18)) for i in range(3))) + (255,)
        for x in range(x0, x1 + 1):
            u = (x - x0) / float(wdt) * 2 - 1                # -1 .. 1 across the mouth
            dy = int(round(bow * (1 - u * u)))               # deepest in the middle
            for k in range(2):
                y = line + dy + k
                if 0 <= y < back.size[1] and mp[x, y][3]:
                    lp[x, y] = lit
    return liq


def composite(back, mask, front, colour, fill):
    """What the game will draw: back, then the liquid up to `fill`, then front."""
    out = back.copy()
    out.alpha_composite(liquid_plate(back, mask, colour, fill))
    out.alpha_composite(front)
    return out


def composite_proof(back, mask, front):
    red = composite(back, mask, front, (217, 69, 92), 0.6)
    blue = composite(back, mask, front, (68, 103, 204), 0.6)
    rp, bp, fp, mp = red.load(), blue.load(), front.load(), mask.load()
    label_same = cavity_diff = 0
    for y in range(front.height):
        for x in range(front.width):
            if fp[x, y][3] == 255 and mp[x, y][3]:          # a label pixel over the cavity
                label_same += rp[x, y] == bp[x, y]
            elif mp[x, y][3] and fp[x, y][3] == FILM_ALPHA:  # the drink seen through the film
                cavity_diff += rp[x, y] != bp[x, y]
    return {'proof_label_pixels_unchanged': label_same, 'proof_cavity_pixels_showing_liquid': cavity_diff}


def run(card_id, outline=1):
    d = os.path.join(RAW, card_id)
    takes = sorted(f for f in os.listdir(d) if f.endswith('.png')) if os.path.isdir(d) else []
    emb = None
    ed = os.path.join(d, 'emblem')
    if os.path.isdir(ed):
        pick = os.path.join(ed, 'pick.png')
        if os.path.exists(pick):
            emb = Image.open(pick).convert('RGBA')
    for t in takes:
        out = os.path.join(STAGING, card_id, t[:-4] + ('_o%d' % outline if outline != 1 else ''))
        a = process_take(card_id, os.path.join(d, t), out, outline=outline, emblem=emb)
        print('  %-14s %-12s %s' % (card_id, t, json.dumps({k: a[k] for k in a if k in
              ('rejected', 'measure', 'liquid_rows', 'off_palette', 'proof_label_pixels_unchanged',
               'proof_cavity_pixels_showing_liquid', 'open_state')})[:300]))


if __name__ == '__main__':
    args = [a for a in sys.argv[1:] if not a.startswith('--')]
    ol = 1
    if '--outline' in sys.argv:
        ol = int(sys.argv[sys.argv.index('--outline') + 1])
    for cid in args:
        run(cid, outline=ol)
