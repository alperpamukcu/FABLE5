# -*- coding: utf-8 -*-
"""The 2026-08-19 cast trial: one neutral figure, three line languages, two poses.

The author asked to rebuild the customer spec from scratch and to CHOOSE the drawing
language before any animation exists ("farkli tarzlarda ilk basta animasyonsuz sadece
karakteri yaratalim ve iskeleti onun uzerinden kuralim"). So this queues six stills and
nothing else - no animation jobs, no clips - and the rig (canvas, foot line, wrist line,
HeadY, hand anchors) is measured off whichever of them the author picks.

Six, because two axes are being asked at once and they must not be confounded:
    3 line languages (inked / selective / lineless)  x  2 poses (neutral / at the bar)
The FIGURE is identical across all six, so the only difference the eye can find is the
one being chosen. Everything the six have in common is in patron_prompts.py, which is
the brief and the thing to edit; this file is only the errand.

Size 220px: the largest of the three scale candidates being compared in the room, so the
small ones can be previewed by exact nearest-neighbour reduction from a real drawing
rather than by generating three sets. Whatever the author picks is then generated AT ITS
OWN SIZE - the house's native-resolution law (14 art bible 11.A) is not suspended for a
comparison, it just does not apply to a preview that is labelled as one.

Usage:
    patron_trial_gen.py queue     queue the six (idempotent - already-queued ones skip)
    patron_trial_gen.py poll      report status, download whatever has landed
"""
import base64
import io
import json
import os
import sys
import time

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, HERE)
import pixellab                    # noqa: E402
import patron_prompts as brief     # noqa: E402

STATE = os.path.join(HERE, 'patron_trial_state.json')
RAW = os.path.join(HERE, 'AssetPipeline', 'sources', 'patron_trial')
LOG = os.path.join(HERE, 'AssetPipeline', 'generation_log.jsonl')

SIZE = brief.PIVOT_CANVAS_PX
VIEW = brief.PIVOT_VIEW    # eye level: the author looked at the top-down round and said no
POSES = (('neutral', brief.NEUTRAL_POSE), ('bar', brief.BAR_POSE))

# Round two (2026-08-19): the language and the ruler are settled, so the axis being asked
# about is WHO. One still per candidate customer, neutral pose only - create_character
# ignores pose text anyway (round one proved it), and the author asked for the standing
# pictures alone. Three generations, not six.
ROUND_TWO = True


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


def log(rec):
    rec['ts'] = time.strftime('%Y-%m-%dT%H:%M:%S')
    with io.open(LOG, 'a', encoding='utf-8') as f:
        f.write(json.dumps(rec, ensure_ascii=False) + '\n')


def jobs():
    if ROUND_TWO:
        for name, figure in brief.FIGURE_OPTIONS.items():
            yield name, brief.PIVOT_LANGUAGE, brief.NEUTRAL_POSE, figure
        return
    for language in brief.LINE_LANGUAGES:
        for pose_name, pose in POSES:
            yield '%s_%s' % (language, pose_name), language, pose, brief.TRIAL_FIGURE


def queue(only=None):
    state = load()
    for key, language, pose, figure in jobs():
        if only and only not in key:
            continue
        if state.get(key, {}).get('character_id'):
            print('  %-20s already queued (%s)' % (key, state[key]['character_id'][:8]))
            continue
        text, _ = call('create_character', {
            'description': brief.character_prompt(figure, pose, language),
            'name': 'trial %s' % key,
            'mode': 'v3',
            'size': SIZE,
            'view': VIEW,
            'outline': brief.outline_hint(language),
            'detail': 'medium detail',
        })
        # The answer names the id inside a ready-to-paste call - get_character(
        # character_id="...") - so the id is pulled as a UUID, not as "everything after
        # the colon", which swallowed the whole line the first time this ran.
        import re
        m = re.search(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}',
                      text, re.I)
        cid = m.group(0) if m else None
        if not cid:
            print('  %-20s NO ID:\n%s' % (key, text[:400]))
            continue
        state[key] = {'character_id': cid, 'language': language, 'size': SIZE}
        save(state)
        log({'asset': 'patron_trial/' + key, 'event': 'queued', 'character_id': cid,
             'size': SIZE, 'view': VIEW})
        print('  %-20s queued %s' % (key, cid))


def rotations_from_zip(blob):
    """The rotation stills, SOUTH FIRST - the view the rig is measured from.

    A trial character has no animations, so everything in the zip is a rotation; they
    are named by direction, and 'south' is the one that faces the camera. The rest are
    kept in file order behind it so the report can show the turn-around too.
    """
    import zipfile
    with zipfile.ZipFile(io.BytesIO(blob)) as z:
        names = [n for n in z.namelist() if n.lower().endswith('.png')]
        names.sort(key=lambda n: (0 if 'south' in n.lower().split('/')[-1] else 1, n))
        return [Image.open(io.BytesIO(z.read(n))).convert('RGBA') for n in names]


def poll():
    os.makedirs(RAW, exist_ok=True)
    state = load()
    for key in sorted(state):
        cid = state[key]['character_id']
        text, images = call('get_character', {'character_id': cid, 'include_preview': True})
        first = text.strip().splitlines()[0] if text.strip() else '(silent)'
        # 'status: completed', not 'completed' anywhere: the answer's own hint says
        # "...runs after creation completes", which made every still look finished.
        if 'status: completed' not in text:
            print('  %-20s %s' % (key, first))
            continue
        # The south-facing rotation is the one the rig is measured from; the preview
        # image the tool hands back IS that view, so no authenticated fetch is needed
        # for a look (the rotation URLs 403 without a Bearer header - see memory).
        # include_preview does not always put an image in the answer, and the rotation
        # URLs 403 without a Bearer header, so the character's own download zip is the
        # reliable way to the pixels - the same route patron_gen.py takes to the clips.
        if not images:
            url = None
            for line in text.splitlines():
                if line.strip().startswith('download:'):
                    url = line.split('download:', 1)[1].strip()
            if not url:
                print('  %-20s completed, but no download url' % key)
                continue
            import patron_gen
            blob = patron_gen.download_zip(cid, url)
            io.open(os.path.join(RAW, key + '.zip'), 'wb').write(blob)
            images = rotations_from_zip(blob)
        if images:
            p = os.path.join(RAW, key + '.png')
            images[0].save(p)
            print('  %-20s completed -> %s (%dx%d)'
                  % (key, os.path.basename(p), images[0].width, images[0].height))
            state[key]['png'] = os.path.relpath(p, HERE)
            save(state)
        else:
            print('  %-20s completed, nothing drawable in the answer' % key)


# ── animation, one clip at a time (2026-08-19, round three) ──────────────────
# The author kept two of the three candidates (clubgirl, heavyset) and asked to go
# through the clips IN ORDER rather than queueing a whole cast's worth at once, so this
# takes the clip name on the command line and does exactly that one.
#
# v3 custom mode, 16 frames - the tool's own ceiling and the answer to "animasyon
# uzunlugunu gercekcilik icin maksimumda tutalim". Cost is canvas x frames: a 220px
# canvas at 10 frames is ~8 generations per direction, so one clip for the two of them
# is ~16. That is the reason this queues one clip, looks, and only then queues the next.
#
# Direction is part of the clip, not a separate axis: the idle is watched from the front
# (south), and the walk-in crosses the room right to left, which is WEST.
KEPT = ('clubgirl', 'heavyset')

CLIPS = {
    # THERE IS NO IDLE CLIP. Two were generated and both rejected, the second with the
    # reason: "nefes alis veris istemiyorum ... sabit durmali". A looping clip cannot be
    # still, so the idle is the character's own standing rotation, held, and the small
    # looking-around comes from the glances (patron_prompts.IDLE_IS_STILL).
    #
    # EVERY CLIP IS STANDING (2026-08-19, the author: "karakter animasyonlari oturur
    # vaziyette olmayacak hep ayakta olacak"). Two seated experiments are behind this line
    # and both are worth remembering rather than repeating: a SIT that continues the walk
    # works and cannot be turned to face front (v3 follows the pose it is handed, and it
    # was handed a profile), and a front-facing seated pose that turned out to be a
    # standing figure with its hands clasped. Both are deleted.
    #
    # Every clip starts from 'idle' - the SHIPPED standing frame, not something that only
    # exists in a zip - so each one begins and ends in the pose the player is looking at
    # and nothing jumps at a clip change.
    'look_right': dict(directions=['south'], frames=8, start='idle'),
    'look_left':  dict(directions=['south'], frames=8, start='idle'),
    'order':      dict(directions=['south'], frames=8, start='idle'),
    'drink':      dict(directions=['south'], frames=8, start='idle'),
    'cheer':      dict(directions=['south'], frames=8, start='idle'),
    'upset':      dict(directions=['south'], frames=8, start='idle'),
    # SMALL STEPS (the author: "daha kucuk adimlarla"). The walking-10 template strides
    # like somebody late for a train and a template's stride length is not a parameter, so
    # the walk is a v3 custom - the only place the SIZE of a step can be asked for. West,
    # because the walk-in crosses the room right to left.
    'walk': dict(directions=['west'], frames=8),
}
FRAMES = 8

# RESTRAINT IS IN EVERY DESCRIPTION, because it has had to be twice already (the author:
# "cok abartili hareketler vermemeliler, normal insan gibi sakin az hareket etmeliler").
# A v3 clip left to itself performs; what stops it is naming the amount and naming what
# must not move, in the same breath as what must.
CALM = ('a small restrained movement, calm and natural, '
        'the body stays where it is, no exaggeration, no big gesture')

CUSTOM = {
    # NO OBJECTS AND NO PLACES IN THESE STRINGS. The tool says so in its own schema
    # ("focusing on the movement or pose only ... avoid environmental details like
    # locations or objects"), and the one description that ignored it - "resting on a bar
    # top" - came back as a failed generation for both characters, twice. The bar is not
    # drawn here anyway: what the game needs is the POSE, and the room supplies the bar.
    #
    # And every one of them is STANDING, arms down. The counter crosses the body at the
    # navel, so a standing figure and a seated one show the player the same chest,
    # shoulders and head - which is why the seated experiments cost something and bought
    # nothing.
    'look_right': ('turning only the head to the right, a small glance to the side, '
                   'then holding that look, the shoulders do not turn, '
                   'the arms stay down at the sides, ' + CALM),
    'look_left': ('turning only the head to the left, a small glance to the side, '
                  'then holding that look, the shoulders do not turn, '
                  'the arms stay down at the sides, ' + CALM),
    # Speaking, not performing: the mouth moves and the head shifts a little. A v3 order
    # clip left open waves its arms about, which is a man hailing a taxi, not a customer
    # saying what they want.
    'order': ('speaking a short sentence, the mouth moves and the head tilts a little, '
              'the arms stay down at the sides and do not gesture, ' + CALM),
    # ONE hand, and the other stays down: the first take brought both up to the mouth,
    # which is praying, not drinking. The hand is empty because the GAME draws the glass
    # into it - a drawn one would mean every customer drinks the same vessel whatever was
    # poured (patron_prompts, THE GLASS IS NEVER DRAWN).
    'drink': ('lifting one hand up to the mouth as if holding something small, '
              'tipping the head back a little to sip, then lowering that same hand back '
              'down to the side, the other arm stays down at the side and does not move, '
              + CALM),
    # A reaction from somebody at a bar: a nod and a small smile, not a cheer with the
    # arms in the air.
    'cheer': ('a pleased reaction, a small nod and a smile, the shoulders relax, '
              'the arms stay down at the sides, nothing is raised, ' + CALM),
    'upset': ('a displeased reaction, a small frown and a slight shake of the head, '
              'the arms stay down at the sides, nothing is raised, ' + CALM),
    'walk': ('walking forward with small short steps at a calm unhurried pace, '
             'the feet stay close to the ground, the arms swing very little, ' + CALM),
}


def start_frame(slug, clip, which='held'):
    """One clip's held frame, base64, as the starting pose for another.

    Read off the SHIPPED frames (Resources/Patron/<slug>/<clip>), not the raw zip: what
    the game plays is what the next clip should continue from, and the shipped frames are
    the ones stood on the rig. The held frame is the one furthest from the first - see
    peak_frame for why that is not simply the last one.
    """
    import base64
    d = os.path.join(ROOT, 'Assets', 'Resources', 'Patron', slug, clip)
    if not os.path.isdir(d):
        return None
    names = sorted(n for n in os.listdir(d) if n.endswith('.png'))
    if not names:
        return None
    frames = [Image.open(os.path.join(d, n)).convert('RGBA') for n in names]
    # 'held' is the pose a clip settles on (measured, see peak_frame); 'last' is where a
    # clip physically ENDS, which is what a continuation has to start from - the walk's
    # held frame is mid-stride, and sitting down out of mid-stride is a stumble.
    held = (frames[-1] if which == 'last'
            else frames[peak_frame(frames)] if len(frames) > 1 else frames[0])
    buf = io.BytesIO()
    held.save(buf, format='PNG')
    return base64.b64encode(buf.getvalue()).decode('ascii')


def animate(clip):
    """Queue one clip for every kept character. One clip at a time, on purpose: each is
    looked at before the next is paid for."""
    spec = CLIPS[clip]
    state = load()
    for name in KEPT:
        cid = state[name]['character_id']
        done = state[name].setdefault('clips', {})
        if clip in done:
            print('  %-10s %-11s already queued (%s)' % (name, clip, done[clip][:8]))
            continue
        args = {'character_id': cid, 'animation_name': clip,
                'directions': spec['directions']}
        if spec.get('template'):
            args.update(mode='template', template_animation_id=spec['template'],
                        ai_freedom=0)
        else:
            args.update(mode='v3', frame_count=spec.get('frames', FRAMES),
                        action_description=CUSTOM[clip])
            # Start from another clip's HELD frame rather than from the standing rotation,
            # so a seated customer's arms do not jump back to their sides every time the
            # clip changes. The frame is read off what has already shipped, which means a
            # re-ship with different art re-anchors these clips too.
            if spec.get('start'):
                src, which = (spec['start'] if isinstance(spec['start'], tuple)
                              else (spec['start'], 'held'))
                seed = start_frame(name, src, which)
                if seed is None:
                    print('  %-10s %-11s needs %s shipped first' % (name, clip, src))
                    continue
                args['custom_start_frame_base64'] = seed
        text, _ = call('animate_character', args)
        import re
        ids = re.findall(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}', text, re.I)
        job = next((i for i in ids if i != cid), None)
        if not job:
            print('  %-10s %-11s NO JOB: %s' % (name, clip, text[:220].replace(chr(10), ' ')))
            continue
        done[clip] = job
        save(state)
        log({'asset': 'patron_trial/' + name, 'event': 'animation queued',
             'character_id': cid, 'clip': clip, 'template': spec.get('template'),
             'start': spec.get('start'), 'directions': spec['directions']})
        print('  %-10s %-11s queued %s (%s)'
              % (name, clip, job, spec.get('template') or 'v3 custom'))


def peak_frame(frames):
    """Where a turn-and-hold clip should STOP, measured rather than chosen.

    The glance clips were asked to turn 45 degrees and hold, and only half of them do:
    clubgirl holds her look through the last three frames, while heavyset turns and comes
    BACK to the front by the end. A clip that returns cannot be held on its last frame, so
    the rig holds the frame that is furthest from frame 0 instead - the peak of the turn -
    and plays 0..peak forward when the neighbour arrives, peak..0 backward when they go.

    Distance is counted in changed pixels against frame 0, which for a head-only motion is
    exactly the amount of head that has turned. Measured 2026-08-19:
        clubgirl look_right 7, look_left 8 (both hold to the end)
        heavyset look_right 4, look_left  6 (both return, so both are trimmed)
    """
    from PIL import ImageChops
    base = frames[0].convert('RGB')
    best, best_i = -1, 0
    for i, f in enumerate(frames):
        d = ImageChops.difference(base, f.convert('RGB')).load()
        w, h = f.size
        n = sum(1 for y in range(h) for x in range(w) if sum(d[x, y]) > 30)
        if n > best:
            best, best_i = n, i
    return best_i


def peaks():
    """Print the hold frame for every glance clip that has landed."""
    import patron_gen
    for name in KEPT:
        blob = io.open(os.path.join(RAW, name + '_anim.zip'), 'rb').read()
        g = patron_gen.frames_from_zip(blob)
        for clip in ('look_right', 'look_left'):
            if clip in g:
                print('  %-10s %-11s %d kare -> tut %d'
                      % (name, clip, len(g[clip]), peak_frame(g[clip])))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'poll'
    if cmd == 'queue':
        queue(sys.argv[2] if len(sys.argv) > 2 else None)
    elif cmd == 'anim':
        animate(sys.argv[2])
    elif cmd == 'peaks':
        peaks()
    else:
        poll()
