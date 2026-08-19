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
sys.path.insert(0, HERE)
import pixellab                    # noqa: E402
import patron_prompts as brief     # noqa: E402

STATE = os.path.join(HERE, 'patron_trial_state.json')
RAW = os.path.join(HERE, 'AssetPipeline', 'sources', 'patron_trial')
LOG = os.path.join(HERE, 'AssetPipeline', 'generation_log.jsonl')

SIZE = 220
VIEW = 'high top-down'     # the room is 30 degrees; of PixelLab's two, this is the near one
POSES = (('neutral', brief.NEUTRAL_POSE), ('bar', brief.BAR_POSE))


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
    for language in brief.LINE_LANGUAGES:
        for pose_name, pose in POSES:
            yield '%s_%s' % (language, pose_name), language, pose


def queue(only=None):
    state = load()
    for key, language, pose in jobs():
        if only and only not in key:
            continue
        if state.get(key, {}).get('character_id'):
            print('  %-20s already queued (%s)' % (key, state[key]['character_id'][:8]))
            continue
        text, _ = call('create_character', {
            'description': brief.character_prompt(brief.TRIAL_FIGURE, pose, language),
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


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'poll'
    if cmd == 'queue':
        queue(sys.argv[2] if len(sys.argv) > 2 else None)
    else:
        poll()
