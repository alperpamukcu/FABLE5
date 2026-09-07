# -*- coding: utf-8 -*-
"""One PixelLab character -> a LAST CALL patron, frames and all (2026-08-19).

The cast that shipped before this was stood on its common foot line by a script that
lived in a session scratchpad and died with it, which is why the constants in TycoonHud
carry hand-measured HeadY values and no tool to re-derive them. This is that tool, kept
in the repo this time.

What a patron IS, measured off the shipped cast (bearded, bikeryoung, glam):

  * a 180x180 canvas, one sprite per frame, under Assets/Resources/Patron/<slug>/<clip>/
  * six clips - idle, order, drink, walk, cheer, upset - each an ordered <clip>_NN.png
  * the figure standing on a COMMON FOOT LINE at y 170, whatever its height
  * a face.png: the head, cropped square out of the idle frame, for the licence card
  * one row in TycoonHud's PatronCast: (slug, HeadY, Stars), where HeadY is the row the
    head starts on - the order ticket and the patience gauge hang off it, so it is a
    measurement and never a guess

PixelLab draws the character on its own canvas (160x160 for a v3 character) with the
figure wherever the pose puts it. This tool therefore RE-STANDS every frame: each frame
is measured for its own alpha bbox, then pasted onto the 180 canvas so that its feet sit
on the foot line and its horizontal centre sits on the canvas centre - EXCEPT that the
walk clip is aligned on the group's own median centre instead of per-frame, or the figure
moonwalks on the spot as each stride re-centres itself.

No scaling anywhere: a 160-tall drawing goes onto a 180 canvas as the same pixels, which
is the only way a character can stand beside hand-drawn bottles without changing density.

Usage:
    patron_gen.py fetch <character_id> <slug>    download + stand + ship the six clips
    patron_gen.py measure <slug>                 print the HeadY row for the cast table
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
PATRON = os.path.join(ROOT, 'Assets', 'Resources', 'Patron')
RAW = os.path.join(HERE, 'AssetPipeline', 'sources', 'patron_raw')
LOG = os.path.join(HERE, 'AssetPipeline', 'generation_log.jsonl')

sys.path.insert(0, HERE)
import pixellab  # noqa: E402

# The 2026-08-09 cast's rig. The 2026-08-19 cast is bigger and carries its own numbers
# (patron_prompts.RIG_CANVAS_PX / RIG_FOOT_Y); stand() takes them as arguments so one tool
# can ship both without either rig being a magic number in the other's code.
CANVAS = 180
FOOT_Y = 170          # the cast's common foot line, measured off the shipped frames
CLIPS = ('idle', 'order', 'drink', 'walk', 'cheer', 'upset')


def call_text(tool, args, timeout=600):
    _, body = pixellab.post({'jsonrpc': '2.0', 'id': 1, 'method': 'tools/call',
                             'params': {'name': tool, 'arguments': args}}, timeout=timeout)
    msgs = pixellab.sse(body)
    text, images = '', []
    for m in msgs:
        for c in ((m.get('result') or {}).get('content') or []):
            if c.get('type') == 'text':
                text += c['text'] + '\n'
            elif c.get('type') == 'image':
                images.append(Image.open(io.BytesIO(base64.b64decode(c['data']))).convert('RGBA'))
    return text, images


def bbox(im):
    px = im.load()
    w, h = im.size
    xs = [x for x in range(w) if any(px[x, y][3] >= 40 for y in range(h))]
    ys = [y for y in range(h) if any(px[x, y][3] >= 40 for x in range(w))]
    if not xs or not ys:
        return None
    return xs[0], ys[0], xs[-1] + 1, ys[-1] + 1


def stand(frames, lock_centre=False, canvas=CANVAS, foot_y=FOOT_Y, rigid=False):
    """Put a clip on the canvas with its feet on the foot line.

    lock_centre: use ONE horizontal centre for the whole clip (the median of the frames'
    own centres). A walk needs this - aligning each stride to its own bbox centre pins the
    figure in place and the legs scissor underneath it, which reads as moonwalking.

    rigid: use ONE transform for the WHOLE clip, horizontally AND vertically, so every
    frame keeps the position it was drawn in relative to its neighbours.
    THIS IS WHAT A MOVING CLIP NEEDS (2026-08-19, the author: "hareket esnasinda cok
    oynuyor, smooth bir yuruyus olmuyor"). Standing each frame on its own bbox bottom
    nails the FEET to one row and lets the head bob by however much the pose is shorter -
    measured at 12 to 16 pixels a frame on the first shipped walk, which is a limp, not a
    stride. A real walk keeps the head level and moves the feet; the drawing already does
    that, and per-frame standing was destroying it. The same applies to a head turn: the
    body must not slide sideways to keep a tilting head centred.

    Anything still: a single pose can be stood on its own bbox, because there is no
    neighbour for it to disagree with.
    """
    boxes = [bbox(f) for f in frames]
    good = [(f, b) for f, b in zip(frames, boxes) if b]
    if not good:
        return []
    centres = sorted((b[0] + b[2]) / 2.0 for _, b in good)
    locked = centres[len(centres) // 2]
    if rigid:
        bottoms = sorted(b[3] for _, b in good)
        dx = int(round(canvas / 2.0 - locked))
        dy = foot_y - bottoms[len(bottoms) // 2]
        out = []
        for f, _ in good:
            plate = Image.new('RGBA', (canvas, canvas), (0, 0, 0, 0))
            plate.paste(f, (dx, dy))
            out.append(plate)
        return out
    out = []
    for f, b in good:
        x0, y0, x1, y1 = b
        cx = locked if lock_centre else (x0 + x1) / 2.0
        plate = Image.new('RGBA', (canvas, canvas), (0, 0, 0, 0))
        plate.paste(f.crop(b),
                    (int(round(canvas / 2.0 - (cx - x0))), foot_y - (y1 - y0)))
        out.append(plate)
    return out


def ship(slug, clip, frames):
    d = os.path.join(PATRON, slug, clip)
    os.makedirs(d, exist_ok=True)
    for old in os.listdir(d):
        if old.endswith('.png'):
            os.remove(os.path.join(d, old))
    for i, f in enumerate(frames):
        f.save(os.path.join(d, '%s_%02d.png' % (clip, i)))
    print('  %-6s %d frames -> Resources/Patron/%s/%s/' % (clip, len(frames), slug, clip))


# THE PORTRAIT FRAME (2026-09-07, the author: "tum karakterlerin vesikaliklari omuz dahil omuz
# hizasindan olacak, farkli sekillerde olmamali"). One frame for the whole cast, specified the
# way a passport photograph is: a fixed size, the head a fixed fraction of it, the crown a fixed
# distance below the top edge. Everything else follows.
FACE_PX = 64            # every portrait, the same square - the licence window scales one number
FACE_HEAD_F = 0.52      # how much of the frame's height the head fills
FACE_CROWN_F = 0.10     # clear air above the crown


def head_span(im):
    """(crown row, chin row) of the figure in `im`, measured off its own silhouette.

    The crown is the topmost opaque row. The chin is where the silhouette stops being a head
    and starts being shoulders: scanning down, the first row whose width jumps past 1.6x the
    narrowest row under the crown - which is the neck. Falls back to a third of the figure,
    which is roughly where a head ends on any of these rigs.
    """
    w, h = im.size
    px = im.load()
    rows = []
    for y in range(h):
        n = sum(1 for x in range(w) if px[x, y][3] > 40)
        rows.append(n)
    opaque = [y for y, n in enumerate(rows) if n > 0]
    if not opaque:
        return 0, h // 3
    crown, bottom = opaque[0], opaque[-1]
    figure = bottom - crown

    # THE NECK, FOUND ON THE FIGURE'S OWN CENTRE LINE - not on the full silhouette width.
    # Measured 2026-09-07 and drawn over the frames to check: on the full width, the
    # narrowest row in the upper figure is the WAIST, because these rigs stand with the arms
    # hanging clear of the body, so the shoulders-and-arms span stays wide right down to the
    # hips. Every portrait cut that way took in the whole torso.
    #
    # A column the width of the head, centred on the head, sees only head-neck-shoulders: it
    # widens to the cheekbones, pinches at the neck, then fills edge to edge at the shoulders.
    # That pinch is the chin line, and it is what "omuz hizasindan" is measured from.
    head_band = list(range(crown, min(h, crown + max(6, int(figure * 0.22)))))
    if len(head_band) < 5:
        return crown, crown + max(6, figure // 5)
    widest = max(head_band, key=lambda y: rows[y])
    head_w = max(4, rows[widest])
    # the centre of the head, and a column just wider than it
    ys = [x for x in range(w) if px[x, widest][3] > 40]
    cx = (ys[0] + ys[-1]) // 2 if ys else w // 2
    half = max(3, int(head_w * 0.60))
    col = []
    for y in range(crown, min(h, crown + max(10, int(figure * 0.40)))):
        col.append(sum(1 for x in range(max(0, cx - half), min(w, cx + half + 1))
                       if px[x, y][3] > 40))
    # scan below the widest head row for the pinch
    start = widest - crown + 1
    tail = [(i, n) for i, n in enumerate(col) if i > start and n > 0]
    if not tail:
        return crown, crown + max(6, figure // 5)
    ni, nw = min(tail, key=lambda t: t[1])
    if nw > head_w * 0.85:
        # no pinch to find (a hood, a scarf, a collar to the jaw): take the proportion
        return crown, crown + max(6, figure // 5)
    return crown, crown + ni


def make_face(slug, idle0):
    """The licence portrait: ONE frame for every face, head-and-shoulders (2026-09-07).

    It used to size itself off the figure - side = shoulder_width * 0.78 - so the cast shipped
    crops from 42px (driftgirl) to 75px (idoljp), a 1.79x spread. Every one of them is then
    drawn into the SAME square window on the card with preserveAspect, so that spread became a
    magnification difference: the narrow crops came out as a head filling the frame and the wide
    ones as a distant head-and-chest. Sizing the frame by the subject is backwards; a portrait
    frame is fixed and the SUBJECT is placed in it, which is what this does.
    """
    from PIL import Image
    crown, chin = head_span(idle0)
    head_h = max(4, chin - crown)
    # The scale that makes this head FACE_HEAD_F of the frame - the one number that makes every
    # portrait the same portrait.
    scale = (FACE_PX * FACE_HEAD_F) / float(head_h)
    src_side = max(8, int(round(FACE_PX / scale)))

    b = bbox(idle0)
    cx = (b[0] + b[2]) // 2
    # The crown sits FACE_CROWN_F down the frame; in source pixels that is this far above it.
    top = int(round(crown - src_side * FACE_CROWN_F))
    left = cx - src_side // 2

    # Crop with the canvas padded rather than clamped, or a face near an edge is shifted off
    # its own centre line - which would be the old fault in a new place.
    pad = Image.new('RGBA', (idle0.width + src_side * 2, idle0.height + src_side * 2), (0, 0, 0, 0))
    pad.paste(idle0, (src_side, src_side))
    crop = pad.crop((left + src_side, top + src_side,
                     left + src_side + src_side, top + src_side + src_side))
    # NEAREST: these are pixel drawings, and a smooth resample of one is mud.
    crop = crop.resize((FACE_PX, FACE_PX), Image.NEAREST)
    crop.save(os.path.join(PATRON, slug, 'face.png'))
    print('  face   %dx%d head %d -> Resources/Patron/%s/face.png'
          % (FACE_PX, FACE_PX, head_h, slug))


def download_zip(character_id, url, timeout=600):
    """The character's own download endpoint - a zip of every rotation and animation.

    There is no per-animation getter on the server (get_character lists them, nothing
    hands back their frames), so this is the only way to the pixels. It answers HTTP 423
    while any job is still running, which is also the honest "are we there yet".
    """
    import urllib.request, ssl
    req = urllib.request.Request(url, headers={'Authorization': 'Bearer ' + pixellab._token()})
    with urllib.request.urlopen(req, timeout=timeout,
                                context=ssl.create_default_context()) as r:
        return r.read()


def frames_from_zip(blob):
    """{animation name -> [frames in order]} out of the download zip.

    Entries are grouped by their folder and ordered by the number in the file name, so
    a zip that grows a new animation needs no change here.
    """
    import zipfile, re, collections
    out = collections.defaultdict(list)
    with zipfile.ZipFile(io.BytesIO(blob)) as z:
        for n in z.namelist():
            if not n.lower().endswith('.png'):
                continue
            parts = [p for p in n.replace(chr(92), '/').split('/') if p]
            if len(parts) < 2:
                continue
            # .../animations/<clip>/<direction>/frame_NNN.png - the clip is the
            # folder ABOVE the direction, and the animation_name we asked for is
            # exactly what lands there. Reading parts[-2] took the DIRECTION and
            # collapsed all six clips into one bucket called "south".
            if 'animations' not in parts:
                continue
            group = parts[parts.index('animations') + 1]
            idx = re.findall(r'(\d+)', parts[-1])
            out[group].append((int(idx[-1]) if idx else 0, n))
    frames = {}
    with zipfile.ZipFile(io.BytesIO(blob)) as z:
        for group, items in out.items():
            items.sort()
            frames[group] = [Image.open(io.BytesIO(z.read(n))).convert('RGBA')
                             for _, n in items]
    return frames


def fetch(character_id, slug):
    os.makedirs(RAW, exist_ok=True)
    text, _ = call_text('get_character', {'character_id': character_id,
                                          'include_preview': False})
    if 'status: completed' not in text:
        print(text.splitlines()[0] if text else 'no answer')
        return
    if 'pending jobs' in text:
        for line in text.splitlines():
            if 'pending jobs' in line or '~' in line and 'custom' in line:
                print(' ', line.strip())
        print('  animations still running - run again when they land')
        return

    url = None
    for line in text.splitlines():
        if line.strip().startswith('download:'):
            url = line.split('download:', 1)[1].strip()
    if not url:
        print('  no download url in the character record')
        return

    print('character', character_id, '->', slug)
    blob = download_zip(character_id, url)
    open(os.path.join(RAW, slug + '.zip'), 'wb').write(blob)
    groups = frames_from_zip(blob)
    print('  zip carries:', ', '.join('%s(%d)' % (k, len(v)) for k, v in sorted(groups.items())))

    # The zip names an animation by its ACTION, not by our clip name, so each clip is
    # matched on a word only it uses. Anything unmatched is left alone rather than
    # guessed at - a mis-slotted clip is worse than a missing one.
    # The zip folders carry the animation_name we passed to animate_character, which
    # is the clip name itself - so the match is identity, and a rename on either side
    # shows up as a loud MISSING rather than a silently mis-slotted clip.
    MATCH = {c: c for c in CLIPS}
    idle0 = None
    for clip in CLIPS:
        key = next((k for k in groups if MATCH[clip] in k.lower()), None)
        if not key:
            print('  %-6s MISSING (no zip folder matched %r)' % (clip, MATCH[clip]))
            continue
        frames = stand(groups[key], lock_centre=(clip == 'walk'))
        if clip == 'idle' and frames:
            idle0 = frames[0]
        ship(slug, clip, frames)

    if idle0 is not None:
        make_face(slug, idle0)
        b = bbox(idle0)
        print('  cast row:  ("%s", %df, <stars>),' % (slug, b[1]))
    with io.open(LOG, 'a', encoding='utf-8') as f:
        f.write(json.dumps({'asset': 'patron/' + slug, 'event': 'shipped',
                            'character_id': character_id,
                            'ts': time.strftime('%Y-%m-%dT%H:%M:%S')}) + chr(10))


def measure(slug):
    d = os.path.join(PATRON, slug, 'idle')
    fs = sorted(f for f in os.listdir(d) if f.endswith('.png'))
    im = Image.open(os.path.join(d, fs[0])).convert('RGBA')
    b = bbox(im)
    print('%s: figure x %d..%d y %d..%d  -> HeadY %d' % (slug, b[0], b[2], b[1], b[3], b[1]))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'measure'
    if cmd == 'fetch':
        fetch(sys.argv[2], sys.argv[3])
    else:
        measure(sys.argv[2])
