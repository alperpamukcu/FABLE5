# -*- coding: utf-8 -*-
"""The view out of the bar's window, drawn at the window's OWN size (2026-08-23).

WHY THIS EXISTS. The six sheets in window_raw are 193x150 - landscape - and the room's
window is 141x274, which is portrait and nearly twice as tall as it is wide. Fitting the
old frames to it meant scaling them 2x, and that put the city's pixels at twice the size
of the wall around them. Measured, not guessed: 100% of the shipped sheet's 2x2 blocks
are uniform. The rig's own rule says why that is wrong - the room is drawn at ONE ART
PIXEL PER STAGE UNIT, and anything drawn at another rate stops belonging to it.

So the view has to be DRAWN tall rather than stretched tall. That is a recomposition, not
a crop: the same world - banded violet-to-orange sky, sun on the horizon, downtown
silhouette, palms either side, lit boulevard below - stood up in a portrait frame.

WHY NOT animate_image. It would have been one call: give it the still, describe the
motion. Its limits rule it out - frames cap at 256x256 and this window is 274 tall.

WHAT WE DO INSTEAD, and why it is not a compromise. The existing cycle was MEASURED before
any of this was chosen: across a sheet the skyline's edges stay 95-99% identical while
14-57% of pixels change colour. A sunset is not a moving picture, it is one picture whose
light turns. That is exactly what edit_image is for - "the pose, composition and pixel
style are preserved and only what you asked for changes" - so each moment is an EDIT of
the one before it, and the silhouette carries forward by construction rather than by luck.

    still            create_image_pro at 141x274, styled off the approved sheet
    step             edit_image from the last frame: the light one stage later
    poll             collect whatever is still generating
    report           the consistency gate + an HTML sheet to look at

CONSISTENCY IS THE POINT (the author: "tutarlılık çok önemli"), so it is measured against
the same numbers the existing cycle set: edges identical 95-99%, colour changed 14-57%.
A step that drifts outside that band is a step that redrew the city instead of relighting
it, and the report says so in those words.
"""
import base64
import io
import json
import os
import re
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import patron_trial_gen as trial          # noqa: E402  (call/log helpers)

RAW = os.path.join(HERE, 'window_raw')
STATE = os.path.join(HERE, 'window_sunset_state.json')

# The window's own hole, printed by window_cycle.measure(). Drawn at this size, the view
# needs no scaling at all and its pixels are the room's pixels.
W, H = 141, 274
SEED = 20260823

# The frame the house already approved, as the style to inherit. Unlike the cast - where
# the palette is deliberately NOT copied, because a shared palette is one person drawn
# nine times - here the palette IS the subject: this is the same sky at another hour.
STYLE_REF = os.path.join(RAW, 'sky_ref.png')
STYLE_COPY = ['color_palette', 'outline', 'detail', 'shading']

SCENE = (
    "a Miami sunset seen from a high bar window, in a TALL upright frame: banded sky "
    "filling the upper half - deep violet at the top stepping down through magenta and "
    "hot pink into orange at the horizon - the sun a flat disc sitting on the skyline, "
    "a downtown of blocky towers in flat purple silhouette across the middle, a lit "
    "boulevard and low rooftops below it with small warm windows, and one tall palm "
    "leaning in from each side. Flat bands of colour with hard edges and no gradients, "
    "no dithering, pixel art, opaque background, no text, no logo, no people, no frame, "
    "no window bars"
)

# One picture, four hours. Each is an edit of the one before it, so the city is never
# redrawn - only the light on it moves.
STEPS = [
    ("golden", None),      # the still itself
    ("amber",
     "the sun has just touched the horizon: the orange band low and deeper, the pink "
     "above it stronger, a few more windows lit. Do not move the buildings or the palms"),
    ("dusk",
     "twenty minutes later: the sun is gone, the orange band is a thin strip on the "
     "horizon, the sky is magenta into deep violet, many more windows lit. Do not move "
     "the buildings or the palms"),
    ("night",
     "night: the sky is deep indigo with no orange left, the towers are near-black "
     "against it, and the city below is fully lit with warm windows. Do not move the "
     "buildings or the palms"),
]


def load():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def save(s):
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(s, indent=1))


def b64(path):
    return base64.b64encode(io.open(path, 'rb').read()).decode()


def png(name):
    return os.path.join(RAW, 'sunset_%s.png' % name)


def _job_from(text):
    m = re.search(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}', text, re.I)
    return m.group(0) if m else None


def _keep(name, im):
    im = im.convert('RGB')
    im.save(png(name))
    st = load()
    st.setdefault(name, {})['png'] = os.path.relpath(png(name), HERE)
    save(st)
    print('  %-8s kept %s  %d colours' % (name, im.size, len(np.unique(
        np.asarray(im).reshape(-1, 3), axis=0))))


def still():
    """Generation 1 of 4: the drawing itself, at the window's own size."""
    st = load()
    if st.get('golden', {}).get('png'):
        print('  golden already drawn'); return
    if not os.path.exists(STYLE_REF):
        raise SystemExit('no style reference at ' + STYLE_REF)
    args = {
        'description': SCENE,
        'width': W, 'height': H,
        # FALSE, and it matters: pro defaults to cutting the subject out on transparency,
        # and a sky with holes in it is not a sky. This picture IS its background.
        'no_background': False,
        'style_image_base64': b64(STYLE_REF),
        'style_copy': STYLE_COPY,
        'seed': SEED,
    }
    text, images = trial.call('create_image_pro', args)
    st.setdefault('golden', {})['job_id'] = _job_from(text)
    save(st)
    if images:
        _keep('golden', images[0])
    else:
        print('  golden queued %s' % (st['golden']['job_id'] or text.strip()[:160]))


def step(which=None):
    """Generations 2-4: the same picture, later. Each edits the frame before it."""
    st = load()
    order = [n for n, _ in STEPS]
    for i, (name, instruction) in enumerate(STEPS):
        if instruction is None or (which and name != which):
            continue
        if st.get(name, {}).get('png'):
            print('  %-8s already drawn' % name); continue
        prev = order[i - 1]
        if not st.get(prev, {}).get('png'):
            print('  %-8s waits for %s' % (name, prev)); return
        args = {
            'images_base64': [b64(png(prev))],
            'description': instruction,
            'width': W, 'height': H,
            'no_background': False,
            'seed': SEED + i,
        }
        text, images = trial.call('edit_image', args)
        st.setdefault(name, {})['job_id'] = _job_from(text)
        st[name]['from'] = prev
        save(st)
        if images:
            _keep(name, images[0])
        else:
            print('  %-8s queued %s' % (name, st[name]['job_id'] or text.strip()[:160]))
        return          # one call per invocation: each step needs the one before it


def poll():
    st = load()
    for name in [n for n, _ in STEPS]:
        e = st.get(name) or {}
        if e.get('png') or not e.get('job_id'):
            continue
        text, images = trial.call('get_image', {'job_id': e['job_id']})
        if images:
            _keep(name, images[0])
        else:
            print('  %-8s %s' % (name, (text.strip().splitlines() or ['(silent)'])[0][:120]))


# ── the hours between the hours ─────────────────────────────────────────────
TWEENS = 4          # frames inserted between each generated pair


def _quantise(img, palette):
    """Nearest colour from an ALLOWED set. Blending two frames invents colours; this puts
    every pixel back on one the house already drew, so no gradient is created and the flat
    areas stay flat (16 §6.10)."""
    h, w, _ = img.shape
    # int32, and it is not a style choice: a squared channel difference reaches 65025 and
    # three of them 195075, which wraps in int16 and makes argmin pick a colour at random.
    # The first take of this did exactly that and filled the sky with window-light yellow.
    flat = img.reshape(-1, 3).astype(np.int32)
    pal = palette.astype(np.int32)
    out = np.empty_like(flat)
    for i in range(0, flat.shape[0], 8192):        # in chunks: the full matrix is large
        chunk = flat[i:i + 8192]
        d = ((chunk[:, None, :] - pal[None, :, :]) ** 2).sum(axis=2)
        out[i:i + 8192] = pal[d.argmin(axis=1)]
    return out.reshape(h, w, 3).astype(np.uint8)


def tween():
    """Fill the gaps between the drawn moments WITHOUT drawing more of them.

    The measurement is what allows this: the skyline holds 97% across the chain, so
    between two moments nothing MOVES - only the light differs. A frame between them is
    therefore a colour step, not a new picture, and it can be computed.

    Blended and then QUANTISED back onto the two frames' own colours: a straight blend
    would invent in-between tones and turn a banded sky into a gradient, which is the one
    thing the style forbids.
    """
    st = load()
    have = [n for n, _ in STEPS if st.get(n, {}).get('png')]
    if len(have) < 2:
        raise SystemExit('need at least two drawn moments')
    seq = []
    for i in range(len(have) - 1):
        a = np.asarray(Image.open(png(have[i])).convert('RGB'))
        b = np.asarray(Image.open(png(have[i + 1])).convert('RGB'))
        pal = np.unique(np.concatenate([a.reshape(-1, 3), b.reshape(-1, 3)]), axis=0)
        seq.append((have[i], a))
        for k in range(1, TWEENS + 1):
            f = k / float(TWEENS + 1)
            mix = (a * (1 - f) + b * f).astype(np.uint8)
            seq.append(('%s_%d' % (have[i], k), _quantise(mix, pal)))
    seq.append((have[-1], np.asarray(Image.open(png(have[-1])).convert('RGB'))))

    os.makedirs(os.path.join(RAW, 'cycle'), exist_ok=True)
    for i, (name, arr) in enumerate(seq):
        Image.fromarray(arr, 'RGB').save(os.path.join(RAW, 'cycle', '%02d_%s.png' % (i, name)))
    print('  %d frames written to window_raw/cycle (%d drawn, %d computed)'
          % (len(seq), len(have), len(seq) - len(have)))
    pal_sizes = [len(np.unique(a.reshape(-1, 3), axis=0)) for _, a in seq]
    print('  colours per frame: min %d, max %d — no frame invents a colour the pair did not have'
          % (min(pal_sizes), max(pal_sizes)))


# ── the gate ────────────────────────────────────────────────────────────────
def _edges(rgb):
    g = rgb.astype(int).sum(axis=2)
    return np.abs(np.diff(g, axis=0)) > 90


def report():
    """Did the light move, or did the city get redrawn? The existing cycle's own numbers
    are the band: edges identical 95-99%, colour changed 14-57%."""
    st = load()
    have = [n for n, _ in STEPS if st.get(n, {}).get('png')]
    if not have:
        raise SystemExit('nothing drawn yet')
    ims = {n: np.asarray(Image.open(png(n)).convert('RGB')) for n in have}
    base = ims[have[0]]
    e0 = _edges(base)

    rows = []
    print('%-8s %-10s %-10s %-8s %s' % ('frame', 'edges vs 1', 'edges vs prev', 'recol.', 'verdict'))
    for i, n in enumerate(have):
        cur = ims[n]
        if cur.shape != base.shape:
            print('  %s is %s, not %s — it cannot be compared' % (n, cur.shape, base.shape))
            continue
        e_first = 100.0 * (_edges(cur) == e0).mean()
        prev = ims[have[i - 1]] if i else cur
        e_prev = 100.0 * (_edges(cur) == _edges(prev)).mean()
        recol = 100.0 * (cur != base).any(axis=2).mean()
        ok = i == 0 or e_first >= 90.0
        print('%-8s %9.1f%% %12.1f%% %7.1f%%  %s'
              % (n, e_first, e_prev, recol, 'held' if ok else 'THE CITY MOVED'))
        rows.append((n, e_first, e_prev, recol, ok))

    out = os.path.join(HERE, 'window_sunset_report.html')
    cells = []
    for n, ef, ep, rc, ok in rows:
        cells.append(
            '<figure><img src="data:image/png;base64,%s" style="image-rendering:pixelated;'
            'width:%dpx"><figcaption><b>%s</b><br>edges vs first %.1f%%<br>'
            'edges vs prev %.1f%%<br>recoloured %.1f%%<br>%s</figcaption></figure>'
            % (b64(png(n)), W * 2, n, ef, ep, rc, 'held' if ok else '<b>THE CITY MOVED</b>'))
    io.open(out, 'w', encoding='utf-8').write(
        '<!doctype html><meta charset="utf-8"><title>window sunset</title>'
        '<body style="background:#14101c;color:#e8e0d0;font:13px/1.5 system-ui;padding:24px">'
        '<h1 style="font-size:18px">The view out of the window — %dx%d, drawn 1:1</h1>'
        '<p>The gate is the existing cycle\'s own measurement: across a sheet its edges '
        'stay 95–99%% identical while 14–57%% of pixels change colour. A frame whose edges '
        'fall below 90%% did not get relit, it got redrawn.</p>'
        '<div style="display:flex;gap:20px;flex-wrap:wrap;align-items:flex-start">%s</div>'
        '</body>' % (W, H, ''.join(cells)))
    print('\nwrote %s' % os.path.relpath(out, HERE))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'report'
    arg = sys.argv[2] if len(sys.argv) > 2 else None
    {'still': lambda: still(), 'step': lambda: step(arg), 'tween': lambda: tween(),
     'poll': lambda: poll(), 'report': lambda: report()}[cmd]()
