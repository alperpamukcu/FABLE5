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
import patron_delineate            # noqa: E402
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
# The idle is ONE FRAME and it is the character's own south rotation - the standing pose,
# facing us. A seated pose was generated for it and then dropped at the author's word
# ("seated animasyonlarini kaldiralim kullanmayalim"), which costs nothing visually: the
# counter crosses the body at the navel, so what the player sees of a standing figure and
# a seated one is the same chest, shoulders and head. Not a loop either, because a seated
# person is not a metronome (the author, twice).
# folder -> (zip clip or clips, rigid, pick). RIGID means one transform for the whole clip
# so the frames keep the positions they were drawn in: every moving clip needs it, and a
# still frame cannot care. PICK takes a single frame out of a clip.
#
# A LIST OF CLIPS IS JOINED, in order, into one folder. The one-shots are generated in two
# halves - out to the middle of the action, then interpolated back to the idle pose - and
# what the game wants is the whole gesture in one place. The junction frame is dropped
# because half B was GENERATED from half A's last frame, so its first frame is that same
# pose: keeping both would hold it for two frames, which reads as a hitch exactly where
# the join is.
# ANCHOR says this clip begins on the idle pose, so its whole run is shifted until its
# first frame lands exactly where the idle frame stands. Without it the two are stood
# independently - each on its own median - and the same pose ends up in a different place
# on the canvas: measured at 7,000 to 11,000 differing pixels on heavyset, which is a jump
# every single time the game leaves the idle or comes back to it. The clips were generated
# FROM the idle frame, so this is not a fudge; it is restoring what the generator was told.
#
# The walk is not anchored: it is a cycle that never claims to start where the idle stands.
SHIP = {
    # THE IDLE IS A CLIP'S OWN FIRST FRAME, not the character's rotation. Every clip was
    # generated FROM the rotation, and the server redraws that seed rather than copying it:
    # heavyset's clips all open on the same figure, and it differs from his rotation by
    # 7,028 pixels - the shirt hangs differently and the arms sit differently. Shipping the
    # rotation as the idle would put that difference on screen every time a clip started or
    # ended. Taking frame 0 of order_a instead makes the idle the exact pose every clip
    # begins and ends on, and the joins vanish to single digits.
    # ('order_a' first, falling back to the rotation) - the bootstrap. A character with no
    # clips yet has only its rotation, and the clips cannot be generated until an idle is
    # shipped for them to start from; once order_a exists the idle is re-shipped from ITS
    # frame 0, which is the pose every clip actually opens on. So a new face is shipped
    # twice, and the second shipping is what makes the joins vanish.
    'idle': (('order_a', 'still'), False, 'first', False),
    'walk': ('walk', True, None, False),
    'look_right': ('look_right', True, None, True),
    'look_left': ('look_left', True, None, True),
    'order': (['order_a', 'order_b'], True, None, True),
    'drink': (['drink_a', 'drink_b'], True, None, True),
    'cheer': (['cheer_a', 'cheer_b'], True, None, True),
    'upset': (['upset_a', 'upset_b'], True, None, True),
}



def log(rec):
    rec['ts'] = time.strftime('%Y-%m-%dT%H:%M:%S')
    with io.open(LOG, 'a', encoding='utf-8') as f:
        f.write(json.dumps(rec, ensure_ascii=False) + '\n')


def anchor_to(frames, idle):
    """Shift a whole clip so its FIRST frame stands where the idle frame stands.

    One offset for every frame, so nothing inside the clip moves relative to anything
    else - the clip keeps the motion it was drawn with and only its address changes.
    """
    a, b = patron_gen.bbox(frames[0]), patron_gen.bbox(idle)
    if a is None or b is None:
        return frames
    dx, dy = b[0] - a[0], b[3] - a[3]
    if dx == 0 and dy == 0:
        return frames
    out = []
    for f in frames:
        plate = Image.new('RGBA', f.size, (0, 0, 0, 0))
        plate.paste(f, (dx, dy))
        out.append(plate)
    return out


def clean(folder):
    if not os.path.isdir(folder):
        return
    for name in os.listdir(folder):
        if name.endswith('.png') or name.endswith('.png.meta'):
            os.remove(os.path.join(folder, name))


def ship(slug):
    zip_path = os.path.join(trial.RAW, slug + '_anim.zip')
    still_path = os.path.join(trial.RAW, slug + '.png')
    # A face with no clips yet has no zip, and that is a normal state: it is shipped for
    # its idle alone so the clips have a pose to be generated from (see the note on 'idle').
    groups = (patron_gen.frames_from_zip(io.open(zip_path, 'rb').read())
              if os.path.exists(zip_path) else {})
    still = Image.open(still_path).convert('RGBA')
    print('%s (zip carries %s)' % (slug, ', '.join(sorted(groups))))

    head_y = None
    idle_frame = None
    for folder, (source, lock, pick, anchor) in SHIP.items():
        if isinstance(source, tuple):
            # first source that exists wins - see the bootstrap note on 'idle'
            source = next((n for n in source if n == 'still' or groups.get(n)), source[-1])
        if source == 'still':
            frames = [still]
        elif isinstance(source, list):
            parts = [groups.get(name) for name in source]
            if any(part is None for part in parts):
                missing = [n for n, part in zip(source, parts) if part is None]
                print('  %-12s MISSING %s' % (folder, ', '.join(missing)))
                continue
            frames = list(parts[0])
            for part in parts[1:]:
                frames += list(part[1:])          # the junction frame, once
        else:
            frames = groups.get(source)
        if frames and pick == 'last':
            frames = [frames[-1]]
        if frames and pick == 'first':
            frames = [frames[0]]
        if not frames:
            print('  %-11s MISSING' % folder)
            continue
        # THE KEYLINE COMES OFF HERE, on every frame of every clip. PixelLab will not
        # reliably draw a lineless figure - the same request came back at 3% and at 93% on
        # consecutive rolls - so the line is removed instead of re-rolled for, and it is
        # removed at SHIP time so the generated file stays as it came back. Idempotent: a
        # figure with no line is unchanged by it (patron_delineate).
        frames = [patron_delineate.delineate(f) for f in frames]
        stood = patron_gen.stand(frames, lock_centre=lock, rigid=lock,
                                 canvas=CANVAS, foot_y=FOOT_Y)
        if anchor and idle_frame is not None and stood:
            stood = anchor_to(stood, idle_frame)
        out = os.path.join(PATRON, slug, folder)
        os.makedirs(out, exist_ok=True)
        clean(out)
        for i, f in enumerate(stood):
            f.save(os.path.join(out, '%s_%02d.png' % (folder, i)))
        print('  %-12s %2d frames' % (folder, len(stood)))
        if folder == 'idle':
            head_y = patron_gen.bbox(stood[0])[1]
            idle_frame = stood[0]
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
