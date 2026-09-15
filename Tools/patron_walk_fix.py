# -*- coding: utf-8 -*-
"""CLOSE THE WALK ON THE CAST ALREADY DRAWN (2026-09-07, the author: "walk animasyonunda
hepsinde problem var, animasyona smooth bir sekilde akmiyor, son frame ile ilk frame arasinda
kesinti oluyor").

Measured before it was touched: neighbouring frames of the shipped 9-frame walk differ by
about 5,000 pixels and the wrap from the last frame back to the first by about 7,300 - half
again as much. The clip is not a cycle at all; it is ONE step, and playing it on a loop asks
the eye to accept the missing step every 0.75 seconds.

The generator's answer for one-shots was two halves, and it is the answer here: the shipped
walk becomes half A (the right step) and this asks PixelLab for half B (the left step),
interpolated back to A's first frame so the join is DRAWN rather than cut. patron_ship then
concatenates them exactly as it does the order and the drink - 17 frames, and the last frame
is the pose the first one stands in.

    py -3 -X utf8 Tools/patron_walk_fix.py queue [slug ...]   ask for the missing halves
    py -3 -X utf8 Tools/patron_walk_fix.py pull  [slug ...]   collect and re-ship them
    py -3 -X utf8 Tools/patron_walk_fix.py check [slug ...]   the seam, in pixels
"""
import io
import json
import os
import re
import sys
import time

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
PATRON = os.path.join(ROOT, 'Assets', 'Resources', 'Patron')
sys.path.insert(0, HERE)

import patron_trial_gen as trial   # noqa: E402
import patron_ship                 # noqa: E402


def cast(only):
    names = only or sorted(d for d in os.listdir(PATRON)
                           if os.path.isdir(os.path.join(PATRON, d)))
    state = trial.load()
    return [n for n in names if state.get(n, {}).get('character_id')]


def queue(only=None):
    """Ask for walk_b wherever the walk is still a single half."""
    for slug in cast(only):
        d = os.path.join(PATRON, slug, 'walk')
        frames = len([n for n in os.listdir(d) if n.endswith('.png')]) if os.path.isdir(d) else 0
        if frames >= 17:
            print('  %-14s walk already %d frames' % (slug, frames))
            continue
        state = trial.load()
        clips = state[slug].setdefault('clips', {})
        # The shipped walk IS half A: name it so, so a re-ship joins the two under the same
        # rule the one-shots use.
        clips.setdefault('walk_a', clips.get('walk'))
        trial.save(state)
        # ...and seed the request from the SHIPPED walk, which is what the player is looking
        # at. trial.start_frame reads Resources/Patron/<slug>/<clip>, so the clip it is asked
        # for is 'walk' rather than the half's name; the spec is otherwise walk_b's own.
        spec = dict(trial.CLIPS['walk_b'])
        spec['start'] = ('walk', 'last')
        spec['end'] = ('walk', 'first')
        saved = trial.CLIPS['walk_b']
        trial.CLIPS['walk_b'] = spec
        try:
            trial.animate('walk_b', [slug])
        finally:
            trial.CLIPS['walk_b'] = saved


def pull(only=None):
    """Collect the halves and re-ship the patron, which joins them."""
    for slug in cast(only):
        trial.pull(slug)
        patron_ship.ship(slug)


def seam(slug):
    """How far the last frame is from the first, in pixels - a closed cycle is small."""
    d = os.path.join(PATRON, slug, 'walk')
    if not os.path.isdir(d):
        return None
    names = sorted(n for n in os.listdir(d) if n.endswith('.png'))
    if len(names) < 3:
        return None
    ims = [Image.open(os.path.join(d, n)).convert('RGBA') for n in (names[0], names[-2], names[-1])]
    first, penult, last = ims

    def diff(a, b):
        pa, pb = a.load(), b.load()
        return sum(1 for y in range(a.height) for x in range(a.width) if pa[x, y] != pb[x, y])

    return len(names), diff(last, first), diff(penult, last)


def check(only=None):
    print('%-14s %5s  %8s  %8s  %s' % ('slug', 'n', 'wrap', 'neighbour', ''))
    for slug in cast(only):
        got = seam(slug)
        if not got:
            continue
        n, wrap, near = got
        # A cycle closes when the wrap is no worse than an ordinary step between frames.
        verdict = 'closed' if wrap <= near * 1.3 else 'JUMPS'
        print('%-14s %5d  %8d  %8d  %s' % (slug, n, wrap, near, verdict))


# ── the walks whose PixelLab characters are gone (2026-09-15) ─────────────────────────
# Ten walks are still ONE step: clubgirl, driftgirl, eastasianman, guard, harajuku, leopard,
# pastelman, salaryman, shaved and silkwoman. Their characters no longer exist on the account,
# so queue() cannot ask for half B. animate_image can: it animates the shipped walk's LAST frame
# towards its FIRST - the same interpolated left step the others were given, drawn from the
# pixels instead of from the character. Measured on that route: the frames it hands back are
# the input, then the generated ones, and the last generated frame is the pinned end, so the
# loop is the shipped nine plus the generated frames between those two copies.
IMAGE_STATE = os.path.join(HERE, 'patron_walk_image_state.json')
UUID = r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}'


def walk_frames(slug):
    d = os.path.join(PATRON, slug, 'walk')
    names = sorted(n for n in os.listdir(d) if n.endswith('.png')) if os.path.isdir(d) else []
    return d, names


def queue_image(only):
    state = json.load(io.open(IMAGE_STATE, encoding='utf-8')) if os.path.exists(IMAGE_STATE) else {}
    for slug in only or []:
        if state.get(slug):
            print('  %-14s already queued (%s)' % (slug, state[slug][:8]))
            continue
        d, names = walk_frames(slug)
        if len(names) != 9:
            print('  %-14s walk has %d frames, not the single nine-frame step' % (slug, len(names)))
            continue
        first = Image.open(os.path.join(d, names[0])).convert('RGBA')
        last = Image.open(os.path.join(d, names[-1])).convert('RGBA')
        args = {'action': trial.CUSTOM['walk_b'], 'frame_count': 8, 'no_background': True,
                'first_frame_base64': trial.frame_b64(last),
                'last_frame_base64': trial.frame_b64(first)}
        # The server runs twenty jobs at a time and says so two ways ("need 1 job slots",
        # "rate limit exceeded (20/20 jobs)"): wait for one to finish rather than give up.
        for _ in range(30):
            text, _ = trial.call('animate_image', args)
            low = text.lower()
            if not ('rate limit' in low or ('slot' in low and 'available' in low)):
                break
            time.sleep(45)
        job = next(iter(re.findall(UUID, text, re.I)), None)
        if not job:
            print('  %-14s NO JOB: %s' % (slug, text[:240].replace('\n', ' ')))
            continue
        state[slug] = job
        io.open(IMAGE_STATE, 'w', encoding='utf-8').write(json.dumps(state, indent=1))
        trial.log({'asset': 'patron/' + slug, 'event': 'walk_b by animate_image', 'job': job})
        print('  %-14s queued %s' % (slug, job))


def pull_image(only):
    import pixellab
    import patron_ink
    state = json.load(io.open(IMAGE_STATE, encoding='utf-8')) if os.path.exists(IMAGE_STATE) else {}
    for slug, job in sorted(state.items()):
        if only and slug not in only:
            continue
        d, names = walk_frames(slug)
        if len(names) != 9:
            print('  %-14s walk already %d frames' % (slug, len(names)))
            continue
        text, images = trial.call('get_image', {'job_id': job})
        if 'status: completed' not in text:
            print('  %-14s %s' % (slug, ' '.join(text.split())[:140]))
            continue
        # The answer inlines four frames and leaves the rest behind download links.
        count = re.search(r'frames:\s*(\d+)', text)
        base = re.search(r'download:\s*(\S+)\?index=0', text)
        if count and base and int(count.group(1)) > len(images):
            images = [Image.open(io.BytesIO(pixellab.fetch_url('%s?index=%d' % (base.group(1), i))))
                      .convert('RGBA') for i in range(int(count.group(1)))]
        for i, im in enumerate(images):
            im.save(os.path.join(trial.RAW, '%s_walk_b_image_%02d.png' % (slug, i)))
        first = Image.open(os.path.join(d, names[0])).convert('RGBA')

        def changed(a, b):
            pa, pb = a.load(), b.load()
            return sum(1 for y in range(a.height) for x in range(a.width) if pa[x, y] != pb[x, y])

        # Drop the input copy; drop the last generated frame only when it IS the pinned first.
        new = images[1:]
        if new and changed(new[-1], first) < 200:
            new = new[:-1]
        for k, f in enumerate(new):
            if f.size != first.size:
                print('  %-14s generated frame %d is %s, not %s - stopping' % (slug, k, f.size, first.size))
                new = []
                break
            patron_ink.reink(f)
            f.save(os.path.join(d, 'walk_%02d.png' % (9 + k)))
        if new:
            trial.log({'asset': 'patron/' + slug, 'event': 'walk joined', 'frames': 9 + len(new)})
            print('  %-14s walk now %d frames' % (slug, 9 + len(new)))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'check'
    rest = sys.argv[2:] or None
    if cmd == 'queue':
        queue(rest)
    elif cmd == 'pull':
        pull(rest)
    elif cmd == 'queue-image':
        queue_image(rest)
    elif cmd == 'pull-image':
        pull_image(rest)
    else:
        check(rest)
