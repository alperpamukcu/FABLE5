# -*- coding: utf-8 -*-
"""The 2026-08-19 cast, from the trial zips into the game (Resources/Patron/<slug>/).

The trial is over: clubgirl and heavyset are the cast the author kept, and this is what
puts them where the game looks for them. It reads the zips patron_trial_gen.py already
downloaded rather than fetching again - the pixels are paid for, and a shipping step that
re-downloads is a shipping step that can ship something different from what was approved.

THE RIG, and why each number is what it is:

  canvas 220, foot line 210. The drawings arrive on a 220 canvas and the game draws that
  canvas at one art pixel per stage unit (TycoonHud.CharSize = 220 x StageToHud), so the
  canvas IS the rig - a frame standing anywhere else on it stands anywhere else in the
  room. Ten pixels of air under the feet keeps the shoe's own outline off the edge, the
  same allowance the 2026-08-09 rig used.

  Every frame is re-stood on that foot line, because PixelLab draws each frame wherever
  the pose puts it. The walk and the two glances are stood on the group's MEDIAN centre
  instead of per frame: aligning each frame to its own bbox pins the figure in place and
  the motion happens underneath it - a walk moonwalks, and a head turn slides the body
  sideways to keep the head centred, which is the opposite of a head turn.

  THE IDLE IS ONE FRAME. Not an oversight and not a stub: the author rejected two
  breathing idles ("nefes alis veris istemiyorum ... sabit durmali"), so a seated
  customer's idle is the still frame, and the small glances the game plays every few
  seconds come from the look clips. One frame in the folder is what makes the game hold
  perfectly still - PatronFrameIndex returns 0 for a one-frame clip.

Usage:
    patron_ship.py            ship every kept character
    patron_ship.py clubgirl   ship one
"""
import io
import json
import os
import sys
import time

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, HERE)
import patron_gen                  # noqa: E402
import patron_prompts as brief     # noqa: E402
import patron_trial_gen as trial   # noqa: E402

PATRON = os.path.join(ROOT, 'Assets', 'Resources', 'Patron')
LOG = os.path.join(HERE, 'AssetPipeline', 'generation_log.jsonl')

CANVAS = brief.RIG_CANVAS_PX
FOOT_Y = brief.RIG_FOOT_Y

# Which zip clip becomes which folder the game loads. The game asks for six clips
# (TycoonHud.PatronClip) and two of ours are new; what is missing is simply absent, and
# the loader drops a clip it cannot find rather than drawing a hole.
# folder -> (zip clip, rigid). RIGID means one transform for the whole clip so the frames
# keep the positions they were drawn in: every moving clip needs it, and a still frame
# cannot care. See patron_gen.stand for what per-frame standing did to the first walk.
# folder -> (zip clip, rigid, pick). RIGID means one transform for the whole clip so the
# frames keep the positions they were drawn in: every moving clip needs it, and a still
# frame cannot care. PICK takes a single frame out of a clip - 'last' for a clip that ends
# on the pose we want to keep.
#
# The idle is ONE FRAME and it is the end of seat_front: a customer who has sat down is
# facing us with their hands low in front, and the counter turns that into hands on the
# bar. Not a loop, because a seated person is not a metronome (the author, twice).
SHIP = {
    'idle': ('seat_front', False, 'last'),
    'walk': ('walk', True, None),
    'look_right': ('look_right', True, None),
    'look_left': ('look_left', True, None),
}


def log(rec):
    rec['ts'] = time.strftime('%Y-%m-%dT%H:%M:%S')
    with io.open(LOG, 'a', encoding='utf-8') as f:
        f.write(json.dumps(rec, ensure_ascii=False) + '\n')


def clean(folder):
    if not os.path.isdir(folder):
        return
    for name in os.listdir(folder):
        if name.endswith('.png') or name.endswith('.png.meta'):
            os.remove(os.path.join(folder, name))


def ship(slug):
    zip_path = os.path.join(trial.RAW, slug + '_anim.zip')
    still_path = os.path.join(trial.RAW, slug + '.png')
    if not os.path.exists(zip_path):
        print('  %s: no clip zip - run patron_trial_gen.py poll first' % slug)
        return
    groups = patron_gen.frames_from_zip(io.open(zip_path, 'rb').read())
    still = Image.open(still_path).convert('RGBA')
    print('%s (zip carries %s)' % (slug, ', '.join(sorted(groups))))

    head_y = None
    for folder, (source, lock, pick) in SHIP.items():
        frames = [still] if source == 'still' else groups.get(source)
        if frames and pick == 'last':
            frames = [frames[-1]]
        if not frames:
            print('  %-11s MISSING' % folder)
            continue
        stood = patron_gen.stand(frames, lock_centre=lock, rigid=lock,
                                 canvas=CANVAS, foot_y=FOOT_Y)
        out = os.path.join(PATRON, slug, folder)
        os.makedirs(out, exist_ok=True)
        clean(out)
        for i, f in enumerate(stood):
            f.save(os.path.join(out, '%s_%02d.png' % (folder, i)))
        print('  %-11s %2d frames' % (folder, len(stood)))
        if folder == 'idle':
            head_y = patron_gen.bbox(stood[0])[1]
            patron_gen.make_face(slug, stood[0])

    # The hold frame for each glance, measured off the shipped frames rather than the raw
    # ones so the number belongs to what the game will actually play.
    holds = {}
    for folder in ('look_right', 'look_left'):
        d = os.path.join(PATRON, slug, folder)
        if not os.path.isdir(d):
            continue
        fs = [Image.open(os.path.join(d, n)).convert('RGBA')
              for n in sorted(os.listdir(d)) if n.endswith('.png')]
        if fs:
            holds[folder] = trial.peak_frame(fs)

    print('  cast row:  ("%s", %sf, <stars>),   hold %s'
          % (slug, head_y, holds))
    log({'asset': 'patron/' + slug, 'event': 'shipped', 'rig': [CANVAS, FOOT_Y],
         'head_y': head_y, 'holds': holds})


if __name__ == '__main__':
    for slug in (sys.argv[1:] or list(trial.KEPT)):
        ship(slug)
