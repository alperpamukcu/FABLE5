# -*- coding: utf-8 -*-
"""Keep PixelLab's 20-job window full until every card has its take.

submit_all fired thirty-six jobs at a server that runs twenty at once; the ones past the
window came back 'rate limit exceeded (20/20 jobs)' and were never queued. This loop
counts what is pending, tops the window up from the missing cards, polls, and repeats
until nothing is missing. Idempotent: a card with its take on disk is never resubmitted.

  py -3 -u Tools/v4_bottles/refill.py
"""
import io
import json
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.dirname(HERE))
import brief          # noqa: E402
import gen            # noqa: E402

WINDOW = 20
SEED = brief.SEEDS[0]


def take_path(cid):
    return os.path.join(gen.RAW, cid, 's%d.png' % SEED)


def main():
    t0 = time.time()
    while time.time() - t0 < 5400:
        st = gen._state()
        missing = [c for c in brief.CARDS if not os.path.exists(take_path(c))]
        if not missing:
            print('all %d takes on disk' % len(brief.CARDS)); return
        pending = {}
        for c in missing:
            j = st['jobs'].get('%s:%d' % (c, SEED), {}).get('job')
            if j:
                pending[c] = j
        room = WINDOW - len(pending)
        for c in [m for m in missing if m not in pending][:max(0, room)]:
            os.makedirs(os.path.join(gen.RAW, c), exist_ok=True)
            args = {'description': brief.build(c), 'width': brief.CANVAS['width'],
                    'height': brief.CANVAS['height'], 'no_background': True, 'seed': SEED}
            refs = gen.references(True)
            if refs:
                args['reference_images'] = json.dumps(refs)
            if os.path.exists(gen.ANCHOR):
                args['style_image_base64'] = gen._b64(gen.ANCHOR)
                args['style_copy'] = json.dumps(['color_palette', 'outline', 'detail', 'shading'])
            text, msgs = gen._call(brief.TOOL, args, timeout=300)
            jid = gen._job_id(text, msgs)
            st = gen._state()
            st['jobs']['%s:%d' % (c, SEED)] = {'job': jid, 'submit': text[:300]}
            gen._save(st)
            if jid:
                pending[c] = jid; print('  queued %-18s %s' % (c, jid[:8]))
            else:
                print('  !! %s: %s' % (c, text[:120].replace('\n', ' ')))
                if 'rate limit' in text.lower():
                    break
        for c, jid in list(pending.items()):
            text, msgs = gen._call('get_image', {'job_id': jid}, timeout=120)
            imgs = gen._images_from(text, msgs)
            if imgs:
                io.open(take_path(c), 'wb').write(imgs[0])
                print('  -> %-18s done (%.0fs)' % (c, time.time() - t0))
            elif 'failed' in text.lower():
                print('  !! %s failed: %s' % (c, text[:160].replace('\n', ' ')))
                st = gen._state(); st['jobs']['%s:%d' % (c, SEED)] = {'job': None, 'submit': 'failed'}; gen._save(st)
        time.sleep(15)
    print('refill timed out')


if __name__ == '__main__':
    main()
