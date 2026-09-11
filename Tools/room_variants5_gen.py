# -*- coding: utf-8 -*-
"""FIFTH ROUND (2026-09-12, the author: "farklı tavanlar, televizyon, trio tablolar, sağ duvar
poster, neon ışık, mobilya masa yansıma olmasın, halı, zemin, bitkiler, tezgah, bira musluğu,
damla paspası, garnitür paspası üret ortama uygun, o adayları da değerlendirelim").

Every rung of the room the author asked to see more of, generated against the room's own
palette and mapped into the room by the geometry the earlier rounds measured. Three of most
things: a ladder wants rungs to CHOOSE between, not a catalogue.

WHAT IS GENERATED, AND HOW IT LANDS
  ceilings   a flat tileable texture, warped into the cream trapezoid (round 3's mapping)
  floors     a flat tileable texture, projected as a ground plane (round 4's, the honest one)
  counter    the same ground-plane projection at BAR HEIGHT: the top slab is a horizontal
             plane too. Only the slab — the cellar under it holds the bottles and stays.
  rug, mats  the room already fixes their silhouette and their perspective, so what is
             generated is the TEXTURE and it is stamped into the piece's own alpha
  posters    printed paper, hung on the right wall (the author's answer to round 4's
             question: the left wall is the window's, posters go right)
  tv, neon,  drawn whole, cut out, stood or hung where the piece they replace stands
  taps,
  plants,
  tables     — and matte: "mobilya masa yansıma olmasın", no reflection under them
  trio       three small framed pictures per set, hung as one piece by Tools/pics_trio.py

  py -3 -X utf8 Tools/room_variants5_gen.py take     submit / collect (re-run until done)
  py -3 -X utf8 Tools/room_variants5_gen.py build    map everything into the room
"""
import io
import json
import os
import re
import sys

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(HERE, 'room_variants5')
STATE = os.path.join(OUT, 'state.json')
FIXT = os.path.join(ROOT, 'Assets', 'Resources', 'Fixtures')
# Where the room stands its counter art (measured off the stage: the sprite's top row lands
# on room row 238 and the rest of it runs off the bottom of the picture).
COUNTER_TOP_Y = 238

sys.path.insert(0, HERE)
import pixellab                                    # noqa: E402
import room_variants3_gen as r3                    # noqa: E402  ceiling warp
import room_variants4_gen as r4                    # noqa: E402  floor projection, hanging

ROOM = ('pixel art, warm 1980s Miami bar interior palette, magenta and teal accents on '
        'dark wood and brass, flat colours with hard pixel edges, no dithering, no text, '
        'no watermark')
MATTE = ('completely matte and flat, no reflections, no mirror, no gloss, no shine, no '
         'specular highlights, no reflected light, ')
TILE = ('a flat surface texture seen straight on, filling the whole frame edge to edge, '
        'tileable, even and repeating, no perspective, no vanishing point, ' + MATTE)
PROP = ('one object on a transparent background, seen straight on from a low eye level, '
        'nothing else in frame, no floor, no wall, no shadow, no reflection, ' + MATTE)
FRAMED = ('one small framed picture hanging flat on a wall, the frame filling the whole '
          'frame edge to edge, seen straight on, transparent background outside the frame, '
          + MATTE)
POSTER = ('a rectangular paper poster printed edge to edge, the printed image filling the '
          'whole rectangle right to the paper edge with a narrow plain paper margin, no '
          'frame, no glass, seen straight on, ' + MATTE)

JOBS = {
    # ── ceilings (tileable; warped into the trapezoid) ────────────────────────
    'ceil_coffer':   (128, 128, 5101, TILE + 'a coffered ceiling of deep square wooden '
                      'panels with slim brass beading between them, ' + ROOM),
    'ceil_press':    (128, 128, 5103, TILE + 'a pressed tin ceiling of repeating square '
                      'rosettes, painted matte cream with faded coral in the hollows, ' + ROOM),
    'ceil_cane':     (128, 128, 5105, TILE + 'woven cane and rattan panels in a wooden '
                      'grid, warm straw colours, ' + ROOM),

    # ── floors (tileable; projected as a ground plane) ────────────────────────
    'floor_terra':   (128, 128, 5111, TILE + 'terracotta floor tiles in a running bond, '
                      'warm clay with pale grout, ' + ROOM),
    'floor_teal':    (128, 128, 5113, TILE + 'glazed teal and cream floor tiles in a small '
                      'diamond pattern, ' + ROOM),
    'floor_board':   (128, 128, 5115, TILE + 'wide dark walnut floorboards running in one '
                      'direction with visible grain, ' + ROOM),

    # ── the bar top (tileable; projected at bar height) ───────────────────────
    'bar_terrazzo':  (128, 128, 5121, TILE + 'dark terrazzo with pink and teal chips, ' + ROOM),
    'bar_marble':    (128, 128, 5123, TILE + 'black marble with fine white and magenta '
                      'veining, ' + ROOM),
    'bar_zinc':      (128, 128, 5125, TILE + 'a worn zinc bar top, soft grey metal with '
                      'faint scratches, ' + ROOM),

    # ── the rug and the two mats (texture only; stamped into their silhouettes) ─
    'rug_kilim':     (96, 96, 5131, TILE + 'a woven kilim rug pattern, coral and teal '
                      'diamonds on cream, ' + ROOM),
    'rug_wave':      (96, 96, 5133, TILE + 'a rug woven with rolling wave stripes in teal, '
                      'sand and dusty pink, ' + ROOM),
    'rug_leopard':   (96, 96, 5135, TILE + 'a rug with a bold spotted animal pattern in '
                      'amber and black, ' + ROOM),
    'mat_ribbed':    (64, 64, 5141, TILE + 'black ribbed rubber bar matting, ' + ROOM),
    'mat_cork':      (64, 64, 5143, TILE + 'cork matting, warm speckled brown, ' + ROOM),
    'mat_towel':     (64, 64, 5145, TILE + 'a folded cotton bar towel, teal with two woven '
                      'stripes, ' + ROOM),

    # ── posters for the right wall ────────────────────────────────────────────
    'post5_neon':    (72, 96, 5151, POSTER + 'a poster of a neon lit palm avenue at night, '
                      'magenta and cyan, ' + ROOM),
    'post5_car':     (72, 96, 5153, POSTER + 'a poster of a low convertible car parked by '
                      'the sea at sunset, ' + ROOM),
    'post5_diver':   (72, 96, 5155, POSTER + 'a poster of a diver above a turquoise pool, '
                      'seen from below, ' + ROOM),

    # ── the neon sign on the right wall ───────────────────────────────────────
    'neon_flamingo': (32, 36, 5161, PROP + 'a small neon sign in the shape of a flamingo, '
                      'glowing pink tubing, ' + ROOM),
    'neon_palm':     (32, 36, 5163, PROP + 'a small neon sign in the shape of a palm tree, '
                      'glowing teal and magenta tubing, ' + ROOM),
    'neon_wave':     (32, 36, 5165, PROP + 'a small neon sign in the shape of a curling '
                      'wave, glowing cyan tubing, ' + ROOM),

    # ── the television ────────────────────────────────────────────────────────
    'tv_crt':        (49, 49, 5171, PROP + 'a boxy 1980s television set with a rounded '
                      'screen and two dials, dark plastic case, ' + ROOM),
    'tv_wood':       (49, 49, 5173, PROP + 'a 1980s television in a wood veneer case with '
                      'a mesh speaker panel beside the screen, ' + ROOM),
    'tv_portable':   (49, 49, 5175, PROP + 'a small portable television with a carrying '
                      'handle and a telescopic antenna, ' + ROOM),

    # ── tables (with their stools, matte, no reflection) ──────────────────────
    'table5_cane':   (132, 78, 5181, PROP + 'a round cafe table with a cane and brass base '
                      'and two matching stools, one either side, ' + ROOM),
    'table5_tile':   (132, 78, 5183, PROP + 'a round bar table with a teal tiled top on a '
                      'chunky plum pedestal and two low stools, ' + ROOM),
    'table5_bistro': (132, 78, 5185, PROP + 'a small square bistro table in dark wood with '
                      'a coral rim and two bentwood chairs, ' + ROOM),

    # ── plants ────────────────────────────────────────────────────────────────
    'plant5_banana': (56, 92, 5191, PROP + 'a banana plant with broad split leaves in a '
                      'ribbed cream pot, ' + ROOM),
    'plant5_yucca':  (56, 92, 5193, PROP + 'a yucca with stiff sword leaves on a thick '
                      'trunk in a terracotta pot, ' + ROOM),
    'plant5_fern':   (56, 92, 5195, PROP + 'a hanging fern in a macrame holder over a low '
                      'wooden stand, ' + ROOM),

    # ── beer taps ─────────────────────────────────────────────────────────────
    'tap5_swan':     (56, 56, 5201, PROP + 'a brass beer font with two swan neck taps on a '
                      'round base, ' + ROOM),
    'tap5_deco':     (56, 56, 5203, PROP + 'an art deco beer font, a stepped chrome column '
                      'with three black handles, ' + ROOM),
    'tap5_dolphin':  (56, 56, 5205, PROP + 'a beer font shaped like a rising dolphin in '
                      'polished brass with two taps, ' + ROOM),

    # ── three small framed pictures per trio ──────────────────────────────────
    'picT_cocktail': (44, 44, 5211, FRAMED + 'a cocktail glass with a cherry, gold frame, ' + ROOM),
    'picT_palm':     (44, 44, 5213, FRAMED + 'a single palm tree at sunset, teal frame, ' + ROOM),
    'picT_sun':      (44, 44, 5216, PROP + 'a big round setting sun low over the sea, wide '
                      'bands of coral, magenta and deep blue, filling the frame, ' + ROOM),
    'picT_vinyl':    (44, 44, 5217, FRAMED + 'a vinyl record on a turntable, black frame, ' + ROOM),
    'picT_parrot':   (44, 44, 5219, FRAMED + 'a parrot on a branch, brass frame, ' + ROOM),
    'picT_wave':     (44, 44, 5221, FRAMED + 'a breaking wave, pale wood frame, ' + ROOM),
}

CUT = ('neon_', 'tv_', 'table5_', 'plant5_', 'tap5_', 'picT_')     # cut out, not a texture
TRIOS = {'trioA': ('picT_cocktail', 'picT_palm', 'picT_sun'),
         'trioB': ('picT_vinyl', 'picT_parrot', 'picT_wave')}


def _state():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def _save(s):
    os.makedirs(OUT, exist_ok=True)
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(s, indent=1))


def take(only=None):
    os.makedirs(OUT, exist_ok=True)
    s = _state()
    waiting = 0
    for key, (w, h, seed, desc) in JOBS.items():
        if only and not any(key.startswith(o) for o in only):
            continue
        png = os.path.join(OUT, key + '.png')
        if os.path.exists(png):
            continue
        e = s.get(key) or {}
        if e.get('job'):
            text, images = r4._call('get_image', {'job_id': e['job']}, timeout=180)
            if images:
                io.open(png, 'wb').write(images[0])
                print('  ->', key)
            else:
                waiting += 1
            continue
        text, images = r4._call('create_image_pro', {
            'description': desc, 'width': w, 'height': h,
            'no_background': key.startswith(CUT), 'seed': seed})
        if images:
            io.open(png, 'wb').write(images[0])
            print('  ->', key)
            continue
        m = re.search(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}', text, re.I)
        s.setdefault(key, {})['job'] = m.group(0) if m else None
        _save(s)
        waiting += 1
        print('  queued %s %s' % (key, m.group(0) if m else text.strip()[:80]))
    have = sum(1 for k in JOBS if os.path.exists(os.path.join(OUT, k + '.png')))
    print('%d of %d drawn, %d still cooking' % (have, len(JOBS), waiting))


# ── the bar top is a horizontal plane too ─────────────────────────────────────

def bar_rows():
    """Rows of the counter's TOP SLAB, off the room's own counter art: the dark slab above
    the magenta rail, which is the only part of the counter this round touches — the cellar
    under it holds the bottles and is nobody's texture."""
    im = Image.open(os.path.join(ROOT, 'Assets', 'Art', 'Backgrounds', 'counter.png')).convert('RGBA')
    px = im.load()

    def slab(c):
        r, g, b, a = c
        return a > 200 and r < 70 and g < 60 and b < 80          # the near-black top
    rows = {}
    for y in range(im.height):
        run = [x for x in range(im.width) if slab(px[x, y])]
        if len(run) > 40:
            rows[y] = (min(run), max(run))
    # only the slab, not the dark shelves below it: keep the top run of rows
    ys = sorted(rows)
    keep = {}
    for y in ys:
        if keep and y - max(keep) > 2:
            break
        keep[y] = rows[y]
    # IN THE ROOM'S OWN ROWS, not the drawing's: the projection is the room's ground plane
    # (its horizon is room row 148), and the slab is a plane at bar height in that same room.
    return (640, 360), {y + COUNTER_TOP_Y: span for y, span in keep.items()}


def stamp(tex, mask_path, tiles=3.0):
    """A generated texture inside a piece the room already shaped: the silhouette and the
    perspective are the room's, only the surface is new."""
    mask = Image.open(mask_path).convert('RGBA')
    w, h = mask.size
    field = Image.new('RGBA', (w, h))
    tw = max(8, int(w / tiles))
    th = max(8, int(tex.height * tw / max(tex.width, 1)))
    tile = tex.convert('RGBA').resize((tw, th), Image.NEAREST)
    for y in range(0, h, th):
        for x in range(0, w, tw):
            field.alpha_composite(tile, (x, y))
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    mp, fp = mask.load(), field.load()
    op = out.load()
    for y in range(h):
        for x in range(w):
            a = mp[x, y][3]
            if a:
                r, g, b, _ = fp[x, y]
                op[x, y] = (r, g, b, a)
    return out


# THE FRAME IS DRAWN, NOT GENERATED (the house rule: chrome is procedural — and the cut-out
# took the generator's frames off with the background anyway, leaving three objects floating on
# the wall). A moulding of the room's own colours: an outer rail, a bevel, and a mat around the
# picture, the way the small pictures of round three are framed.
FRAMES = {
    'gold':  ((0x7a, 0x54, 0x1c), (0xf7, 0xb1, 0x1b), (0x3a, 0x28, 0x12), (0xf0, 0xe4, 0xcf)),
    'wood':  ((0x3a, 0x24, 0x1c), (0x8a, 0x5a, 0x3a), (0x25, 0x16, 0x12), (0xe8, 0xd8, 0xc4)),
    'teal':  ((0x12, 0x3c, 0x3a), (0x3f, 0xb6, 0xa8), (0x0c, 0x24, 0x24), (0xdf, 0xef, 0xe8)),
    'coral': ((0x6d, 0x22, 0x2e), (0xf0, 0x47, 0x5d), (0x3a, 0x12, 0x18), (0xf6, 0xdf, 0xd8)),
}


def framed(pic, style='gold'):
    """A picture in a moulding: 1px shadow line, 2px face, 1px inner line, 2px mat."""
    dark, face, line, mat = FRAMES[style]
    pic = pic.crop(pic.getbbox())
    pad = 6
    w, h = pic.width + pad * 2, pic.height + pad * 2
    im = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rectangle([0, 0, w - 1, h - 1], fill=dark + (255,))
    d.rectangle([1, 1, w - 2, h - 2], fill=face + (255,))
    d.rectangle([3, 3, w - 4, h - 4], fill=line + (255,))
    d.rectangle([4, 4, w - 5, h - 5], fill=mat + (255,))
    im.alpha_composite(pic, (pad, pad))
    return im


def build():
    os.makedirs(OUT, exist_ok=True)

    def have(key):
        p = os.path.join(OUT, key + '.png')
        return Image.open(p).convert('RGBA') if os.path.exists(p) else None

    # ceilings, into the cream trapezoid
    size, crows = r3.ceiling_rows()
    for key in [k for k in JOBS if k.startswith('ceil_')]:
        tex = have(key)
        if tex is None:
            continue
        r3.warp(tex, crows, size, near_at_bottom=False).save(os.path.join(OUT, 'fx_' + key + '.png'))
        print('  ceiling', key)

    # floors, as a ground plane
    fsize, frows = r4.floor_rows()
    for key in [k for k in JOBS if k.startswith('floor_')]:
        tex = have(key)
        if tex is None:
            continue
        r4.project_floor(tex, frows, fsize).save(os.path.join(OUT, 'fx_' + key + '.png'))
        print('  floor', key)

    # the bar top, the same projection at bar height
    bsize, brows = bar_rows()
    for key in [k for k in JOBS if k.startswith('bar_')]:
        tex = have(key)
        if tex is None:
            continue
        r4.project_floor(tex, brows, bsize, tiles_across=2.0).save(
            os.path.join(OUT, 'fx_' + key + '.png'))
        print('  bar top', key)

    # The rug and the two mats: the room already fixes what shape they are and how they lie,
    # so only the surface is new. One mat texture makes both mats — they are the same rubber
    # in two lengths — and each piece keeps its own silhouette.
    for prefix, masks, tiles in (('rug_', ('fx_floor_rug',), 4.0),
                                 ('mat_', ('fx_beer_mat', 'fx_prep_mat'), 1.0)):
        for k in [k for k in JOBS if k.startswith(prefix)]:
            tex = have(k)
            if tex is None:
                continue
            for mask in masks:
                piece = stamp(tex, os.path.join(FIXT, mask + '.png'), tiles)
                tail = '' if len(masks) == 1 else ('_drip' if 'beer' in mask else '_prep')
                piece.save(os.path.join(OUT, 'piece_' + k + tail + '.png'))
            print('  stamped', k)

    # posters, hung on the right wall
    for key in [k for k in JOBS if k.startswith('post5_')]:
        pic = have(key)
        if pic is None:
            continue
        r4.hang_right(pic).save(os.path.join(OUT, 'fx_' + key + '.png'))
        print('  poster', key)

    # the trios: three framed pictures hung as one piece
    for name, parts in TRIOS.items():
        pics = [have(p) for p in parts]
        if any(p is None for p in pics):
            continue
        styles = ('gold', 'teal', 'wood') if name == 'trioA' else ('wood', 'coral', 'gold')
        pics = [framed(p, st) for p, st in zip(pics, styles)]
        gap = 10
        w = sum(p.width for p in pics) + gap * (len(pics) - 1)
        h = max(p.height for p in pics)
        sheet = Image.new('RGBA', (w, h), (0, 0, 0, 0))
        x = 0
        for p in pics:
            sheet.alpha_composite(p, (x, (h - p.height) // 2))
            x += p.width + gap
        sheet.save(os.path.join(OUT, 'picS_' + name + '.png'))
        print('  trio', name, '%dx%d' % (w, h))
    print('build done ->', OUT)


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'take'
    if cmd == 'take':
        take(sys.argv[2:] or None)
    elif cmd == 'build':
        build()
    else:
        raise SystemExit(__doc__)
