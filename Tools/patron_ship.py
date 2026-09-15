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

# NOTHING IS FILTERED HERE ANY MORE (2026-08-20). For one day this file stripped the
# black keyline off two faces that came back inked, and the author threw the whole idea
# out - first where it damaged art he had approved, then outright: "hicbir karakterde
# siyah kontur olmamali ... dogal kontur olacak". He is right twice over. Removing a line
# is not free (it eats the drawing's own dark detail along with the ink), and an eroded
# edge is not the same thing as an edge that was drawn by a change of colour - which is
# what "dogal kontur" means and what the rest of the cast actually has.
#
# So the line is beaten where it is drawn, in patron_prompts.py and in the roll: the brief
# now forbids the keyline in words as well as in the `outline` parameter, and a face is
# rolled several times and the MEASURED best is adopted (patron_trial_gen.roll/adopt). The
# two that could not be re-rolled out of it were re-briefed instead - the tailoring words
# came off the waistcoat and the black came out of the leopard print - because both times
# the ink turned out to be something the description was asking for.

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
    # Two halves, joined at the junction frame, exactly as the one-shots are: a right step
    # and a left step make one cycle that ends where it began (2026-09-07). Falls back to a
    # single 'walk' group for the cast generated before the split.
    # The TEMPLATE walk first (2026-09-15): a skeleton cycle even by construction, which the two
    # halves never were. ship() steadies it and locks it to the idle's colours (patron_motion).
    'walk': (('walk_t', ['walk_a', 'walk_b'], 'walk'), True, None, False),
    'look_right': ('look_right', True, None, True),
    'look_left': ('look_left', True, None, True),
    'order': (['order_a', 'order_b'], True, None, True),
    'drink': (['drink_a', 'drink_b'], True, None, True),
    'cheer': (['cheer_a', 'cheer_b'], True, None, True),
    'upset': (['upset_a', 'upset_b'], True, None, True),
    # Arriving ends on the idle and leaving starts on it (Tools/patron_transitions.py), so each is
    # anchored by that end: 'last' stands the clip's LAST frame where the idle stands.
    'arrive': ('arrive', True, None, 'last'),
    'leave': ('leave', True, None, True),
}



def log(rec):
    rec['ts'] = time.strftime('%Y-%m-%dT%H:%M:%S')
    with io.open(LOG, 'a', encoding='utf-8') as f:
        f.write(json.dumps(rec, ensure_ascii=False) + '\n')


def anchor_to(frames, idle, which='first'):
    """Shift a whole clip so its FIRST (or LAST) frame stands where the idle frame stands.

    One offset for every frame, so nothing inside the clip moves relative to anything
    else - the clip keeps the motion it was drawn with and only its address changes.
    """
    a, b = patron_gen.bbox(frames[0] if which == 'first' else frames[-1]), patron_gen.bbox(idle)
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


def stand_all(groups, still, foot_y, quiet=False):
    """Every shipped folder stood on one foot line, in SHIP order, as {folder: frames}.

    The idle is stood first and the anchored clips are anchored to it, so a set stood on
    any foot line has the same joins as a set stood on any other.
    """
    stood_set, idle_frame = {}, None
    for folder, (source, lock, pick, anchor) in SHIP.items():
        if isinstance(source, tuple):
            # First source that exists wins - see the bootstrap note on 'idle'. An entry may
            # itself be a LIST of halves (the walk since 2026-09-07): it counts as existing
            # only when every half is in the zip.
            def has(n):
                if isinstance(n, list):
                    return all(groups.get(part) for part in n)
                return n == 'still' or groups.get(n)
            source = next((n for n in source if has(n)), source[-1])
        if source == 'still':
            frames = [still]
        elif isinstance(source, list):
            parts = [groups.get(name) for name in source]
            if any(part is None for part in parts):
                missing = [n for n, part in zip(source, parts) if part is None]
                if not quiet:
                    print('  %-12s MISSING %s' % (folder, ', '.join(missing)))
                continue
            frames = list(parts[0])
            if folder == 'walk':
                # A LOOP IS JOINED THE OTHER WAY ROUND (2026-09-15, the author: "tüm yürüyüşler
                # loop halinde değil"). Measured on 33 walks: half B's FIRST frame is a new
                # frame, a whole step on from A's last (4-7k px), and its LAST frame is A's
                # first pixel for pixel (0-8 px) - the end pose it was pinned to. Dropping B's
                # first frame cut a step out of the middle and kept the copy at the end, so
                # every cycle hitched on the same pose twice. Keep B's first, drop B's last.
                for part in parts[1:]:
                    frames += list(part[:-1])
            else:
                for part in parts[1:]:
                    frames += list(part[1:])          # the junction frame, once
        else:
            frames = groups.get(source)
        if frames and pick == 'last':
            frames = [frames[-1]]
        if frames and pick == 'first':
            frames = [frames[0]]
        if not frames:
            if not quiet:
                print('  %-11s MISSING' % folder)
            continue
        stood = patron_gen.stand(frames, lock_centre=lock, rigid=lock,
                                 canvas=CANVAS, foot_y=foot_y)
        if anchor and idle_frame is not None and stood:
            stood = anchor_to(stood, idle_frame, 'last' if anchor == 'last' else 'first')
        if not stood:
            continue
        stood_set[folder] = stood
        if folder == 'idle':
            idle_frame = stood[0]
    return stood_set


def crowned(frames):
    """True when any frame carries the figure on its top row: the head ran off the canvas."""
    for f in frames:
        alpha = f.split()[3].load()
        if any(alpha[x, 0] >= 40 for x in range(f.size[0])):
            return True
    return False


def clean(folder):
    if not os.path.isdir(folder):
        return
    for name in os.listdir(folder):
        if name.endswith('.png') or name.endswith('.png.meta'):
            os.remove(os.path.join(folder, name))


def ship(slug, only=None):
    """Stand a character's clips on the rig and write them under Resources/Patron/<slug>/.

    only: a set of folder names to write, leaving every other folder, the face and the cast
    record exactly as they are (2026-09-15: re-joining the walks must not re-ship the rest).
    The whole set is still STOOD, so the feet land on the same row the shipped set uses.
    """
    zip_path = os.path.join(trial.RAW, slug + '_anim.zip')
    still_path = os.path.join(trial.RAW, slug + '.png')
    # A face with no clips yet has no zip, and that is a normal state: it is shipped for
    # its idle alone so the clips have a pose to be generated from (see the note on 'idle').
    groups = (patron_gen.frames_from_zip(io.open(zip_path, 'rb').read())
              if os.path.exists(zip_path) else {})
    still = Image.open(still_path).convert('RGBA')
    print('%s (zip carries %s)' % (slug, ', '.join(sorted(groups))))

    # THE HEAD, NOT THE SHOES (2026-09-14). Standing every frame on row 210 of a 220 canvas
    # cropped anyone drawn taller than 210 px off the TOP - kstudent by 7 rows, atelier,
    # skater and teacherde on every clip - while the raw frames all had the whole head.
    # Everything below row 127 is behind the counter in play, so the side to give up is the
    # bottom: the whole set goes down one row at a time, ONE offset for every clip so no
    # join moves, until no frame of any clip touches the top row.
    foot_y = FOOT_Y
    stood_set = stand_all(groups, still, foot_y)
    while foot_y < CANVAS and any(crowned(frames) for frames in stood_set.values()):
        foot_y += 1
        stood_set = stand_all(groups, still, foot_y, quiet=True)
    if foot_y != FOOT_Y:
        print('  %-12s feet on row %d, not %d: the head ran off the top' % ('headroom', foot_y, FOOT_Y))

    # THE SIDE VIEW IN THE PERSON'S OWN COLOURS, THE WALK HELD STEADY (2026-09-15, see
    # patron_motion). The palette is the idle AFTER the ink pass - the idle is inked on disk below,
    # and a clip locked to the un-inked idle would carry colours the shipped idle no longer has.
    # Locked folders are inked inside lock_palette and skipped by the ink pass below.
    import patron_ink
    import patron_motion
    LOCKED = ('walk', 'arrive', 'leave')
    on_disk = os.path.join(PATRON, slug, 'idle', 'idle_00.png')
    if only and 'idle' not in only and os.path.exists(on_disk):
        # A PARTIAL SHIP JOINS THE IDLE THAT IS ON DISK. This pass re-derives the headroom over
        # every clip, new ones included, and may stand the set a few rows off the ship that wrote
        # the idle: bridging to the idle stood HERE left kstudent's arrive 4 px off the idle the
        # game actually plays (measured 2026-09-15). The shipped idle is already inked.
        stood_set['idle'] = [Image.open(on_disk).convert('RGBA')]
        idle_inked = stood_set['idle'][0]
    elif 'idle' in stood_set:
        idle_inked = stood_set['idle'][0].copy()
        patron_ink.reink(idle_inked)
    if 'idle' in stood_set:
        for folder in LOCKED:
            if folder in stood_set and (not only or folder in only):
                frames = stood_set[folder]
                if folder == 'walk':
                    frames = patron_motion.stabilise(frames)
                stood_set[folder] = patron_motion.lock_palette(frames, idle_inked)
        # BOTH ENDS OF A TRANSITION ON THE CLIPS THEY JOIN (patron_motion.bridge): arriving
        # starts on the walk's first frame and ends on the idle; leaving starts on the idle and
        # ends on the walk's first frame MIRRORED, which is how the game draws the walk out.
        walk0 = stood_set['walk'][0] if 'walk' in stood_set else None
        if walk0 is not None:
            from PIL import ImageOps
            if 'arrive' in stood_set:
                stood_set['arrive'] = patron_motion.bridge(stood_set['arrive'], walk0, idle_inked)
            if 'leave' in stood_set:
                stood_set['leave'] = patron_motion.bridge(stood_set['leave'], idle_inked,
                                                          ImageOps.mirror(walk0))

    head_y = None
    for folder, stood in stood_set.items():
        if only and folder not in only:
            continue
        out = os.path.join(PATRON, slug, folder)
        os.makedirs(out, exist_ok=True)
        clean(out)
        for i, f in enumerate(stood):
            f.save(os.path.join(out, '%s_%02d.png' % (folder, i)))
        print('  %-12s %2d frames' % (folder, len(stood)))
        if folder == 'idle':
            head_y = patron_gen.bbox(stood[0])[1]
            patron_gen.make_face(slug, stood[0])

    # NO BLACK KEYLINE, EVER (2026-09-07, the author: "kesinlikle siyah kontras olmamali,
    # natural kontras olmali"). The model draws one whatever the prompt says - measured
    # across three rounds of casting - so the fix is not another take, it is this pass: every
    # near-black pixel becomes the colour of its neighbours, darkened. Run here rather than
    # by hand because a shipped patron that skipped it is a black outline in the room, and
    # nobody sees it until the author does.
    inked = 0
    for folder in SHIP:
        if only and folder not in only:
            continue
        if folder in LOCKED:
            continue                      # inked before the palette lock, above
        d = os.path.join(PATRON, slug, folder)
        if not os.path.isdir(d):
            continue
        for name in sorted(os.listdir(d)):
            if not name.endswith('.png'):
                continue
            f = os.path.join(d, name)
            im = Image.open(f).convert('RGBA')
            n = patron_ink.reink(im)
            if n:
                im.save(f)
                inked += n
    if only:
        # A partial re-ship owns no face, no glance holds and no cast row: logging it as
        # 'shipped' would hand patron_join a record with no head row.
        print('  re-shipped only %s (feet on row %d)' % (', '.join(sorted(only)), foot_y))
        log({'asset': 'patron/' + slug, 'event': 'reshipped', 'only': sorted(only),
             'foot_y': foot_y})
        return
    face = os.path.join(PATRON, slug, 'face.png')
    if os.path.exists(face):
        im = Image.open(face).convert('RGBA')
        if patron_ink.reink(im):
            im.save(face)
    if inked:
        print('  %-12s re-inked %d black pixels' % ('ink', inked))

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
         'foot_y': foot_y, 'head_y': head_y, 'holds': holds})


if __name__ == '__main__':
    args = sys.argv[1:]
    only = None
    if '--only' in args:
        i = args.index('--only')
        only = set(args[i + 1].split(','))
        args = args[:i] + args[i + 2:]
    for slug in (args or list(trial.KEPT)):
        ship(slug, only)
