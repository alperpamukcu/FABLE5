# -*- coding: utf-8 -*-
"""The author's OWN PixelLab-site takes (2026-08-19) -> game-ready plates.

Third source family, after the generated v3 sets and the Nano Banana batch: the author
generated these two on PixelLab's website against their concept art and said "bu olacak,
oyuna ekle" - and, in the same breath, "yeni seyler uretme, sanati degistiriyorum, beraber
duzenleriz". So this tool INVENTS NOTHING: it does only the mechanical work that makes the
two files function as plates, and every step is an artifact of the generation canvas, not
an opinion about the art.

  room    the 640x360 take keeps its native pixels untouched. Its content rows (~49..306)
          sit in a 16:9 canvas with fully TRANSPARENT bands above and below - canvas
          letterbox, not design. Each band row is filled with its nearest content row,
          wholesale; rows that are only PARTLY transparent (the keyed window panes) are
          not touched, because those holes are the window plate's job.

  counter the 688x296 take arrives on an opaque near-WHITE card. The card is keyed by a
          border flood (interior whites - chrome, lit fridges - are unreachable from the
          border and survive), the true content (measured 659x201) is centre-cropped to
          640 wide - which is what makes it run edge to edge, the author's own ask - and
          the strip is cut 150 tall from the slab's back edge down. The base's last rows
          run off the bottom of the frame, which is where a bar's kickboard lives anyway.

After the room ships, `scene_nb_post.py post window` re-cuts the window plate to the new
room's own alpha hole - that recipe already derives the plate from the shipped file, so
it follows any room for free.

Sources are archived beside the pipeline in Tools/AssetPipeline/sources/pixellab_user/,
because a Downloads folder is not provenance.
"""
import io
import json
import os
import sys
import time

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SRC = os.path.join(HERE, 'AssetPipeline', 'sources', 'pixellab_user')
BACKGROUNDS = os.path.join(ROOT, 'Assets', 'Art', 'Backgrounds')
STAGE = os.path.join(HERE, 'AssetPipeline', 'staging', 'pixellab_user')
LOG = os.path.join(HERE, 'AssetPipeline', 'generation_log.jsonl')

ROOM_SRC = os.path.join(SRC, 'room_pixellab.png')
COUNTER_SRC = os.path.join(SRC, 'counter_pixellab.png')


def log(rec):
    rec['ts'] = time.strftime('%Y-%m-%dT%H:%M:%S')
    with io.open(LOG, 'a', encoding='utf-8') as f:
        f.write(json.dumps(rec, ensure_ascii=False) + '\n')


def save(im, path, key):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    im.save(path)
    os.makedirs(STAGE, exist_ok=True)
    im.save(os.path.join(STAGE, os.path.basename(path)))
    print('  -> %-46s %dx%d' % (os.path.relpath(path, ROOT), im.width, im.height))
    log({'asset': key, 'event': 'posted', 'source_batch': 'pixellab-site 2026-08-19',
         'path': os.path.relpath(path, ROOT), 'size': [im.width, im.height]})


ROOM_10X = os.path.join(SRC, 'room_pixellab_10x.png')


def post_room():
    """The room at ORIGINAL SIZE, uncropped, stood flush to the top of the frame.

    Three instructions landed on this plate in one sitting and the last one wins:
    "sigmiyor, taraflari cekilmis" (the first pass smeared 38 rows to fake a fit),
    "kenarlara gore buyut, kenar orani ayni kalsin" (a cover fit - which cost 78 drawing
    pixels off each side), and finally "bar arkaplanini kesme, orijinal boyutunda kullan.
    Eger yukaridan tasacaksa ekranda tassin". So: no smear, no scale, no crop.

    The pixels come from the author's 10x master by exact NEAREST /10 - the export is a
    true integer grid (900 sampled blocks, zero mismatches), so this is a lossless
    recovery of the drawing, and it carries 272 rows of room against the 258 the 640-wide
    file had. Those 272 stand flush against the top edge at 1:1.

    Nothing overflows at this size, and the author's permission for it to run off the top
    is kept anyway: if a future room is taller than the frame, it is anchored by its TOP
    and the surplus leaves the screen rather than being cropped or squeezed. What sits
    below the drawing is behind the counter - the counter's rest line puts its top edge at
    row 242 and the floor reaches 271, so the overlap is 29 rows and no gap can open.

    The trade, stated: at 1:1 the drawing is 2.35:1 against a 1.78:1 screen, so the room
    does not fill the frame by itself - the counter fills the rest. That is the author's
    choice between a cropped room and a whole one.
    """
    src = ROOM_10X if os.path.exists(ROOM_10X) else ROOM_SRC
    m = Image.open(src).convert('RGBA')
    W, H = m.size
    step = max(1, W // 640)
    if step > 1:
        m = m.resize((W // step, H // step), Image.NEAREST)   # exact, integer, lossless
        print('  master %dx%d -> native /%d' % (W, H, step))
    px = m.load()
    w, h = m.size
    rows = [y for y in range(h) if any(px[x, y][3] >= 200 for x in range(0, w, 4))]
    top, bot = rows[0], rows[-1]
    tall = bot - top + 1
    print('  content rows %d..%d (%d tall); flush to the top at 1:1, uncropped' % (top, bot, tall))

    out = Image.new('RGBA', (640, 360), (0, 0, 0, 0))
    out.paste(m.crop((0, top, w, bot + 1)), (0, 0))

    # Below the drawing, extend the floor by repeating its last row per column. It lives
    # behind the counter and can never be seen at tier 1; it is here so a taller counter
    # tier cannot open a hole under the bar.
    op = out.load()
    for x in range(640):
        srcy = next((y for y in range(tall - 1, -1, -1) if op[x, y][3] >= 200), None)
        if srcy is None:
            continue
        r, g, b, _ = op[x, srcy]
        for y in range(tall, 360):
            op[x, y] = (r, g, b, 255)
    save(out, os.path.join(BACKGROUNDS, 'club_room.png'), 'club_room')


def post_counter():
    im = Image.open(COUNTER_SRC).convert('RGBA')
    px = im.load()
    w, h = im.size

    # Key the white card by flooding from the border: interior whites (chrome, the lit
    # fridge glass) are unreachable from outside the drawing and survive untouched.
    def white(x, y):
        r, g, b, a = px[x, y]
        return a >= 200 and r >= 243 and g >= 243 and b >= 243

    seen = bytearray(w * h)
    stack = [(x, y) for x in range(w) for y in (0, h - 1) if white(x, y)]
    stack += [(x, y) for y in range(h) for x in (0, w - 1) if white(x, y)]
    for x, y in stack:
        seen[y * w + x] = 1
    keyed = 0
    while stack:
        x, y = stack.pop()
        px[x, y] = (0, 0, 0, 0)
        keyed += 1
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= nx < w and 0 <= ny < h and not seen[ny * w + nx] and white(nx, ny):
                seen[ny * w + nx] = 1
                stack.append((nx, ny))
    print('  white card keyed: %d px' % keyed)

    cols = [x for x in range(w) if any(px[x, y][3] >= 200 for y in range(0, h, 2))]
    rows = [y for y in range(h) if any(px[x, y][3] >= 200 for x in range(0, w, 2))]

    # THE STRIP STARTS AT THE SLAB, not at whatever the generator painted above it
    # (2026-08-19, the brutalist take): this source bakes a concrete wall behind the
    # counter, which SS6 forbids in the file - the room is the shell's job. The slab
    # is near-black against that wall, so its back edge is the first row the middle
    # half of the image goes decisively dark.
    def dark_share(y):
        n = d = 0
        for x in range(w // 4, 3 * w // 4, 2):
            r, g, b, a = px[x, y]
            if a >= 200:
                n += 1
                if max(r, g, b) < 100:
                    d += 1
        # thin stray rows lie: the slab is a MASS of dark, so the row must also be
        # substantially opaque across the middle half before its darkness counts.
        return (d / n, n) if n else (0.0, 0)
    span = len(range(w // 4, 3 * w // 4, 2))
    slab_top = next((y for y in range(rows[0], rows[-1])
                     if (lambda r: r[0] > 0.5 and r[1] > span * 0.5)(dark_share(y))),
                    rows[0])
    print('  slab back edge found at source row %d' % slab_top)

    cx = (cols[0] + cols[-1] + 1) // 2
    left = max(cols[0], min(cx - 320, cols[-1] - 639))
    body = im.crop((left, slab_top, left + 640, min(slab_top + 150, h)))
    strip = Image.new('RGBA', (640, 150), (0, 0, 0, 0))
    strip.paste(body, (0, 0))
    print('  content x %d..%d y %d.. -> cropped at x %d, slab top to row 0'
          % (cols[0], cols[-1], rows[0], left))
    save(strip, os.path.join(BACKGROUNDS, 'counter.png'), 'counter')


BACKBAR_SRC = os.path.join(SRC, 'backbar_pixellab.png')


def post_backbar():
    # "Backbar sahnesindeki arkaplan tamamen bu olacak" (2026-08-19). The 688x384 take
    # is cropped (24,0,664,360) to an exact 640x360 - the menu panel is 1280x720, so the
    # plate draws at a clean integer 2x. The crop drops the neighbouring-wall strip on
    # the left and 24 rows of the marble base; the three niche columns, all three shelf
    # boards and the downlights survive whole (geometry re-measured after the crop lives
    # in TycoonServiceFlow.Menu.cs beside the layout that uses it).
    im = Image.open(BACKBAR_SRC).convert('RGBA')
    assert im.size == (688, 384), im.size
    im = im.crop((24, 0, 664, 360))
    save(im, os.path.join(ROOT, 'Assets', 'Resources', 'Scene', 'backbar.png'), 'backbar')


if __name__ == '__main__':
    only = sys.argv[1:]
    for key, fn in (('room', post_room), ('counter', post_counter),
                    ('backbar', post_backbar)):
        if only and key not in only:
            continue
        print('[%s]' % key)
        fn()
