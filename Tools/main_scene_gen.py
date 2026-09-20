# -*- coding: utf-8 -*-
"""THE MAIN SCENE AS ONE PIECE (2026-09-20, the author: "Ana sahne daha birleşik bir sanat gibi görünmesi için
farklı konsept ana sahneler istiyorum tezgahla beraber oyun sahnesi tasarla tek parça oyunun pixel çözünürlüğünde
sadece ana sahne. Oyunda kullanılan nesneleri kullanma, nesneleri kullanma direkt").

The room is a COLLAGE today and that is the complaint: the back wall, the floor, the ceiling, the side wall, the
window and every stick of furniture were generated as separate plates in six rounds and assembled in Unity, so no
two of them were drawn by the same hand on the same day. These are whole-room candidates instead - one painting
each, counter included, nothing borrowed from the pieces the game already owns.

Three rules this tool exists to keep:

  NATIVE SIZE. 640x360 is DiegeticStage.Reference, and create_image_pro takes 688x384 at 16:9 - so the picture is
  drawn at the size it is shown and never resampled. The 2026-08-18 batch was painted large and area-averaged down
  to 640, which folds four painted pixels into one; the author measured the result ("görüntü ve sanat kalitesi
  düşük ve bulanık görünüyor") and 40,859 colours, 100% off-palette, in a 55-colour game.

  NO LIGHT, NO REFLECTION. The room is lit by URP 2D (a Light2D per fixture, a global that swings with the hour),
  so a highlight painted into the plate fights the lamp that is actually there. Flat matte local colour only;
  form comes from the ramp's own steps.

  NO GAME OBJECTS. The author's line - do not use the objects the game uses, not directly. Nothing from
  Assets/Resources is handed over as a reference: these are drawn from the palette and the geometry alone, so
  what comes back is a design and not a re-render of the collage.

  py -3 -X utf8 Tools/main_scene_gen.py take      submit / collect (re-run until every one is in)
  py -3 -X utf8 Tools/main_scene_gen.py snap      quantise each to the 55 and measure it
  py -3 -X utf8 Tools/main_scene_gen.py report    Tools/main_scene/report.html
"""
import io
import json
import os
import re
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
OUT = os.path.join(HERE, 'main_scene')
STATE = os.path.join(OUT, 'state.json')

sys.path.insert(0, HERE)
import pixellab                                    # noqa: E402

W, H = 640, 360                                    # DiegeticStage.Reference, to the pixel

# ── the palette (UITheme, GDD 16 §0): eleven ramps of five, and nothing else ──────────────────────
RAMPS = {
    'Night':    ('#0D0813', '#1A1023', '#241830', '#362447', '#4A3160'),
    'Magenta':  ('#5C1B45', '#8F2464', '#C23283', '#E84DA6', '#FF7DC6'),
    'Cyan':     ('#123B45', '#1B5F66', '#26918F', '#3BC8BE', '#7DF0E3'),
    'Amber':    ('#4A2E14', '#8F5A1E', '#C9822B', '#E8A33D', '#F5C97B'),
    'ViceRed':  ('#3D1220', '#6E1B32', '#A62B44', '#D9455C', '#F27D8A'),
    'ClubBlue': ('#131B3D', '#1F2E66', '#2E4699', '#4467CC', '#6E93F0'),
    'Lime':     ('#16331B', '#2A5926', '#479938', '#6FCC4B', '#A8F077'),
    'Cream':    ('#453E38', '#6E6459', '#9C8F80', '#C9BCA8', '#F2E8D5'),
    'Malt':     ('#3A2410', '#6B4416', '#9E6A1D', '#C98F2B', '#E6B959'),
    'Graphite': ('#14161A', '#24272D', '#383D45', '#545A64', '#808893'),
    'Brick':    ('#38161A', '#5C2226', '#7E3130', '#9C4740', '#B96253'),
}
PALETTE = np.array([[int(h[i:i + 2], 16) for i in (1, 3, 5)] for r in RAMPS.values() for h in r], dtype=np.int16)

# ── what every candidate has to be, whatever it looks like ───────────────────────────────────────
#
# The geometry is not decoration: the game draws on top of this plate and will not move for it. The counter owns
# the bottom third and its top must come back EMPTY (the sink, the ice, the tap and the cellar door are placed
# there at run time); the middle band is where the drinkers stand, so it stays clear; nobody is painted in.
FORM = (
    'ONE single continuous pixel-art painting of one room, drawn in one hand, edge to edge: no panels, no border, '
    'no collage. Straight-on eye-level view of a small 1980s cocktail bar from the customer side. A BAR COUNTER '
    'runs across the whole bottom third, left edge to right edge, its top completely EMPTY. Behind it the room '
    'opens: a seating area at the back with two small tables and chairs, a tall glass wall on the LEFT with the '
    'city outside, a side wall on the RIGHT with a plain doorway. The middle of the room stays open. '
)
DISCIPLINE = (
    'No people, no figures. No text, letters, numbers, logo or watermark. No bottles, glasses or shelves of stock. '
    'Flat matte local colour only: no reflections, no gloss, no specular, no cast shadows, no rim light, no glow, '
    'no bloom, no light rays, even flat lighting. Shade only in whole flat palette steps, never a smooth gradient. '
    'Hard pixel edges, 1px outlines, ordered dither for transitions, no anti-aliasing, no blur, no soft edges. '
)
PAL = ('Use only this palette: plum-black #0D0813 #241830 #4A3160, hot pink #8F2464 #C23283 #E84DA6 #FF7DC6, '
       'teal #1B5F66 #26918F #3BC8BE, amber #8F5A1E #C9822B #E8A33D #F5C97B, red #6E1B32 #A62B44 #D9455C, '
       'blue #1F2E66 #2E4699 #4467CC, cream #6E6459 #9C8F80 #C9BCA8 #F2E8D5, brown #6B4416 #9E6A1D #C98F2B, '
       'grey #24272D #383D45 #545A64, brick #5C2226 #7E3130 #9C4740.')

MAX_DESC = 2000        # create_image_pro's own ceiling, and it validates rather than truncates


def brief(direction):
    text = FORM + direction + ' ' + DISCIPLINE + PAL
    assert len(text) <= MAX_DESC, 'brief is %d characters, the ceiling is %d' % (len(text), MAX_DESC)
    return text


# ── six directions, one room ─────────────────────────────────────────────────────────────────────
JOBS = {
    'a_sunset': (5201, brief(
        'THE HOUR IS SUNSET AND THE WHOLE ROOM IS ONE COLOUR SCHEME: the glass wall on the left is full of a '
        'flat banded sunset - magenta into coral into gold - with a flat city skyline cut out black against it, '
        'and the whole room is painted in those same warm plums and corals so it reads as one picture. '
        'The counter front is deep plum, its top a dark warm slab, a single thin hot-pink line along its far edge.')),

    'b_neon': (5203, brief(
        'A DEEP NIGHT ROOM DRAWN BY ITS NEON: near-black plum walls, and the architecture itself is traced in '
        'thin flat tubes of hot pink and teal - a line along the top of the back wall, a line down each corner, '
        'a line along the counter\'s far edge, a plain abstract neon shape hung high on the back wall. '
        'The glass wall on the left is black with a grid of tiny lit city windows. Everything else stays dark.')),

    'c_deco': (5205, brief(
        'MIAMI DECO PASTEL: the back wall is a row of tall flat arches in cream and dusty pink with thin teal '
        'banding, the floor is pale terrazzo speckle, the ceiling a flat cream. Palm-leaf silhouettes stand in '
        'the corners. The counter is cream with a coral front panel and a teal top edge. Light, airy, pastel, '
        'the whole room one soft palette.')),

    'd_dive': (5207, brief(
        'A DARK WOOD DIVE: brick and dark panelled walls, a scuffed plank floor, heavy wooden chairs, a long '
        'solid wooden counter with a worn top. Browns, brick reds and greys throughout, with ONE small flat '
        'magenta neon shape on the back wall as the only bright thing in the room.')),

    'e_chrome': (5209, brief(
        'STEEL AND MIDNIGHT BLUE: a cold modern bar - grey steel counter with a brushed front, dark blue panelled '
        'walls, slim steel-framed furniture, a dark glass wall on the left showing a blue night city. '
        'Greys and deep blues throughout with one thin teal accent line. Austere, clean, flat.')),

    'f_terrace': (5211, brief(
        'THE ROOM OPENS TO THE NIGHT: the left third is not a window but an open terrace edge with a low railing, '
        'and a big flat city skyline with a low moon fills it, so the room feels like it is outdoors on one side. '
        'Warm plum interior, a long counter in dark wood running the full width, plants in the corners.')),
}


# ── the wire ─────────────────────────────────────────────────────────────────────────────────────

def _state():
    if os.path.exists(STATE):
        return json.load(io.open(STATE, encoding='utf-8'))
    return {}


def _save(s):
    os.makedirs(OUT, exist_ok=True)
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(s, indent=1, ensure_ascii=False))


def _call(tool, args, timeout=900):
    import contextlib
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        msgs = pixellab.call(tool, args, timeout=timeout)
    text = buf.getvalue()
    images = []
    for m in msgs or []:
        r = m.get('result') if isinstance(m, dict) else None
        for c in (r or {}).get('content', []):
            if c.get('type') == 'image' and c.get('data'):
                import base64
                images.append(base64.b64decode(c['data']))
    return text, images


def take(only=None):
    """Submit what has not been submitted, collect what is ready. Safe to re-run: a job id is written to the
    state BEFORE the picture comes back, so a dropped connection never pays twice."""
    os.makedirs(OUT, exist_ok=True)
    s = _state()
    waiting = 0
    for key, (seed, desc) in JOBS.items():
        if only and key not in only:
            continue
        png = os.path.join(OUT, key + '.png')
        if os.path.exists(png):
            continue
        e = s.get(key) or {}
        if e.get('job'):
            text, images = _call('get_image', {'job_id': e['job']}, timeout=300)
            if images:
                io.open(png, 'wb').write(images[0])
                print('  ->', key)
            else:
                waiting += 1
            continue
        text, images = _call('create_image_pro', {
            'description': desc, 'width': W, 'height': H, 'no_background': False, 'seed': seed})
        if images:
            io.open(png, 'wb').write(images[0])
            print('  ->', key)
            continue
        m = re.search(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}', text, re.I)
        s.setdefault(key, {})['job'] = m.group(0) if m else None
        _save(s)
        waiting += 1
        print('  queued %s %s' % (key, (m.group(0) if m else text.strip()[:90])))
    have = sum(1 for k in JOBS if os.path.exists(os.path.join(OUT, k + '.png')))
    print('%d of %d drawn, %d still cooking' % (have, len(JOBS), waiting))


def _snap(img):
    """Every pixel to its nearest of the 55. Nearest-match, not ordered: a room is form, not a shaded icon
    (the luma-ladder trick in Tools/topbar_gen.py is for a single object on one ramp)."""
    a = np.array(img.convert('RGB'), dtype=np.int16)
    flat = a.reshape(-1, 3)
    d = ((flat[:, None, :] - PALETTE[None, :, :]) ** 2).sum(axis=2)
    return Image.fromarray(PALETTE[d.argmin(axis=1)].astype(np.uint8).reshape(a.shape), 'RGB')


def snap(only=None):
    for key in JOBS:
        if only and key not in only:
            continue
        src = os.path.join(OUT, key + '.png')
        if not os.path.exists(src):
            continue
        img = Image.open(src)
        raw_colours = len(img.convert('RGB').getcolors(1 << 24) or [])
        out = _snap(img)
        out.save(os.path.join(OUT, key + '_snapped.png'))
        print('%-10s %dx%d  %5d colours -> %d on the palette' % (
            key, img.width, img.height, raw_colours, len(out.getcolors(1 << 24) or [])))


def _uri(path):
    import base64
    return 'data:image/png;base64,' + base64.b64encode(io.open(path, 'rb').read()).decode('ascii')


# Where the game's own furniture lands in the 640x360 plate, measured off the running scene: the counter's far
# edge (its neon line) and its front, and the band the drinkers stand in. A concept is only a direction until
# these line up, so the report draws them over every candidate.
COUNTER_TOP = 235
CROWD_TOP = 150


def report(only=None):
    rows = []
    for key, (seed, desc) in JOBS.items():
        raw = os.path.join(OUT, key + '.png')
        if not os.path.exists(raw):
            continue
        snapped = os.path.join(OUT, key + '_snapped.png')
        img = Image.open(raw)
        rows.append((key, _uri(raw), _uri(snapped if os.path.exists(snapped) else raw),
                     '%dx%d' % img.size, len(img.convert('RGB').getcolors(1 << 24) or []),
                     desc[len(FORM):len(desc) - len(DISCIPLINE) - len(PAL)].strip()))
    html = [
        '<!doctype html><meta charset="utf-8"><title>LAST CALL - ana sahne konseptleri</title>',
        '<style>body{background:#0D0813;color:#F2E8D5;font:14px/1.6 system-ui,sans-serif;margin:24px}'
        'h1{font-size:22px;margin:0 0 6px}p.lead{color:#9C8F80;margin:0 0 6px;max-width:78ch}'
        'p.lead b{color:#F2E8D5}section{margin:0 0 44px}h2{font-size:17px;color:#FF7DC6;margin:0 0 2px}'
        'p.brief{color:#6E6459;font-size:12px;margin:0 0 10px;max-width:120ch}'
        '.pair{display:flex;gap:18px;flex-wrap:wrap}figure{margin:0;position:relative}'
        'figcaption{color:#9C8F80;font-size:12px;margin-top:4px}'
        'img{image-rendering:pixelated;width:640px;height:360px;border:1px solid #362447;background:#000;display:block}'
        '.guide{position:absolute;left:0;right:0;height:0;border-top:1px dashed #3BC8BE;opacity:.75;pointer-events:none}'
        '.guide span{position:absolute;right:4px;top:-16px;font-size:10px;color:#3BC8BE;background:#0D0813;padding:0 3px}'
        '</style>',
        '<h1>Ana sahne &mdash; tek par&ccedil;a konseptler</h1>',
        '<p class="lead">Her biri <b>tek bir resim</b>: oda ve tezgah birlikte, <b>640&times;360</b> '
        '(DiegeticStage.Reference) boyutunda <b>do&#287;rudan</b> &uuml;retildi &mdash; hi&ccedil;bir yerinde '
        'k&uuml;&ccedil;&uuml;ltme yok. Oyunun hi&ccedil;bir nesnesi referans verilmedi.</p>',
        '<p class="lead">Solda ham &uuml;retim, sa&#287;da oyunun 55 renkli paletine oturtulmu&#351; hali. '
        'I&#351;&#305;k ve yans&#305;ma bilerek yok: oday&#305; URP 2D &#305;&#351;&#305;klar&#305; '
        'ayd&#305;nlat&#305;yor. Kesikli &ccedil;izgiler oyunun kendi bantlar&#305;: &uuml;sttekinin '
        'alt&#305;nda m&uuml;&#351;teriler duruyor, alttaki tezgah&#305;n &uuml;st kenar&#305;.</p>',
    ]
    for key, raw_uri, snap_uri, size, colours, desc in rows:
        guides = ('<div class="guide" style="top:%dpx"><span>m&uuml;&#351;teri band&#305;</span></div>'
                  '<div class="guide" style="top:%dpx"><span>tezgah &uuml;st&uuml;</span></div>'
                  % (CROWD_TOP, COUNTER_TOP))
        html.append('<section><h2>%s</h2><p class="brief">%s</p><div class="pair">'
                    '<figure><img src="%s">%s<figcaption>ham &middot; %s &middot; %d renk</figcaption></figure>'
                    '<figure><img src="%s">%s<figcaption>palete oturtulmu&#351;</figcaption></figure>'
                    '</div></section>' % (key, desc, raw_uri, guides, size, colours, snap_uri, guides))
    path = os.path.join(OUT, 'report.html')
    io.open(path, 'w', encoding='utf-8').write('\n'.join(html))
    print('wrote', path, '(%d concepts, %.1f MB)' % (len(rows), os.path.getsize(path) / 1e6))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'take'
    only = set(sys.argv[2:]) or None
    {'take': take, 'snap': snap, 'report': report}[cmd](only)
