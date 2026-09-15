# -*- coding: utf-8 -*-
"""ARRIVING AND LEAVING, FOR THE WHOLE CAST (2026-09-15, the author, on the walk pilot: "şablon ve
sabitleme çok daha iyi buna göre üretim ve eklemeleri yap oyuna bağlansın").

A customer used to walk to the stool, slow to a third of their pace, and snap from a profile to
facing the room in one frame; leaving was the same snap backwards. Two clips replace both snaps:

  ARRIVE  the walk's own first pose -> slow, turn to face the room, settle as if onto the stool
          -> the idle. The game lands the walk-in on a cycle boundary so the join is exact.
  LEAVE   the idle -> rise, turn RIGHT -> the walk's pose mirrored, because the door is on the
          right and the walk out is the walk flipped (TycoonHud.AdvanceExit).

Both ends are pinned, so each clip joins the clips either side of it. Two routes, because the
first cast's PixelLab characters are gone from the account:

  living  animate_character, v3, the character's own identity; the walk pose is the TEMPLATE
          walk's first frame fitted onto the front clips' canvas (the server refuses a pair of
          frames of two sizes - patron_motion.fit_canvas)
  gone    animate_image between the shipped frames; the walk is the shipped one, steadied and
          locked to the idle's colours here rather than re-shipped from a zip it never came from

    patron_transitions.py queue [slug ...]   ask for arrive + leave (every drawn face when none)
    patron_transitions.py pull  [slug ...]   collect whatever has landed
    patron_transitions.py ship  [slug ...]   write walk, arrive and leave under Resources/Patron
"""
import io
import json
import os
import re
import subprocess
import sys
import time

from PIL import Image, ImageOps

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, HERE)
import pixellab                    # noqa: E402
import patron_gen                  # noqa: E402
import patron_motion               # noqa: E402
import patron_trial_gen as trial   # noqa: E402

PATRON = os.path.join(ROOT, 'Assets', 'Resources', 'Patron')
STATE = os.path.join(HERE, 'patron_transitions_state.json')
UUID = r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}'
NAMES = ('arrive', 'leave')


def say(*a):
    print(time.strftime('%H:%M:%S'), *a, flush=True)


def load():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def save(state):
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(state, indent=1))


def cast():
    return sorted(d for d in os.listdir(PATRON)
                  if os.path.isdir(os.path.join(PATRON, d, 'idle')))


def living(slug):
    """A face whose character still answers - known by the template walk it was given."""
    return 'walk_t' in trial.load().get(slug, {}).get('clips', {})


def shipped(slug, clip):
    d = os.path.join(PATRON, slug, clip)
    return [Image.open(os.path.join(d, n)).convert('RGBA')
            for n in sorted(os.listdir(d)) if n.endswith('.png')] if os.path.isdir(d) else []


def zip_groups(slug):
    return patron_gen.frames_from_zip(io.open(os.path.join(trial.RAW, slug + '_anim.zip'), 'rb').read())


def walk_source(slug):
    """The gone cast's walk as it shipped before it was steadied, copied once under the raw folder,
    so a second ship steadies the original and never its own output."""
    def path(i):
        return os.path.join(trial.RAW, '%s_walk_src_%02d.png' % (slug, i))
    if not os.path.exists(path(0)):
        for i, f in enumerate(shipped(slug, 'walk')):
            f.save(path(i))
    frames, i = [], 0
    while os.path.exists(path(i)):
        frames.append(Image.open(path(i)).convert('RGBA'))
        i += 1
    return frames


def image_frames(slug, name):
    frames, i = [], 0
    while os.path.exists(os.path.join(trial.RAW, '%s_%s_img_%02d.png' % (slug, name, i))):
        frames.append(Image.open(os.path.join(trial.RAW, '%s_%s_img_%02d.png' % (slug, name, i))).convert('RGBA'))
        i += 1
    return frames


def poses(slug):
    """(the walk's pose, the front pose), on one canvas."""
    if living(slug):
        g = zip_groups(slug)
        front = g['order_a'][0]
        return patron_motion.fit_canvas(g['walk_t'][0], front.size), front
    return walk_source(slug)[0], shipped(slug, 'idle')[0]


def call(tool, args):
    """tools/call, waiting for a job slot when the server has none (20 at a time)."""
    for _ in range(30):
        text, images = trial.call(tool, args)
        low = text.lower()
        # the same refusal comes in two wordings: "need 1 job slots" and "rate limit exceeded"
        if 'rate limit' in low or ('slot' in low and 'available' in low):
            time.sleep(45)
            continue
        return text, images
    return text, images


def drop_old(slug, cid, name):
    """Delete an earlier take of the same name first: two groups named alike merge in the zip."""
    text, _ = trial.call('get_character', {'character_id': cid, 'include_preview': False})
    for line in text.splitlines():
        m = re.match(r'\s+%s — .*\[group: (%s)\]' % (re.escape(name), UUID), line)
        if m:
            out, _ = trial.call('delete_animation', {'character_id': cid, 'animation_group_id': m.group(1)})
            say(slug, 'deleted the earlier', name, ' '.join(out.split())[:80])


def queue(slugs):
    state = load()
    tstate = trial.load()
    for slug in slugs:
        entry = state.setdefault(slug, {})
        if all(entry.get(n) for n in NAMES):
            continue
        walk, front = poses(slug)
        for name, start, end in (('arrive', walk, front), ('leave', front, ImageOps.mirror(walk))):
            if entry.get(name):
                continue
            if living(slug):
                cid = tstate[slug]['character_id']
                drop_old(slug, cid, name)
                text, _ = call('animate_character', {
                    'character_id': cid, 'animation_name': name,
                    'directions': trial.CLIPS[name]['directions'], 'mode': 'v3', 'frame_count': 8,
                    'action_description': trial.CUSTOM[name], 'keep_first_frame': False,
                    'custom_start_frame_base64': trial.frame_b64(start),
                    'end_frame_base64': trial.frame_b64(end)})
            else:
                cid = None
                text, _ = call('animate_image', {
                    'action': trial.CUSTOM[name], 'frame_count': 8, 'no_background': True,
                    'first_frame_base64': trial.frame_b64(start),
                    'last_frame_base64': trial.frame_b64(end)})
            job = next((i for i in re.findall(UUID, text, re.I) if i != cid), None)
            if not job:
                say(slug, name, 'NO JOB', text[:200].replace('\n', ' '))
                continue
            entry[name] = job
            entry['route'] = 'living' if cid else 'image'
            save(state)
            trial.log({'asset': 'patron_transitions/' + slug, 'event': 'queued', 'clip': name,
                       'route': entry['route'], 'job': job})
            say(slug, name, 'queued', entry['route'], job)


def landed(slug):
    entry = load().get(slug, {})
    if entry.get('route') == 'living':
        g = zip_groups(slug)
        return all(g.get(n) for n in NAMES)
    return all(image_frames(slug, n) for n in NAMES)


def pull(slugs):
    state = load()
    for slug in slugs:
        entry = state.get(slug, {})
        if not all(entry.get(n) for n in NAMES) or landed(slug):
            continue
        if entry.get('route') == 'living':
            trial.pull(slug)
            continue
        for name in NAMES:
            if image_frames(slug, name):
                continue
            text, images = trial.call('get_image', {'job_id': entry[name]})
            if 'status: completed' not in text:
                continue
            count = re.search(r'frames:\s*(\d+)', text)
            base = re.search(r'download:\s*(\S+)\?index=0', text)
            if count and base and int(count.group(1)) > len(images):
                images = [Image.open(io.BytesIO(pixellab.fetch_url('%s?index=%d' % (base.group(1), i))))
                          .convert('RGBA') for i in range(int(count.group(1)))]
            for i, im in enumerate(images):
                im.save(os.path.join(trial.RAW, '%s_%s_img_%02d.png' % (slug, name, i)))
            say(slug, name, 'landed', len(images), 'frames')


def _write(slug, folder, frames):
    d = os.path.join(PATRON, slug, folder)
    os.makedirs(d, exist_ok=True)
    for n in os.listdir(d):
        if n.endswith('.png'):
            os.remove(os.path.join(d, n))
    for i, f in enumerate(frames):
        f.save(os.path.join(d, '%s_%02d.png' % (folder, i)))


def ship(slugs):
    import patron_ship
    for slug in slugs:
        if not landed(slug):
            say(slug, 'not landed yet')
            continue
        if living(slug):
            subprocess.run([sys.executable, '-X', 'utf8', os.path.join(HERE, 'patron_ship.py'), slug,
                            '--only', 'walk,arrive,leave'], check=True)
            continue
        idle = shipped(slug, 'idle')[0]
        # the shipped walk and idle already passed the ink pass: lock without inking again
        walk = patron_motion.lock_palette(patron_motion.stabilise(walk_source(slug)), idle, ink=False)
        arrive = patron_ship.anchor_to(image_frames(slug, 'arrive'), idle, 'last')
        leave = patron_ship.anchor_to(image_frames(slug, 'leave'), idle, 'first')
        # both ends on the clips they join, the walk out drawn mirrored (patron_motion.bridge)
        arrive = patron_motion.bridge(arrive, walk[0], idle)
        leave = patron_motion.bridge(leave, idle, ImageOps.mirror(walk[0]))
        _write(slug, 'walk', walk)
        _write(slug, 'arrive', patron_motion.lock_palette(arrive, idle))
        _write(slug, 'leave', patron_motion.lock_palette(leave, idle))
        trial.log({'asset': 'patron/' + slug, 'event': 'reshipped', 'only': ['walk', 'arrive', 'leave'],
                   'route': 'image'})
        say(slug, 'shipped walk %d, arrive %d, leave %d' % (len(walk), len(arrive), len(leave)))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'pull'
    names = sys.argv[2:] or cast()
    {'queue': queue, 'pull': pull, 'ship': ship}[cmd](names)
