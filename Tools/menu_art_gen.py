# -*- coding: utf-8 -*-
"""THE MENUS' GENERATED PIECES (2026-09-25, the author: "Settings, esc menülerinin UI tasarımı tekrardan tasarlansın.
Gerekli arkaplan görsellerini icon görsellerini veya efektleri pixellabden üretebilirsin" - and, to the estimate of
about fifty generations, "devam").

  door    the ESC menu's picture: the bar's front door at night, beside the keys (create_image_pro, 100x168 -> four
          candidates for the one call's 20 generations; shown at exactly 2x, 200x336)
  header  the settings window's marquee: a blank neon sign board the title is written over (create_image_pro, 168x44
          -> four candidates; shown at 2x, 336x88)
  icons   the keys' icons in the top bar's star3d / cog3d family (create_image_pixflux, 32x32, 1 generation each, on
          the forced palette; SETTINGS keeps Items/cog3d)

Every generated picture here is FLAT and MATTE (the author, 2026-08-18: no lighting and no reflections in generated
images): the neon is a flat colour, the rain a line, nothing glows.

  py -3 -X utf8 Tools/menu_art_gen.py take [door header icons]
  py -3 -X utf8 Tools/menu_art_gen.py fetch
  py -3 -X utf8 Tools/menu_art_gen.py snap
  py -3 -X utf8 Tools/menu_art_gen.py sheet
  py -3 -X utf8 Tools/menu_art_gen.py report <the probe's capture folder>   -> Docs/reports/menu_art
  py -3 -X utf8 Tools/menu_art_gen.py frame [menu_esc_bg]   draw the top/bottom neon band on a shipped picture

Nothing enters Assets from here. The picks are shipped by hand into Assets/Resources/Menu after the author has chosen
on the report page (Docs/reports/menu_art).
"""
import base64
import io
import json
import os
import re
import sys

from PIL import Image

import pixellab

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, os.path.join(HERE, 'v4_bottles'))
import palette  # noqa: E402

STATE = os.path.join(HERE, 'menu_art_state.json')
STAGING = os.path.join(HERE, 'AssetPipeline', 'staging', 'menu_art')
RAW = os.path.join(STAGING, 'raw')
SNAPPED = os.path.join(STAGING, 'snapped')
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
MENU = os.path.join(ROOT, 'Assets', 'Resources', 'Menu')
SCENE = os.path.join(ROOT, 'Assets', 'Resources', 'Scene')
MAX_DESC = 2000

# The pictures' palette: the night the menus already stand in, the club blue, the neon pink and teal, a little amber
# and cream for the glass and the bulbs, and the wall's brick.
PICTURE_RAMPS = [('Night', range(5)), ('ClubBlue', range(5)), ('Magenta', range(1, 5)), ('Cyan', range(1, 5)),
                 ('Amber', range(2, 5)), ('Cream', range(2, 5)), ('Brick', range(1, 4))]
PICTURE_COLOURS = [palette.ramp(n, i) for n, idx in PICTURE_RAMPS for i in idx]

FLAT = ('Flat colour areas only, matte, hard pixel edges, 1px dark outlines, no anti-aliasing, no dithering, no '
        'gradients, no lighting effects, no glow, no halo, no bloom, no reflections, no cast shadows.')

DOOR = ('A single upright pixel-art picture, taller than wide: the front door of a small 1980s Miami cocktail bar late '
        'at night, seen straight on from the street. A narrow dark wooden door; its tall glass panel in the upper half '
        'is one flat warm amber colour area, with thin rain streaks drawn over it as short pale blue diagonal lines. '
        'Above the door a blank dark sign board edged by one thin hot-pink neon tube, with a small teal neon '
        'cocktail-glass outline beside it; the sign carries no letters. A short awning striped pink and cream over the '
        'door. Deep plum brick wall on both sides, a strip of night-blue sky at the top with a few thin rain lines, a '
        'dark pavement at the foot. The door fills the middle third of the picture. ' + FLAT + ' No text, letters, '
        'numbers, logo, people, border or frame; the scene fills the whole canvas edge to edge. Use only the colours '
        'of the palette reference.')

HEADER = ('A small wide pixel-art sign, about four times wider than tall: the blank rectangular marquee board of a '
          '1980s Miami cocktail bar, mounted on a plum brick wall at night. The board face is one calm flat dark '
          'night-blue area filling the middle, left empty for a title to be written over it later. One thin hot-pink '
          'neon tube runs around the board edge; a small teal neon palm tree outline at the left end and a small '
          'teal neon cocktail glass outline at the right end. A row of small round cream light bulbs along the top '
          'edge of the board and another along the bottom edge. ' + FLAT + ' No text or letters anywhere; the sign '
          'fills the whole canvas edge to edge. Use only the colours of the palette reference.')

ESC_BG = ('A pixel-art background panel, taller than wide, for the pause menu of a 1980s Miami cocktail bar at night. '
          'An Art Deco wall panel in deep plum and night blue, framed along all four edges by one thin hot-pink neon tube '
          'with rounded corners. At the top centre a small teal Art Deco sunburst fan, behind where a title will be '
          'written. In each bottom corner a few dark plum palm leaves. The whole middle is one calm, even, dark plum-blue '
          'surface with faint vertical Art Deco reeding lines, left empty because a column of menu buttons is laid over '
          'it; a calm dark band across the bottom for a line of small text. ' + FLAT + ' No text, letters, numbers, '
          'logo, people or buttons; the panel fills the whole canvas edge to edge. Use only the colours of the palette '
          'reference.')

SETTINGS_BG = ('A pixel-art background panel, wider than tall, for the settings menu of a 1980s Miami cocktail '
               'bar at night, a wide sibling of the pause panel. An Art Deco wall panel in deep plum and night blue, '
               'framed along all four edges by one thin hot-pink neon tube with rounded corners. High in the middle a '
               'small teal Art Deco sunburst fan, behind where a sign will hang. In each bottom corner a few dark plum '
               'palm leaves. The whole middle is one very calm, even, dark plum-blue surface with faint vertical Art '
               'Deco reeding lines, left empty because a full page of settings text is laid over it. ' + FLAT + ' No '
               'text, letters, numbers, logo, people or buttons; the panel fills the whole canvas edge to edge. Use '
               'only the colours of the palette reference.')

ICON_TAIL = ('single chunky game menu icon, pixel art, a polished brass and gold metal object seen straight on, '
             'bevelled, light from the upper left and darker on the lower right, one pixel dark outline, centred, '
             'filling most of the canvas, transparent background, no text, no letters, no glow.')

# key -> (subject, the pack glyph it starts from or None). SETTINGS keeps Items/cog3d (no generation). Started from
# the pack's glyph (init 120) the first four came back weak - a hollow triangle, a tray for a disk, a door that was
# two blocks - while the text-only ones were clean; those four were taken again from their words alone (_v1 kept).
ICONS = {
    'mi_resume':   ('a solid play triangle pointing right, thick and filled', None),
    'mi_save':     ('a square 3.5 inch floppy disk with its metal shutter and label', None),
    'mi_continue': ('a small padlock', 'lock'),
    'mi_new_run':  ('a single thick arrow bent round almost a full circle with its arrowhead at the end, a refresh symbol', None),
    'mi_quit':     ('a wooden door standing half open, seen straight on', None),
    'mi_audio':    ('a loudspeaker with three curved sound waves beside it', 'sound_on'),
    'mi_controls': ('a single square keyboard key cap seen slightly from above', None),
    'mi_display':  ('a small old CRT computer monitor with a blank screen', None),
    'mi_language': ('a globe with meridian and parallel lines', None),
    # the top bar's settings key and the ESC menu's SETTINGS (2026-09-26, the author: "Ana sahnede ust bardaki
    # ayarlar butonunun iconunu da ayni icon sanat diliyle uret"): the family's own cog, in place of cog3d
    'mi_settings': ('a gear cog with eight square teeth and a round hole in the middle', None),
}
GLYPHS = ['exit', 'tent', 'play', 'home', 'stats', 'back', 'pause', 'menu', 'music', 'gamepad',
          'expand', 'sound_on', 'sound_off', 'cog', 'save', 'restart', 'close', 'trophy', 'mail', 'heart_line',
          'check', 'trash', 'chevron_down', 'chevron_up', 'heart', 'plus', 'minus', 'lock', 'unlock', 'info']


def call(tool, args, timeout=900):
    _, body = pixellab.post({'jsonrpc': '2.0', 'id': 1, 'method': 'tools/call',
                             'params': {'name': tool, 'arguments': args}}, timeout=timeout)
    text, images = '', []
    for m in pixellab.sse(body):
        for c in ((m.get('result') or {}).get('content') or []):
            if c.get('type') == 'text':
                text += c['text'] + '\n'
            elif c.get('type') == 'image':
                images.append(Image.open(io.BytesIO(base64.b64decode(c['data']))).convert('RGBA'))
    return text, images


def load():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def save(s):
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(s, indent=1))


def png_b64(im):
    buf = io.BytesIO()
    im.save(buf, 'PNG')
    return base64.b64encode(buf.getvalue()).decode('ascii')


def plate(colours, cell=8):
    """The palette as a strip of flat cells: what a reference reads its colours from."""
    im = Image.new('RGB', (len(colours) * cell, cell))
    for i, c in enumerate(colours):
        im.paste(c, (i * cell, 0, (i + 1) * cell, cell))
    return im


def glyph(name, size=32, fill=None):
    """The pack's own 16x16 mask for `name`, at 2x on a transparent canvas, filled flat."""
    sheet = Image.open(os.path.join(MENU, 'pack_glyphs.png')).convert('RGBA')
    i = GLYPHS.index(name)
    r, c = divmod(i, 5)
    cell = sheet.crop((c * 16, r * 16, c * 16 + 16, r * 16 + 16)).resize((32, 32), Image.NEAREST)
    fill = fill or palette.ramp('Malt', 3)
    out = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    px, op = cell.load(), out.load()
    for y in range(32):
        for x in range(32):
            if px[x, y][3] >= 128:
                op[x, y] = fill + (255,)
    return out


def _keep(key, images, meta):
    os.makedirs(RAW, exist_ok=True)
    s = load()
    e = s.setdefault(key, {})
    e['pngs'] = []
    for n, im in enumerate(images):
        p = os.path.join(RAW, '%s_%d.png' % (key, n))
        im.save(p)
        e['pngs'].append(os.path.relpath(p, HERE).replace('\\', '/'))
    e.update(meta)
    save(s)
    print('  %-12s landed %d image(s) %dx%d' % (key, len(images), images[0].width, images[0].height))


def _queue(key, tool, args):
    assert args['width'] % 4 == 0 and args['height'] % 4 == 0, (key, 'an edge not divisible by 4 fails at GET')
    assert len(args['description']) <= MAX_DESC, (key, len(args['description']))
    s = load()
    e = s.get(key) or {}
    if e.get('pngs'):
        print('  %-12s already landed' % key)
        return
    if e.get('job_id'):
        print('  %-12s already queued (%s) - fetch it' % (key, e['job_id']))
        return
    text, images = call(tool, args)
    if images:
        _keep(key, images, {'tool': tool})
        return
    m = re.search(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}', text, re.I)
    s = load()
    s[key] = {'job_id': m.group(0) if m else None, 'tool': tool}
    save(s)
    print('  %-12s queued %s' % (key, m.group(0) if m else text.strip()[:300]))


def take(families):
    pal = png_b64(plate(PICTURE_COLOURS))
    city = Image.open(os.path.join(SCENE, 'curtain_city.png')).convert('RGBA')
    refs = [{'base64': pal, 'usage': 'colour palette: use ONLY these colours'},
            {'base64': png_b64(city), 'usage': 'drawing style only: pixel size, 1px dark outlines, flat shading; '
                                               'do not copy the subject'}]
    if 'door' in families:
        _queue('door', 'create_image_pro', {'description': DOOR, 'width': 100, 'height': 168,
                                            'no_background': False, 'seed': 5301, 'reference_images': refs})
    if 'esc_bg' in families:
        # 2026-09-26, the author: "ESC icin arkaplanda kullanilan mavi UI yerine arkaplan gorseli uret" - the ESC
        # plate itself as a picture, at exactly 2x (440x504), one candidate (the one-alternative rule)
        _queue('esc_bg', 'create_image_pro', {'description': ESC_BG, 'width': 220, 'height': 252,
                                              'no_background': False, 'seed': 5303, 'reference_images': refs})
    if 'settings_bg' in families:
        # 2026-09-26, the author: "ESC menusu ile tasarim olarak arkaplan olarak ayarlar menusu benzer olmali" - the
        # settings plate as the pause panel's wide sibling, at exactly 2x under a RectMask2D (800x608 over the
        # 800x590..604 plate), one candidate (the one-alternative rule)
        _queue('settings_bg', 'create_image_pro', {'description': SETTINGS_BG, 'width': 400, 'height': 304,
                                                   'no_background': False, 'seed': 5304, 'reference_images': refs})
    if 'header' in families:
        _queue('header', 'create_image_pro', {'description': HEADER, 'width': 168, 'height': 44,
                                              'no_background': False, 'seed': 5302, 'reference_images': refs})
    if 'icons' in families:
        icon_pal = png_b64(plate([palette.ramp('Night', 0)] + [palette.ramp('Malt', i) for i in range(5)]
                                 + [palette.ramp('Amber', 3), palette.ramp('Amber', 4), palette.ramp('Cream', 4)]))
        for key, (subject, start) in ICONS.items():
            args = {'description': subject + ', ' + ICON_TAIL, 'width': 32, 'height': 32, 'no_background': True,
                    'seed': 3131, 'outline': 'single color black outline', 'shading': 'medium shading',
                    'detail': 'medium detail', 'text_guidance_scale': 8.0, 'color_image_base64': icon_pal}
            if start:
                args['init_image_base64'] = png_b64(glyph(start))
                args['init_image_strength'] = 120
            _queue(key, 'create_image_pixflux', args)


def fetch():
    s = load()
    for key, e in list(s.items()):
        if e.get('pngs') or not e.get('job_id'):
            continue
        text, images = call('get_image', {'job_id': e['job_id']})
        if images:
            _keep(key, images, {'tool': e.get('tool')})
        else:
            print('  %-12s cooking: %s' % (key, (text.strip().splitlines() or [''])[0][:160]))


def snap_to(im, colours):
    """Every opaque pixel to the nearest of `colours` (weighted for the eye), alpha made binary."""
    im = im.convert('RGBA')
    px = im.load()
    out = Image.new('RGBA', im.size, (0, 0, 0, 0))
    op = out.load()
    cache = {}
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a < 128:
                continue
            k = (r, g, b)
            c = cache.get(k)
            if c is None:
                c = min(colours, key=lambda q: 3 * (r - q[0]) ** 2 + 4 * (g - q[1]) ** 2 + 2 * (b - q[2]) ** 2)
                cache[k] = c
            op[x, y] = c + (255,)
    return out


def luma(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def star_ladder():
    """star3d's own colours by brightness: the ladder every icon in the family is painted on."""
    im = Image.open(os.path.join(ITEMS, 'star3d.png')).convert('RGBA')
    cs = sorted({px[:3] for px in im.getdata() if px[3] >= 128}, key=luma)
    return cs


def ladder_snap(im, ladder):
    """The shaker_gen.steel method on the star's ladder: the darkest 6% held at the keyline, the rest spread over the
    ladder by each pixel's place in the drawing's own luma range, so a highlight stays a highlight."""
    im = im.convert('RGBA')
    px = im.load()
    vals = sorted(luma(px[x, y]) for y in range(im.height) for x in range(im.width) if px[x, y][3] >= 128)
    out = Image.new('RGBA', im.size, (0, 0, 0, 0))
    if not vals:
        return out
    lo, hi = vals[int(len(vals) * 0.02)], vals[int(len(vals) * 0.985)]
    span = max(1.0, hi - lo)
    op = out.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a < 128:
                continue
            t = (luma((r, g, b)) - lo) / span
            i = 0 if t < 0.06 else 1 + int(min(0.999, max(0.0, (t - 0.06) / 0.94)) * (len(ladder) - 2))
            op[x, y] = ladder[min(i, len(ladder) - 1)] + (255,)
    return out


def snap():
    s = load()
    os.makedirs(SNAPPED, exist_ok=True)
    ladder = star_ladder()
    for key, e in sorted(s.items()):
        for p in e.get('pngs') or []:
            im = Image.open(os.path.join(HERE, p)).convert('RGBA')
            # the pictures go to the whole 55 (palette.quantize, the house chain): the first cut snapped them to the
            # prompt's own subset, which has no Magenta[0], and the door's plum brick came out brick red
            # an icon made on the forced palette is already on the 55 and keeps its own shading (the ladder snap
            # flattened the good ones); one that is not goes onto star3d's ladder
            if key.startswith('mi_'):
                out = im.copy() if palette.off_palette(im) == 0 else ladder_snap(im, ladder)
            else:
                out = palette.quantize(im)
            q = os.path.join(SNAPPED, os.path.basename(p))
            out.save(q)
            n_raw = len({c for c in im.getdata() if c[3] >= 128})
            n_out = len({c for c in out.getdata() if c[3] >= 128})
            print('  %-18s %dx%d  colours %d -> %d  off-palette %d' % (os.path.basename(p), out.width, out.height,
                                                                       n_raw, n_out, palette.off_palette(out)))


def sheet():
    s = load()
    tiles = []
    for key, e in sorted(s.items()):
        for p in e.get('pngs') or []:
            q = os.path.join(SNAPPED, os.path.basename(p))
            for src in (os.path.join(HERE, p), q):
                if os.path.exists(src):
                    tiles.append(Image.open(src).convert('RGBA'))
    if not tiles:
        print('nothing landed yet')
        return
    k = 3
    w = sum(t.width * k + 8 for t in tiles) + 8
    h = max(t.height * k for t in tiles) + 16
    out = Image.new('RGBA', (w, h), palette.ramp('Night', 1) + (255,))
    x = 8
    for t in tiles:
        out.alpha_composite(t.resize((t.width * k, t.height * k), Image.NEAREST), (x, 8))
        x += t.width * k + 8
    p = os.path.join(STAGING, 'sheet.png')
    out.save(p)
    print('sheet ->', p)


REPORT = os.path.join(ROOT, 'Docs', 'reports', 'menu_art')

PAGE_CSS = """:root { --bg:#0D0813; --panel:#1A1023; --ink:#F2E8D5; --ink2:#C9BCA8; --accent:#E8A33D; --rule:#2A1E38; }
body { margin:0; padding:24px 16px 64px; background:var(--bg); color:var(--ink); font:16px/1.5 Georgia,"Times New Roman",serif; }
main { max-width:1100px; margin:0 auto; }
h1 { font-size:28px; margin:0 0 4px; font-weight:normal; text-wrap:balance; }
h2 { font-size:20px; margin:36px 0 8px; font-weight:normal; color:var(--accent); text-wrap:balance; }
p, li { max-width:72ch; color:var(--ink2); }
figure { margin:12px 0; background:var(--panel); padding:10px; }
figure img { display:block; width:100%; height:auto; image-rendering:pixelated; }
figcaption { font-size:13px; color:var(--ink2); margin-top:6px; }
.row { display:grid; grid-template-columns:repeat(auto-fit,minmax(300px,1fr)); gap:14px; }
.ask { border-left:3px solid var(--accent); padding:8px 12px; margin:24px 0; }
code { color:var(--accent); font-family:Consolas,monospace; font-size:14px; }
table { border-collapse:collapse; margin:12px 0; color:var(--ink2); font-size:15px; font-variant-numeric:tabular-nums; }
th, td { border-bottom:1px solid var(--rule); padding:6px 14px 6px 0; text-align:left; vertical-align:top; }
th { color:var(--ink); font-weight:normal; }
"""


def _sheet(tiles, k, gap=12, bg=(26, 16, 35, 255)):
    w = sum(t.width * k + gap for t in tiles) + gap
    h = max(t.height * k for t in tiles) + gap * 2
    out = Image.new('RGBA', (w, h), bg)
    x = gap
    for t in tiles:
        out.alpha_composite(t.resize((t.width * k, t.height * k), Image.NEAREST), (x, gap))
        x += t.width * k + gap
    return out


def report(captures):
    """Docs/reports/menu_art: the candidates as drawn and in the game (captures = the probe's folder)."""
    os.makedirs(REPORT, exist_ok=True)
    art = lambda sub, n: Image.open(os.path.join(STAGING, sub, n)).convert('RGBA')   # noqa: E731
    # the art sheets
    for key, k in (('door', 3), ('header', 3)):
        tiles = [art('snapped', '%s_%d.png' % (key, i)) for i in range(4)]
        _sheet(tiles, k).save(os.path.join(REPORT, 'art_%s.png' % key))
    names = ['mi_resume', 'mi_save', 'mi_continue', 'mi_new_run', 'mi_quit', 'mi_audio', 'mi_controls', 'mi_display',
             'mi_language']
    icons = [art('snapped', n + '_0.png') for n in names] + [Image.open(os.path.join(ITEMS, 'cog3d.png')).convert('RGBA')]
    _sheet(icons, 4).save(os.path.join(REPORT, 'art_icons.png'))
    firsts = [art('snapped', n) for n in ('mi_resume_v1_0.png', 'mi_save_v1_0.png', 'mi_quit_v1_0.png',
                                          'mi_new_run_v1_0.png', 'mi_new_run_v2_0.png')]
    _sheet(firsts, 4).save(os.path.join(REPORT, 'art_icons_dropped.png'))
    flags = [Image.open(os.path.join(captures, '..', 'flag_en', 'fl_en_%s.png' % v)).convert('RGBA') for v in 'CAB']
    flags += [Image.open(os.path.join(ITEMS, 'fl_%s.png' % v)).convert('RGBA') for v in ('gb', 'us')]
    _sheet(flags, 4).save(os.path.join(REPORT, 'art_flags.png'))
    # the captures, as taken
    for f in sorted(os.listdir(captures)):
        if f.endswith('.png'):
            Image.open(os.path.join(captures, f)).convert('RGB').save(os.path.join(REPORT, f))
    # the surfaces, close up
    crops = []
    for kind in ('terrazzo', 'ribs', 'pinstripe'):
        im = Image.open(os.path.join(captures, 'esc_ship_%s.png' % kind)).convert('RGB').crop((440, 196, 840, 330))
        crops.append(im.resize((im.width * 2, im.height * 2), Image.NEAREST))
    out = Image.new('RGB', (crops[0].width, sum(c.height + 8 for c in crops)), (13, 8, 19))
    y = 0
    for c in crops:
        out.paste(c, (0, y))
        y += c.height + 8
    out.save(os.path.join(REPORT, 'surfaces_x2.png'))
    html = io.open(os.path.join(HERE, 'menu_art_report.tmpl'), encoding='utf-8').read().replace('/*CSS*/', PAGE_CSS)
    io.open(os.path.join(REPORT, 'index.html'), 'w', encoding='utf-8').write(html)
    print('report ->', os.path.join(REPORT, 'index.html'))


MAGENTA, CREAM = (232, 77, 166, 255), (242, 232, 213, 255)


def frame(name='menu_esc_bg'):
    """Draw the 3px neon band along the top and bottom edges of a shipped menu picture, between its full-height
    side tubes: magenta / cream core / magenta, with the core bridged through the corner so the tube reads as one
    frame. Idempotent: a picture whose top band is already lit is left untouched (the sprite-pipeline rule)."""
    import hashlib
    p = os.path.join(MENU, name + '.png')
    im = Image.open(p).convert('RGBA')
    w, h = im.size
    before = hashlib.sha1(im.tobytes()).hexdigest()[:12]
    if im.getpixel((w // 2, 1))[:3] == CREAM[:3]:
        print('  %s already framed (%s)' % (name, before))
        return
    px = im.load()
    # a picture born without side tubes (the settings panel) gets the family's: cream core
    # on the left like the pause panel's, pink on the right
    PINK = (255, 125, 198, 255)
    if im.getpixel((1, h // 2))[:3] not in (CREAM[:3], PINK[:3]):
        for y in range(h):
            for col, colour in ((0, MAGENTA), (1, CREAM), (2, MAGENTA)):
                px[col, y] = colour
            for col, colour in ((w - 3, MAGENTA), (w - 2, PINK), (w - 1, MAGENTA)):
                px[col, y] = colour
    for x in range(3, w - 3):
        for row, colour in ((0, MAGENTA), (1, CREAM), (2, MAGENTA)):
            px[x, row] = colour
            px[x, h - 1 - row] = colour
    # the lit core turns the corner: one bridge pixel between the horizontal core and each side tube's own core
    for bx in (2, w - 3):
        px[bx, 1] = CREAM
        px[bx, h - 2] = CREAM
    im.save(p)
    after = hashlib.sha1(Image.open(p).convert('RGBA').tobytes()).hexdigest()[:12]
    print('  %s framed  %s -> %s  (%dx%d, rows 0-2 and %d-%d)' % (name, before, after, w, h, h - 3, h - 1))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else ''
    rest = sys.argv[2:] or ['door', 'header', 'icons']
    if cmd == 'take':
        take(rest)
    elif cmd == 'fetch':
        fetch()
    elif cmd == 'snap':
        snap()
    elif cmd == 'sheet':
        sheet()
    elif cmd == 'frame':
        frame(*sys.argv[2:3])
    elif cmd == 'report':
        report(sys.argv[2])
    else:
        print(__doc__)
