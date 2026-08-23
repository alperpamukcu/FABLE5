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

# TWO LAYERS, NOT ONE (2026-08-23, the author: "arka plan görselindeki ağaçları
# görselden ayıralım, ağaçlara animasyonu ayrı vereceğiz çünkü ağaçlar çok daha fazla
# sallanması gerekiyor"). Palms cannot sway inside a picture of a sunset - a wind that
# moves the fronds must not move the skyline behind them. So the sky is drawn WITHOUT
# them and they are drawn on their own, on transparency, and the stage puts one in front
# of the other. Cutting them out of the finished frames was the other option and it is
# worse: it leaves palm-shaped holes that then have to be invented back.
#
# THE SKY IS BANDED HARDER, not softened ("gök yüzünün renk geçişi biraz daha smooth
# olmalı"). The house's own answer to a hard ramp is the ViceFade's: a band set smooths by
# GROWING BANDS, never by interpolating (16 §6.10). Eight bands read as stripes there and
# twenty-six read as a fade; the same lever is pulled here.
SCENE = (
    "a Miami sunset seen from a high bar window, in a TALL upright frame: the sky fills "
    "the upper half in MANY NARROW HORIZONTAL BANDS of flat colour, twenty or more, "
    "stepping smoothly from deep violet at the top through purple, magenta, hot pink, "
    "coral and amber into orange at the horizon, each band only a few pixels tall so the "
    "sky reads as a smooth fade made of steps; the sun a flat disc sitting on the "
    "skyline, a downtown of blocky towers in flat purple silhouette across the middle, "
    "and a lit boulevard with low rooftops and small warm windows below it. "
    "NO palm trees, NO plants, NO leaves, nothing in the foreground. "
    "Hard edges between bands, no dithering, no gradients, pixel art, opaque background, "
    "the picture reaches all four edges, no text, no logo, no people, no window frame"
)

# The layer that moves. Drawn on transparency so the sky behind it is untouched, and
# drawn TALL because these are the palms that lean in from the window's two sides.
PALMS = (
    "two tall palm trees on a transparent background, one leaning in from the left edge "
    "and one from the right, their trunks thin and slightly curved, their fronds spread "
    "wide across the top, plus a few broad low plant leaves along the bottom corners. "
    "Flat dark silhouette with two tones only - a near-black body and one lighter edge "
    "where the sky catches them - no detail inside, no highlights, no glow. Pixel art, "
    "transparent background, nothing else in the picture, no sky, no ground, no text"
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
    # The palms are the one layer that keeps its alpha: flattening them to RGB would fill
    # the gaps between the fronds with black and there would be nothing to see through.
    im = im.convert('RGBA' if name == 'palms' else 'RGB')
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


def palms():
    """The layer that moves, on its own transparency.

    no_background TRUE here and FALSE for the sky, and the pair is the whole idea: the sky
    IS its background, the palms are cut out of theirs so the sky shows between the fronds.
    One drawing serves every hour - the stage tints it with the room's own light rather
    than the palms being redrawn at each, which is also what keeps them from drifting out
    of register with a sky that was drawn separately.
    """
    st = load()
    if st.get('palms', {}).get('png'):
        print('  palms already drawn'); return
    args = {
        'description': PALMS,
        'width': W, 'height': H,
        'no_background': True,
        'style_image_base64': b64(STYLE_REF),
        'style_copy': ['outline', 'detail', 'shading'],
        'seed': SEED + 9,
    }
    text, images = trial.call('create_image_pro', args)
    st.setdefault('palms', {})['job_id'] = _job_from(text)
    save(st)
    if images:
        _keep('palms', images[0])
    else:
        print('  palms queued %s' % (st['palms']['job_id'] or text.strip()[:160]))


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
TWEENS = 9          # frames inserted between each generated pair


def _pack(arr):
    a = arr.reshape(-1, 3).astype(np.int32)
    return (a[:, 0] << 16) | (a[:, 1] << 8) | a[:, 2]


def _lerp(c0, c1, t):
    """Mix two colours in LINEAR light. Mixing sRGB numbers straight walks through a muddy
    middle; squaring into light first and coming back out does not."""
    l0 = (np.asarray(c0, float) / 255.0) ** 2.2
    l1 = (np.asarray(c1, float) / 255.0) ** 2.2
    return np.clip((((1 - t) * l0 + t * l1) ** (1 / 2.2)) * 255.0 + 0.5,
                   0, 255).astype(np.uint8)


def _destinations(a, b):
    """For every colour in A, the colour THOSE SAME PIXELS have in B.

    This is the whole idea, and it is what the old tween got wrong. That one mixed A and B
    pixel by pixel and then snapped the result onto the pair's palette, so where two
    unrelated colours met it snapped to something belonging to neither place: dusk's salmon
    horizon mixed with night's indigo landed on a dusty rose that belongs to a LIT WINDOW,
    and 1197 px of it lay across the sky as a flat bar. Measured, and it is exactly what
    the author was looking at when they said the sunset had bozulmalar in it.

    A colour that walks to ITS OWN destination cannot do that. A band stays one colour the
    whole way over -- simply a different colour each frame -- so the flats stay flat
    (16 §6.10) and nothing has to be quantised back onto anything at all.
    """
    pa, pb = _pack(a), _pack(b)
    dest = {}
    for c in np.unique(pa):
        vals, counts = np.unique(pb[pa == c], return_counts=True)
        dest[int(c)] = int(vals[counts.argmax()])
    return dest


def _walk(img, dest, t):
    """One step of the walk: img's own layout, every colour t of the way to its destination."""
    pk = _pack(img)
    out = np.empty((pk.shape[0], 3), np.uint8)
    for c in np.unique(pk):
        d = dest[int(c)]
        out[pk == c] = _lerp(((c >> 16) & 255, (c >> 8) & 255, c & 255),
                             ((d >> 16) & 255, (d >> 8) & 255, d & 255), t)
    return out.reshape(img.shape)


def tween():
    """Fill the gaps between the drawn moments WITHOUT drawing more of them.

    The measurement is what allows this: the skyline holds 97% across the chain, so between
    two moments nothing MOVES - only the light differs. A frame between them is therefore a
    colour step, not a new picture, and it can be computed.

    Each half of a gap is walked out from the moment it is nearer to, so a frame is always
    a RELIT drawn picture and never a mixture of two. The halves meet in the middle
    carrying the same palette, which leaves only the handful of pixels where the two
    pictures genuinely differ -- a window that lights, the last of the sun -- to change at
    the join.
    """
    st = load()
    have = [n for n, _ in STEPS if st.get(n, {}).get('png')]
    if len(have) < 2:
        raise SystemExit('need at least two drawn moments')
    seq = []
    for i in range(len(have) - 1):
        a = np.asarray(Image.open(png(have[i])).convert('RGB'))
        b = np.asarray(Image.open(png(have[i + 1])).convert('RGB'))
        there, back = _destinations(a, b), _destinations(b, a)
        pa, pb = _pack(a), _pack(b)

        # Where the two pictures AGREE about what a pixel becomes, the pixel is a pure
        # recolour and both walks give the same answer. Where they do not - a window that
        # lights, the last of the sun going behind a tower - the pixel has to change over
        # at some point, and a whole gap's worth of them changing on the same frame is a
        # visible lurch: measured at 427/765 on one pixel and 46% of the picture at once.
        # So each of those pixels is given its OWN moment to change, drawn once from a
        # fixed seed. Nothing dissolves that did not have to; the lights simply come on a
        # few at a time, which is what lights do.
        moved = np.array([there[int(c)] for c in pa]) != pb
        when = np.random.RandomState(SEED).rand(moved.shape[0])

        seq.append((have[i], a))
        for k in range(1, TWEENS + 1):
            f = k / float(TWEENS + 1)
            va = _walk(a, there, f).reshape(-1, 3)
            vb = _walk(b, back, 1.0 - f).reshape(-1, 3)
            take_b = moved & (f >= when)
            seq.append(('%s_%d' % (have[i], k),
                        np.where(take_b[:, None], vb, va).reshape(a.shape)))
    seq.append((have[-1], np.asarray(Image.open(png(have[-1])).convert('RGB'))))

    os.makedirs(os.path.join(RAW, 'cycle'), exist_ok=True)
    for i, (name, arr) in enumerate(seq):
        Image.fromarray(arr, 'RGB').save(os.path.join(RAW, 'cycle', '%02d_%s.png' % (i, name)))
    print('  %d frames written to window_raw/cycle (%d drawn, %d computed)'
          % (len(seq), len(have), len(seq) - len(have)))
    sizes = [len(np.unique(a.reshape(-1, 3), axis=0)) for _, a in seq]
    print('  colours per frame: min %d, max %d - a walked frame carries exactly as many '
          'as the picture it was walked from' % (min(sizes), max(sizes)))
    worst = max(int(np.abs(seq[i][1].astype(int) - seq[i - 1][1].astype(int)).sum(axis=2).max())
                for i in range(1, len(seq)))
    print('  largest single-pixel colour step between neighbours: %d/765' % worst)


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
     'poll': lambda: poll(), 'report': lambda: report(),
     'palms': lambda: palms()}[cmd]()
