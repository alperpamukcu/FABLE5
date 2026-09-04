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
WALL = 0                  # the drink TOUCHES the outline (the author: "tam kenarina temas etmiyor")
BASE = 3                  # rows of glass under the cavity
MOUTH = 4                 # rows of rim and throat kept as the generator drew them
CELLAR_OUTLINE = 1        # the author, 2026-09-04: "mahzen gorunusunde sadece 1 katman siyah cerceve"
FILM_ALPHA = 77           # 30%: the cavity seen through the front glass
STREAK_ALPHA = 200        # the specular streak stays nearly solid
GENERATED_LABEL = True    # 2026-09-04: the generator draws the label; nothing is pressed


# ── helpers ─────────────────────────────────────────────────────────────────

def alpha_bbox(im):
    return im.split()[3].getbbox()


def lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def opaque(im, cut=128):
    """The silhouette. `cut` is 128 for a master (binary alpha) and 1 for a FRONT plate —
    measured on the pilot's cellar copy: with the film at alpha 77 counted as air, the
    outline pass inked the inside of the walls and the label landed off the bottle."""
    return im.split()[3].point(lambda a: 255 if a >= cut else 0)


def spans(im, cut=128):
    """Per row: (x0, x1) of the opaque run, or None. The silhouette in numbers."""
    a = opaque(im, cut).load()
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
    if crop.width > W or crop.height > H:
        raise ValueError('oversize take %dx%d (canvas %dx%d) — rejected, not rescaled'
                         % (crop.width, crop.height, W, H))
    # Foot THREE rows above the bottom when there is room (2026-09-04 audit): the hand ring
    # takes row 189 and the cellar copy's foot then lands on row 62 of 64, which leaves row
    # 63 for ITS ring — parked at H-2 the foot mapped to the last cellar row and 33 copies
    # shipped with no ring under the foot. A take that fills the canvas (hollow_oak came
    # back 191 tall) simply stands on the last row.
    out = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    out.paste(crop, ((W - crop.width) // 2, max(0, H - crop.height - 3)), crop)
    return out


def restore_body(im, card_id):
    """Give back the body the generator's background removal took (2026-09-04).

    PixelLab's no_background keys the background by colour, and three sealed takes came
    back with their DARK front face keyed out with it: cola_marlow's can body (22% of its
    bbox opaque), orange_grove's and cranberry_north's carton fronts. The room showed
    through them; on the cellar's dark ground they passed for black cans until the redraw
    fragmented them. Every transparent pixel that cannot be reached from the canvas edge
    is inside the vessel, and the vessel's body colour is what the brief asked for: the
    card's label ramp, mid tone, shaded one step darker on the right third of each row
    the way the generator shades the cartons it did keep. Returns (image, filled_count)."""
    w, h = im.size
    a = im.split()[3].load()
    solid = [[a[x, y] >= 128 for x in range(w)] for y in range(h)]
    outside = [[False] * w for _ in range(h)]
    stack = [(x, y) for x in range(w) for y in (0, h - 1) if not solid[y][x]]
    stack += [(x, y) for y in range(h) for x in (0, w - 1) if not solid[y][x]]
    while stack:
        x, y = stack.pop()
        if outside[y][x] or solid[y][x]:
            continue
        outside[y][x] = True
        if x > 0: stack.append((x - 1, y))
        if x < w - 1: stack.append((x + 1, y))
        if y > 0: stack.append((x, y - 1))
        if y < h - 1: stack.append((x, y + 1))
    ramp = brief.CARDS[card_id][3]
    mid = palette.ramp(ramp, 2)
    dark = palette.ramp(ramp, 1)
    out = im.copy(); px = out.load()
    n = 0
    for y in range(h):
        holes = [x for x in range(w) if not solid[y][x] and not outside[y][x]]
        if not holes:
            continue
        x0, x1 = min(holes), max(holes)
        for x in holes:
            shade = x1 - x0 > 6 and (x - x0) > (x1 - x0) * 0.72
            px[x, y] = (dark if shade else mid) + (255,)
            n += 1
    return out, n


def peel_and_ring(im, thickness=1, cut=128, peel=True):
    """Take the generator's own dark rim off the silhouette, then ring it in INK.

    The peel removes boundary pixels darker than lum 46 (ink-like) so an outline never
    thickens by accumulation; the ring adds exactly `thickness` px of Night[0] outside the
    remaining silhouette. thickness=0 gives the lineless variant for the report."""
    px = im.load()
    a = opaque(im, cut).load()
    w, h = im.size
    # peel (the master's own generator rim; NOT on a second pass over a copy that is
    # already ringed - measured on the pilot: the cap pass peeled both rings and put one
    # back, so the cellar copy came out 1px on the left and 2px on the right)
    for _ in range(2 if peel else 0):
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
        a = opaque(im, cut).load()
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
        a = opaque(im, cut).load()
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


# ── geometry ────────────────────────────────────────────────────────────────

def body_and_shoulder(sp):
    """(body_w, shoulder_row, foot_row) from a spans list.

    body_w is the MEDIAN width of the lower body (55%..90% of the silhouette's height),
    not the maximum: the base bow and, at 32px, a one-pixel wobble both moved a
    max-based shoulder down the bottle and dropped the cellar label onto the foot."""
    rows = [i for i, s in enumerate(sp) if s]
    top, foot = rows[0], rows[-1]
    hgt = foot - top + 1
    lower = [sp[y][1] - sp[y][0] + 1 for y in rows if top + 0.55 * hgt <= y <= top + 0.90 * hgt]
    lower.sort()
    body_w = lower[len(lower) // 2] if lower else max(sp[y][1] - sp[y][0] + 1 for y in rows)
    shoulder = next((y for y in rows if (sp[y][1] - sp[y][0] + 1) >= 0.88 * body_w), top)
    return body_w, shoulder, foot


# ── cavity, plates ──────────────────────────────────────────────────────────

def cavity(im):
    """The glass interior as a mask image: the span between the walls, from the shoulder
    (where the silhouette first reaches 90% of its body width, walking down) to BASE rows
    above the foot. Pure geometry — there is no label to confuse it (PLAN §4a)."""
    sp = spans(im)
    rows = [i for i, s in enumerate(sp) if s]
    body_w, shoulder, foot = body_and_shoulder(sp)
    m = Image.new('RGBA', im.size, (0, 0, 0, 0))
    mp = m.load()
    # FROM THE MOUTH, not the shoulder (the author, 2026-08-27: "omuz hizasinin ustu ile bos
    # halinin rengi ayni olmali"). A bottle's inside runs up the neck to the lip; starting the
    # cavity at the shoulder left the neck opaque cream over a blue-grey body, which read as
    # a neck full of something. MOUTH rows keep the generator's rim and throat.
    a = opaque(im).load()
    for y in range(rows[0] + MOUTH, foot - BASE + 1):
        s = sp[y]
        if not s:
            continue
        x0, x1 = s[0] + WALL, s[1] - WALL
        for x in range(x0, x1 + 1):
            # the span, but only where the master is opaque: a one-pixel notch in a
            # silhouette (bourbon_redline, 64,61) was drink with no glass over it
            if a[x, y]:
                mp[x, y] = (255, 255, 255, 255)
    return m, shoulder


def liquid_mask(interior, im):
    """Where the DRINK may go: the WHOLE interior, neck included (the author, 2026-09-04:
    "sıvıyı çevirdiğinde ağza da dolması gerekiyor"). "Full means the shoulder" is a rule
    about VOLUME, not about the mask: the runtime scales the fill so that 1.0 is the
    volume below the shoulder when upright — tilt the bottle and that same volume runs
    into the neck, as it does in a real one (BottleArt.ShoulderFraction)."""
    return interior.copy()


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
            if not mp[x, y][3]:
                continue          # the back is opaque exactly where the mask is (a notch is neither)
            t = abs((x - x0) / float(span) - 0.5) * 2.0       # 0 centre .. 1 wall
            c = tuple(int(light[i] * (1 - t) + dark[i] * t) for i in range(3))
            bp[x, y] = palette.nearest(c) + (255,)
            r, g, b, a = fp[x, y]
            if a:
                # Printed pixels (the generated label, its text and logo) stay OPAQUE: they
                # are far from the glass tone in luma or chroma. Glass-like pixels become
                # the film the drink shows through. Measured per bottle by the proof gate.
                lg = lum(glass); gch = max(glass) - min(glass)
                ch = max(r, g, b) - min(r, g, b)
                printed = GENERATED_LABEL and (abs(lum((r, g, b)) - lg) > 46 or ch > gch + 34)
                # THE RING IS NEVER FILM (2026-09-04 audit): on near-black glass (liqueur_kafa,
                # rum_windward) the INK outline sat within 46 luma of the glass tone and went
                # to alpha 77 — a see-through edge with the room showing at the wall. Ink, and
                # any pixel on the silhouette's edge, keeps full alpha.
                edge = (x == 0 or y == 0 or x == w - 1 or y == h - 1 or not fp[x - 1, y][3]
                        or not fp[x + 1, y][3] or not fp[x, y - 1][3] or not fp[x, y + 1][3])
                ink = lum((r, g, b)) < 40
                bright = lum((r, g, b)) > lg + 40 and not printed
                fp[x, y] = (r, g, b, 255 if (printed or edge or ink) else (STREAK_ALPHA if bright else FILM_ALPHA))
    return back, front


# ── the label ───────────────────────────────────────────────────────────────

# BRAND_WORD lives in brief.py (one table; process.py's copy kept the pre-rename ids)


def label_rect(im, fam, want_h):
    """Where the label goes: on the body's widest band, a fraction of the body's width, and
    as TALL as its content needs (want_h) — a ratio-sized label squeezed the emblem to a
    third of itself on the pilot, which is ten pixels of noise."""
    sp = spans(im, 1)
    body_w, shoulder, foot = body_and_shoulder(sp)
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
    word = brief.BRAND_WORD.get(card_id, card_id.split('_')[0].upper())
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


CAP_TONE = {'vodka': 'Graphite', 'mixer': 'Graphite', 'gin': 'Night', 'rum': 'Amber',
            'whiskey': 'Amber', 'tequila': 'Amber', 'liqueur': 'Graphite'}


def draw_cap(im, fam):
    """A small cap on the cellar copy (the master is uncapped). Sits on the mouth: as wide
    as the neck plus one pixel each side, three rows tall, a lit top row, ringed by the
    outline pass that follows."""
    sp = spans(im, 1)
    rows = [i for i, s in enumerate(sp) if s]
    if not rows:
        return im
    top = rows[0]
    x0, x1 = sp[top]
    ramp = CAP_TONE.get(fam, 'Graphite')
    body = palette.ramp(ramp, 3) + (255,)
    lit = palette.ramp(ramp, 4) + (255,)
    dark = palette.ramp(ramp, 1) + (255,)
    px = im.load()
    # THE CAP SITS ON THE MOUTH, NOT ABOVE IT (2026-09-04 audit): stacked three rows above
    # the mouth it made every copy taller than its master and was clipped off the tallest
    # takes at row 0. A capped bottle's cap COVERS the mouth: one row above the rim, three
    # over it — the copy keeps its master's proportion and needs one row of headroom.
    y_top = max(0, top - 1)
    for y in range(y_top, y_top + 4):
        if y >= im.height:
            continue
        for x in range(x0 - 1, x1 + 2):
            if 0 <= x < im.width:
                px[x, y] = lit if y == y_top else (dark if y == y_top + 3 else body)
    return im


def press_label_small(im, card_id, emblem=None):
    """The cellar label: a colour field, the band, and the emblem at a quarter - NO TEXT
    (the author: "mahzen boyutunda sadece amblemler veya sekiller olsun, yazilar gozukmesin")."""
    fam, _, _, lr, br, _ = brief.CARDS[card_id]
    sp = spans(im, 1)
    body_w, shoulder, foot = body_and_shoulder(sp)
    body_h = foot - shoulder
    lw = max(6, int(body_w * 0.62))
    lh = max(7, int(body_h * 0.30))
    cy = shoulder + int(body_h * 0.55)
    cx = (sp[cy][0] + sp[cy][1]) // 2
    x0, y0 = cx - lw // 2, cy - lh // 2
    field = palette.ramp(lr, 3) + (255,)
    edge = palette.ramp(lr, 0) + (255,)
    band = palette.ramp(br, 2) + (255,)
    px = im.load()
    for y in range(y0, y0 + lh):
        for x in range(x0, x0 + lw):
            if 0 <= x < im.width and 0 <= y < im.height:
                px[x, y] = edge if (x in (x0, x0 + lw - 1) or y in (y0, y0 + lh - 1)) else field
    by = y0 + lh - 3
    for x in range(x0 + 1, x0 + lw - 1):
        px[x, by] = band
    if emblem is not None:
        e = palette.quantize(emblem)
        eb = alpha_bbox(e)
        if eb:
            e = e.crop(eb)
            e = mode_downsample(e, 4) if max(e.size) > 8 else e
            if e.width <= lw - 2 and e.height <= lh - 5:
                im.paste(e, (x0 + (lw - e.width) // 2, y0 + 1), e)
    return im


def coverage_silhouette(im, f=3, need=5):
    """Binary silhouette at 1/f by AREA coverage: a cell is solid when >= need of its f*f
    source pixels are opaque. Silhouette-first, colour never (the research's scaffold)."""
    w, h = im.width // f, im.height // f
    a = im.split()[3].load()
    out = [[False] * w for _ in range(h)]
    for y in range(h):
        for x in range(w):
            n = 0
            for dy in range(f):
                for dx in range(f):
                    if a[x * f + dx, y * f + dy] >= 128:
                        n += 1
            out[y][x] = n >= need
    # FILL EVERY INTERIOR HOLE (2026-09-04): a hole left inside the silhouette is a
    # transparent cell with solid neighbours, and the outline pass inks exactly those — a
    # can's thin top ellipse and a carton's gable left holes that came out as black spots
    # (cola_marlow: 152 ink cells for a 70-cell ring). Anything not reachable from the
    # canvas edge through transparent cells is inside the vessel, and inside is solid.
    outside = [[False] * w for _ in range(h)]
    stack = [(x, y) for x in range(w) for y in (0, h - 1) if not out[y][x]]
    stack += [(x, y) for y in range(h) for x in (0, w - 1) if not out[y][x]]
    while stack:
        x, y = stack.pop()
        if outside[y][x] or out[y][x]:
            continue
        outside[y][x] = True
        if x > 0: stack.append((x - 1, y))
        if x < w - 1: stack.append((x + 1, y))
        if y > 0: stack.append((x, y - 1))
        if y < h - 1: stack.append((x, y + 1))
    for y in range(h):
        for x in range(w):
            if not out[y][x] and not outside[y][x]:
                out[y][x] = True
    return out


def print_mask(master, interior, glass):
    """Printed pixels on the master (label paper, text, logo): far from the glass tone."""
    px, ip = master.load(), interior.load()
    lg = lum(glass); gch = max(glass) - min(glass)
    w, h = master.size
    m = [[False] * w for _ in range(h)]
    for y in range(h):
        for x in range(w):
            if not ip[x, y][3]:
                continue
            r, g, b, a = px[x, y]
            ch = max(r, g, b) - min(r, g, b)
            m[y][x] = abs(lum((r, g, b)) - lg) > 46 or ch > gch + 34
    return m


def label_block(master, interior, glass):
    """The label as (bbox, paper colour, ink colour) measured on the master, or None."""
    pm = print_mask(master, interior, glass)
    w, h = master.size
    ys = [y for y in range(h) if any(pm[y])]
    if not ys:
        return None
    # the densest horizontal band of print is the label; take rows with >= 25% of the max
    counts = [sum(pm[y]) for y in range(h)]
    top = max(counts)
    rows = [y for y in range(h) if counts[y] >= top * 0.25]
    y0, y1 = rows[0], rows[-1]
    xs = [x for y in range(y0, y1 + 1) for x in range(w) if pm[y][x]]
    x0, x1 = min(xs), max(xs)
    px = master.load()
    paper = Counter(); ink = Counter()
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            c = px[x, y][:3]
            (ink if pm[y][x] else paper)[c] += 1
    paper_c = paper.most_common(1)[0][0] if paper else (242, 232, 213)
    ink_c = ink.most_common(1)[0][0] if ink else (13, 8, 19)
    # the darkest frequent print colour is the text/emblem, the most frequent print
    # colour that is NOT near paper is the accent
    return (x0, y0, x1 - x0 + 1, y1 - y0 + 1), paper_c, ink_c


def box_down(im, f=3, cut=0.5):
    """Area average of the OPAQUE pixels in each f*f cell (a transparent neighbour never
    darkens an edge), alpha hardened at `cut` coverage. Returns (small, coverage)."""
    w, h = im.width // f, im.height // f
    px = im.load()
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0)); op = out.load()
    cov = [[0.0] * w for _ in range(h)]
    for y in range(h):
        for x in range(w):
            r = g = b = n = 0
            for dy in range(f):
                for dx in range(f):
                    q = px[x * f + dx, y * f + dy]
                    if q[3] >= 128:
                        r += q[0]; g += q[1]; b += q[2]; n += 1
            c = n / float(f * f); cov[y][x] = c
            if c >= cut:
                op[x, y] = (r // n, g // n, b // n, 255)
    return out, cov


def fill_holes(im):
    """Every transparent pixel not reachable from the canvas edge becomes the mean of its
    opaque neighbours (a can's top ellipse, a carton's gable leave pinholes at 1/3)."""
    w, h = im.size
    px = im.load()
    solid = [[px[x, y][3] >= 128 for x in range(w)] for y in range(h)]
    outside = [[False] * w for _ in range(h)]
    stack = [(x, y) for x in range(w) for y in (0, h - 1) if not solid[y][x]]
    stack += [(x, y) for y in range(h) for x in (0, w - 1) if not solid[y][x]]
    while stack:
        x, y = stack.pop()
        if outside[y][x] or solid[y][x]:
            continue
        outside[y][x] = True
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= nx < w and 0 <= ny < h:
                stack.append((nx, ny))
    for _ in range(4):
        todo = [(x, y) for y in range(h) for x in range(w) if not solid[y][x] and not outside[y][x]]
        if not todo:
            break
        for x, y in todo:
            ns = [px[nx, ny] for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1))
                  if 0 <= nx < w and 0 <= ny < h and px[nx, ny][3] >= 128]
            if ns:
                px[x, y] = (sum(c[0] for c in ns) // len(ns), sum(c[1] for c in ns) // len(ns),
                            sum(c[2] for c in ns) // len(ns), 255)
                solid[y][x] = True
    return im


def label_paper_bbox(master, interior, glass, lb):
    """Grow the print band's bbox outward over the label PAPER (rows/cols that are >= 55%
    paper-coloured), so the block drawn at cellar size is the whole label, not its text."""
    (x0, y0, w0, h0), paper, ink = lb
    x1, y1 = x0 + w0 - 1, y0 + h0 - 1
    px = master.load()
    W_, H_ = master.size
    lp = lum(paper)
    pch = max(paper) - min(paper)

    def is_paper(x, y):
        if not (0 <= x < W_ and 0 <= y < H_) or not px[x, y][3]:
            return False
        c = px[x, y][:3]
        return abs(lum(c) - lp) <= 30 and abs((max(c) - min(c)) - pch) <= 40

    def frac(cells):
        cells = list(cells)
        return sum(1 for c in cells if is_paper(*c)) / float(max(1, len(cells)))
    for _ in range(40):
        grew = False
        if x0 > 0 and frac((x0 - 1, y) for y in range(y0, y1 + 1)) >= 0.55:
            x0 -= 1; grew = True
        if x1 < W_ - 1 and frac((x1 + 1, y) for y in range(y0, y1 + 1)) >= 0.55:
            x1 += 1; grew = True
        if y0 > 0 and frac((x, y0 - 1) for x in range(x0, x1 + 1)) >= 0.55:
            y0 -= 1; grew = True
        if y1 < H_ - 1 and frac((x, y1 + 1) for x in range(x0, x1 + 1)) >= 0.55:
            y1 += 1; grew = True
        if not grew:
            break
    return x0, y0, x1 - x0 + 1, y1 - y0 + 1


def label_region(master, min_px=60, merge=4):
    """The label on the master as (bbox, paper, mark), or None.

    label_block measured "print" as distance from the GLASS tone, which is the cavity's
    cool grey — on a cream-bodied bottle the whole body was print and the "label" was the
    bottle (vodka_astra: 46x163). Here the reference is the BODY colour (the mode of the
    lower body, three pixels in from the edge); print is what differs from it; the label
    is the largest connected blob of print after a small dilation (letters merge into a
    word). paper = the dominant colour inside the bbox, mark = the dominant print colour."""
    W_, H_ = master.size
    a = opaque(master, 128).load()
    px = master.load()
    sil = [[bool(a[x, y]) for x in range(W_)] for y in range(H_)]
    # three erosions: shading and the rim near the edge are not print
    inner = sil
    for _ in range(3):
        nxt = [[False] * W_ for _ in range(H_)]
        for y in range(1, H_ - 1):
            for x in range(1, W_ - 1):
                nxt[y][x] = (inner[y][x] and inner[y - 1][x] and inner[y + 1][x]
                             and inner[y][x - 1] and inner[y][x + 1])
        inner = nxt
    rows = [y for y in range(H_) if any(sil[y])]
    if not rows:
        return None
    top, foot = rows[0], rows[-1]
    zone0 = top + int((foot - top) * 0.35)
    body = Counter()
    for y in range(zone0, foot - 2):
        for x in range(W_):
            if inner[y][x] and lum(px[x, y][:3]) >= 60:
                body[palette.nearest(px[x, y][:3])] += 1
    if not body:
        return None
    body_c = body.most_common(1)[0][0]
    bl = lum(body_c)

    # Print is what stands CLEARLY off the body: darker by 55 (a label's border and its
    # text on a light bottle) or lighter by 55 (a light label on dark glass, white
    # lettering on a can). A softer threshold caught the highlight stripe and made the
    # whole body the label. Both polarities are tried; the bigger blob wins.
    def blob(pm):
        dil = [row[:] for row in pm]
        for _ in range(merge):
            nxt = [row[:] for row in dil]
            for y in range(H_):
                for x in range(W_):
                    if dil[y][x]:
                        for dy in (-1, 0, 1):
                            for dx in (-1, 0, 1):
                                nx, ny = x + dx, y + dy
                                if 0 <= nx < W_ and 0 <= ny < H_ and inner[ny][nx]:
                                    nxt[ny][nx] = True
            dil = nxt
        seen = [[False] * W_ for _ in range(H_)]
        best = None
        for y in range(H_):
            for x in range(W_):
                if not dil[y][x] or seen[y][x]:
                    continue
                stack = [(x, y)]; seen[y][x] = True; cells = []
                while stack:
                    cx, cy = stack.pop(); cells.append((cx, cy))
                    for nx, ny in ((cx - 1, cy), (cx + 1, cy), (cx, cy - 1), (cx, cy + 1)):
                        if 0 <= nx < W_ and 0 <= ny < H_ and dil[ny][nx] and not seen[ny][nx]:
                            seen[ny][nx] = True; stack.append((nx, ny))
                n = sum(1 for cx, cy in cells if pm[cy][cx])
                if n >= min_px and (best is None or n > best[0]):
                    best = (n, cells)
        return best
    # labels live on the body: the top 30% (cap, lid, neck ring) and the bottom 10% (the
    # foot's dark band — tequila_sol_viejo's label was measured on its base) are never it
    lid = top + int((foot - top) * 0.30)
    heel = foot - int((foot - top) * 0.10)
    dark = [[lid <= y <= heel and inner[y][x] and lum(px[x, y][:3]) < bl - 55 for x in range(W_)] for y in range(H_)]
    light = [[lid <= y <= heel and inner[y][x] and lum(px[x, y][:3]) > bl + 55 for x in range(W_)] for y in range(H_)]
    bd, bli = blob(dark), blob(light)
    if bd is None and bli is None:
        return None
    if bli is None or (bd is not None and bd[0] >= bli[0]):
        best, pm = bd, dark
    else:
        best, pm = bli, light
    xs = [c[0] for c in best[1]]; ys = [c[1] for c in best[1]]
    x0, x1 = max(0, min(xs) + merge - 1), min(W_ - 1, max(xs) - merge + 1)
    y0, y1 = max(0, min(ys) + merge - 1), min(H_ - 1, max(ys) - merge + 1)
    if x1 - x0 < 6 or y1 - y0 < 4:
        return None
    paper = Counter(); mark = Counter()
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if not a[x, y]:
                continue
            c = palette.nearest(px[x, y][:3])
            (mark if pm[y][x] else paper)[c] += 1
    paper_c = paper.most_common(1)[0][0] if paper else body_c
    mark_c = mark.most_common(1)[0][0] if mark else (13, 8, 19)
    return (x0, y0, x1 - x0 + 1, y1 - y0 + 1), paper_c, mark_c


def thin_ring(before, after):
    """An outline of ONE pixel at the corners too (2026-09-04 audit): where the silhouette
    steps sideways by two cells the 4-connected ring fills the inner corner of its L, a
    second ink pixel with no air beside it. Ring cells (transparent in `before`, ink in
    `after`) that touch no transparent 4-neighbour are dropped; the ring stays connected
    through the diagonal, which is how a pixel artist draws that corner."""
    bp, ap = before.load(), after.load()
    w, h = after.size
    drop = []
    for y in range(h):
        for x in range(w):
            if bp[x, y][3] or not ap[x, y][3]:
                continue
            air = any(0 <= nx < w and 0 <= ny < h and not ap[nx, ny][3]
                      for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)))
            if not air and not (x == 0 or y == 0 or x == w - 1 or y == h - 1):
                drop.append((x, y))
    # The corner cell is not opened (a transparent cell sealed off by ink is a pinhole at
    # every shoulder); it becomes BODY — the colour of the opaque cell beside it, at full
    # alpha, a wall pixel — so the outline runs diagonally and the fill meets it.
    for x, y in drop:
        fill = None
        for nx, ny in ((x + 1, y), (x, y + 1), (x - 1, y), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h and bp[nx, ny][3] and ap[nx, ny][:3] != INK:
                fill = ap[nx, ny][:3]; break
        ap[x, y] = (fill + (255,)) if fill else ap[x, y]
    return after


def cellar_box(master, interior, glass, card_id, emblem=None):
    """THE CELLAR COPY (round seven, 2026-09-04): the master AREA-AVERAGED to 32x64, polished.

    The redraw of round six came out skewed and lifeless in the room ("şişeler yamık ve
    kaliteleri çok düşük, üstlerinde etiket yok"); a generated 32x64 (img2img over this
    very image) added noise and drift. The box-filtered master itself was the most faithful
    thing on the pilot sheet: proportions, perspective, colours and the label's place all
    survive a 1/3 area average. What it lacks is done here: the generator's rim is peeled
    first so it cannot darken the edge; opaque means half a cell covered; pinholes are
    filled; every colour snaps to the 55; the label is sharpened to its paper and ink with
    a one-pixel darker border so it READS at this size; the glass gets its cap; and the
    outline is put back as exactly one ring. Returns (back_c, mask_c, front_c)."""
    fam = brief.family(card_id)
    peeled = peel_and_ring(master, 0)
    small, cov = box_down(peeled, 3, 0.5)
    small = fill_holes(small)
    imask, icov = box_down(interior, 3, 0.5)
    # HEADROOM (2026-09-04 audit): a take that fills the canvas lands its mouth on row 0 and
    # the drawn cap (three rows above the mouth) is clipped. If there is air under the foot,
    # the copy slides down to give the cap its rows; the interior slides with it.
    rows_ = [y for y in range(small.height) if any(small.load()[x, y][3] for x in range(small.width))]
    if rows_ and rows_[0] < 1:
        room = small.height - 1 - rows_[-1]
        shift = min(1 - rows_[0], room)
        if shift > 0:
            def slide(im):
                out = Image.new('RGBA', im.size, (0, 0, 0, 0))
                out.paste(im, (0, shift), im)
                return out
            small, imask = slide(small), slide(imask)
    w, h = small.size
    px = small.load()
    for y in range(h):
        for x in range(w):
            if px[x, y][3]:
                px[x, y] = palette.nearest(px[x, y][:3]) + (255,)
    # the label: a two-colour snap (paper / mark) inside its box so the print is crisp,
    # and on glass a one-pixel darker border so the paper reads against a like-toned body
    lb = label_region(master)
    label_cells = set()
    if lb is not None:
        (lx, ly, lw, lh), paper_q, mark_q = lb
        pl = lum(paper_q)
        edge_q = palette.nearest(tuple(int(c * 0.62) for c in paper_q) if pl > 100
                                 else tuple(min(255, int(c * 1.6) + 20) for c in paper_q))
        span = sum((paper_q[i] - mark_q[i]) ** 2 for i in range(3)) ** 0.5
        bx0, by0 = lx // 3, ly // 3
        bx1, by1 = (lx + lw - 1) // 3, (ly + lh - 1) // 3
        if bx1 - bx0 >= 3 and by1 - by0 >= 2:
            for y in range(by0, by1 + 1):
                for x in range(bx0, bx1 + 1):
                    if not (0 <= x < w and 0 <= y < h) or not px[x, y][3]:
                        continue
                    label_cells.add((x, y))
                    at_edge = any(not (0 <= nx < w and 0 <= ny < h) or not px[nx, ny][3]
                                  for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)))
                    if fam not in brief.SEALED and (x in (bx0, bx1) or y in (by0, by1)) and not at_edge:
                        px[x, y] = edge_q + (255,)
                        continue
                    # snap toward the MARK only: a cell with a third of the print in it is
                    # print. Snapping toward the paper too erased thin lettering (LOCA's
                    # box-filtered strokes are pink, nearer red than cream, and vanished).
                    c = px[x, y][:3]
                    dm = sum((c[i] - mark_q[i]) ** 2 for i in range(3)) ** 0.5
                    if span > 40 and dm < span * 0.66:
                        px[x, y] = mark_q + (255,)
    # NO DOUBLED OUTLINE (2026-09-04 audit): the master's dark shading at the shoulder and
    # foot corners survives the box filter as INK on the silhouette's edge, and the ring
    # then makes it two pixels thick there. An edge cell that is ink and not label goes one
    # step up the Night ramp — still dark, no longer the outline's colour.
    night1 = palette.ramp('Night', 1)
    for y in range(h):
        for x in range(w):
            if not px[x, y][3] or (x, y) in label_cells or px[x, y][:3] != palette.INK:
                continue
            # eight neighbours, not four: the ring lands on every transparent cell that is
            # 4-adjacent to the silhouette, so an ink cell touching the outside only
            # diagonally still ends up beside the ring (the shoulder-corner pairs)
            edge = any(not (0 <= x + dx < w and 0 <= y + dy < h) or not px[x + dx, y + dy][3]
                       for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy)
            if edge:
                px[x, y] = night1 + (255,)
    if fam in brief.SEALED:
        front = thin_ring(small, peel_and_ring(small.copy(), 1, cut=1, peel=False))
        return None, None, front
    # glass: the interior at this size, a cool back gradient behind a film
    ip = imask.load()
    inner = [[False] * w for _ in range(h)]
    for y in range(1, h - 1):
        for x in range(1, w - 1):
            inner[y][x] = bool(ip[x, y][3] and ip[x - 1, y][3] and ip[x + 1, y][3]
                               and ip[x, y - 1][3] and ip[x, y + 1][3] and px[x, y][3])
    rows = [y for y in range(h) if any(px[x, y][3] for x in range(w))]
    top = rows[0] if rows else 0
    for y in range(top, min(top + 3, h)):
        inner[y] = [False] * w
    pm = print_mask(master, interior, glass)
    gq = palette.nearest(glass)
    gl = lum(gq); gch = max(gq) - min(gq)
    k = 0.60 if (gl > 150 and gch < 40) else 0.25
    cool = tuple(int(gq[i] * (1 - k) + (150, 200, 235)[i] * k) for i in range(3))
    light = palette.nearest(tuple(min(255, int(c * 0.90)) for c in cool))
    dark = palette.nearest(tuple(int(c * 0.58) for c in cool))
    back = Image.new('RGBA', (w, h), (0, 0, 0, 0)); bp = back.load()
    mask = Image.new('RGBA', (w, h), (0, 0, 0, 0)); mp = mask.load()
    front = small.copy(); fp = front.load()
    for y in range(h):
        xs = [x for x in range(w) if inner[y][x]]
        for x in xs:
            x0, x1 = xs[0], xs[-1]; span = max(1, x1 - x0)
            tt = abs((x - x0) / float(span) - 0.5) * 2.0
            c = tuple(int(light[i] * (1 - tt) + dark[i] * tt) for i in range(3))
            bp[x, y] = palette.nearest(c) + (255,)
            mp[x, y] = (255, 255, 255, 255)
            # printed cells (>= a third of the source pixels are print) and the label stay opaque
            n = sum(1 for dy in range(3) for dx in range(3) if pm[y * 3 + dy][x * 3 + dx])
            if (x, y) in label_cells or n >= 3:
                continue
            fp[x, y] = fp[x, y][:3] + (FILM_ALPHA,)
    front = draw_cap(front, fam)
    front = thin_ring(front, peel_and_ring(front.copy(), 1, cut=1, peel=False))
    return back, mask, front


def cellar_copy(front_bare, back, mask, card_id, emblem=None, outline=CELLAR_OUTLINE):
    """The 32x64 set, REBUILT (the author: "mahzen boyutundaki gorseller kusursuz olmali").
    Measured on the pilot: a plain third of the master LOSES its outline - a 3x3 block at
    the edge is one ink pixel and two glass pixels, so the mode picks glass (81 of 140 edge
    pixels not ink). So the interior is taken by mode, the outline is put back in ink at
    CELLAR_OUTLINE, the cap is drawn, and the label is pressed AT this size, not shrunk."""
    fam = brief.family(card_id)
    fc = downsample_front(front_bare, 3)
    fc = peel_and_ring(fc, outline, cut=1, peel=False)
    fc = draw_cap(fc, fam)
    fc = peel_and_ring(fc, 1, cut=1, peel=False)   # ring the cap; the rest is already ringed
    if not GENERATED_LABEL:
        fc = press_label_small(fc, card_id, emblem)
    bc = mode_downsample(back, 3)
    mc = mode_downsample(mask, 3)
    return bc, mc, fc


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
        im, restored = restore_body(im, card_id)
        if restored:
            print('  %s: restored %d body pixels the background removal took' % (card_id, restored))
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
        sprite = master.copy() if GENERATED_LABEL else press_label(master.copy(), card_id, emblem)
        sprite.save(os.path.join(out_dir, 'v4_%s.png' % card_id))
        # sealed: the same renderer (no interior), so cans and cartons get one ring too
        _, _, small = cellar_box(master, cavity(master)[0], glass_tone(master, cavity(master)[0]), card_id, emblem)
        small.save(os.path.join(out_dir, 'v4_%s_c.png' % card_id))
        audit['plates'] = ['sprite', 'cellar']
    else:
        interior, shoulder = cavity(master)          # mouth to base: the glass inside
        mask = liquid_mask(interior, master)          # shoulder to base: where drink goes
        glass = glass_tone(master, interior)
        audit['glass'] = glass
        audit['liquid_rows'] = liquid_rows(master, interior, glass)
        audit['cavity_rows'] = sum(1 for s in spans(interior) if s)
        audit['fill_rows'] = sum(1 for s in spans(mask) if s)
        back, front_bare = plates(master, interior, glass)
        # the hand front: the OPEN master with the full label (emblem + wordmark)
        front = front_bare.copy() if GENERATED_LABEL else press_label(front_bare.copy(), card_id, emblem)
        back.save(os.path.join(out_dir, 'v4_%s_back.png' % card_id))
        mask.save(os.path.join(out_dir, 'v4_%s_mask.png' % card_id))
        front.save(os.path.join(out_dir, 'v4_%s_front.png' % card_id))
        # the cellar set, rebuilt at its own size: outline, cap, emblem-only label
        bc, mc, fc = cellar_box(master, interior, glass, card_id, emblem)
        bc.save(os.path.join(out_dir, 'v4_%s_back_c.png' % card_id))
        mc.save(os.path.join(out_dir, 'v4_%s_mask_c.png' % card_id))
        fc.save(os.path.join(out_dir, 'v4_%s_front_c.png' % card_id))
        # the liquid proof: red and blue composites must agree on every label pixel and
        # disagree on the cavity
        proof = composite_proof(back, mask, front)
        audit.update(proof)
        audit['plates'] = ['back', 'mask', 'front', '+cellar back/mask/front']
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


def run(card_id, outline=1, raw_dir=None):
    """raw_dir lets an archived take set (raw/<card>_capped_v1) be processed as its card."""
    d = os.path.join(RAW, raw_dir or card_id)
    takes = sorted(f for f in os.listdir(d) if f.endswith('.png')) if os.path.isdir(d) else []
    emb = None
    ed = os.path.join(d, 'emblem')
    if os.path.isdir(ed):
        pick = os.path.join(ed, 'pick.png')
        if os.path.exists(pick):
            emb = Image.open(pick).convert('RGBA')
    for t in takes:
        out = os.path.join(STAGING, raw_dir or card_id, t[:-4] + ('_o%d' % outline if outline != 1 else ''))
        a = process_take(card_id, os.path.join(d, t), out, outline=outline, emblem=emb)
        print('  %-14s %-12s %s' % (card_id, t, json.dumps({k: a[k] for k in a if k in
              ('rejected', 'measure', 'liquid_rows', 'off_palette', 'proof_label_pixels_unchanged',
               'proof_cavity_pixels_showing_liquid', 'open_state')})[:300]))


if __name__ == '__main__':
    argv = sys.argv[1:]
    ol = 1
    raw = None
    if '--outline' in argv:
        i = argv.index('--outline'); ol = int(argv[i + 1]); del argv[i:i + 2]
    if '--raw' in argv:
        i = argv.index('--raw'); raw = argv[i + 1]; del argv[i:i + 2]
    for cid in argv:
        run(cid, outline=ol, raw_dir=raw)
