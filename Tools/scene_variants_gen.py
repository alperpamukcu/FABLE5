# -*- coding: utf-8 -*-
"""Versions of the shipped room and counter, drawn as ACTUAL PIXEL ART (2026-08-18).

The two plates in the game came out of the author's hand-made Nano Banana batch: painted
at a few thousand pixels wide and area-downscaled to 640, off-palette by decision. On
screen the author's verdict is "goruntu ve sanat kalitesi dusuk ve bulanik" - and it is,
by construction. An area-downscale averages four painted pixels into one, so nothing in
those plates has a hard edge; beside a 55-colour hand-pixelled bottle they read blurry
because they ARE blurry.

So this batch keeps the REFERENCE - camera angle, sizes, geometry - and changes the
production:

  * NATIVE RESOLUTION, NO RESAMPLE. PixelLab draws the room at 640x360 and the counter
    inside a 640x360 frame, which are the sizes the stage wants. Nothing is downscaled,
    so every pixel in the output is a pixel the model placed. This is the actual fix.
  * LABELLED REFERENCES, not adjectives. create_image_pro takes four references, each
    with a "usage" note, so the palette goes in as the Miami subset of the 55 drawn as a
    swatch, the geometry goes in as the shipped plate labelled COMPOSITION ONLY, and
    "crisp" goes in as a project sprite that already is. See `refs`.
  * THE COUNTER GETS A WIDE FRAME (640x160), not a 16:9 one cropped down - the v3 tool
    spent 60% of its pixel budget on air above the slab.
  * PALETTE SNAP ON, but MEASURED. The report prints what share of pixels the snap moved:
    near zero means the model drew inside the 55 and the snap was free, a big number means
    the author is choosing between a take's colours and 14 v3 SS3's law. `--no-quantize`
    to judge a take raw.
  * the prompts ask for hard edges, 1px outlines in each material's own darkest tone, and
    ORDERED DITHER for gradients - a dither is how a limited palette makes a gradient
    without going soft.

What it inherits unchanged from the reference plates:

  * the sizes are the stage's: the room is 640x360 (DiegeticStage.Reference), the counter
    is 640x150 of which only the top 130 rows are ever on screen.
  * the ship chain is scene_nb_post.ship(): BINARY alpha, no half-transparent rim.
    Imported rather than copied so the two tools cannot drift.
  * the window is KEYED, not painted: every room prompt asks for flat chroma green
    #00FF00 panes and `key_green` cuts them, so the sky plate stays derived from the
    room's own hole (14 v3 SS7) and cannot fall out of register.

NOTHING SHIPS FROM HERE. `post` writes to Tools/AssetPipeline/staging/scene_variants/ and
`report` builds preview.html there - the author picks, and only then is a take copied over
Assets/Art/Backgrounds/ (bottle-art-v3-respec's proof gate, applied to scenes).

One thing a picked counter costs: DiegeticStage.ShelfCentrePx stands a bought glass in each
of eight compartments, at x 40/110/182/260/326/400/482/580. `report` DRAWS those eight ticks
onto every counter take, so a front whose joinery would put a glass on a divider is caught
by looking, before the pick and not after.

Commands:  balance | swatch | queue [key...] | fetch | post [key...] | report
           measure [key...] | ship <room_key> <counter_key> | status
State:     Tools/scene_var_state.json      Raw: Tools/scene_var_raw/
Staged:    Tools/AssetPipeline/staging/scene_variants/
Log:       Tools/AssetPipeline/generation_log.jsonl (15 SS5)
"""
import base64, io, json, os, re, sys, time

import numpy as np
from PIL import Image

import pixellab
import scene_nb_post as nb

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
STATE = os.path.join(HERE, 'scene_var_state.json')
RAW = os.path.join(HERE, 'scene_var_raw')
STAGE = os.path.join(HERE, 'AssetPipeline', 'staging', 'scene_variants')
LOG = os.path.join(HERE, 'AssetPipeline', 'generation_log.jsonl')
BACKGROUNDS = os.path.join(ROOT, 'Assets', 'Art', 'Backgrounds')

# The stage's own numbers (DiegeticStage.cs). Where the counter's art top lands in the
# 640x360 frame is arithmetic off the rest line, not a guess.
REF_W, REF_H = 640, 360
COUNTER_H = 150
COUNTER_TOP_ROW = REF_H - (128 + 2)      # CounterRestY + CounterSurfaceInset, from the bottom
# DiegeticStage's own table, verbatim: where a bought glass stands on the bar front, and
# the band it stands in. Drawn onto each counter take rather than re-measured from it -
# a column-profile guess found ten "dividers" in an eight-bay front, and an overlay the
# author can LOOK at answers the real question (does cell 5 land on a divider?) outright.
SHELF_CENTRE_PX = [40, 110, 182, 260, 326, 400, 482, 580]
SHELF_CEIL_PX, SHELF_FLOOR_PX = 72, 124

# The production law of this batch, in the prompt itself. "no anti-aliasing" alone was not
# enough on the v3 takes; naming the dither is what stops the model reaching for a soft
# gradient, and naming the outline tone is what keeps edges from turning to mush.
CRISP = ('true pixel art at native resolution, every pixel placed deliberately, hard '
         'crisp pixel edges, clean 1-pixel outlines in each material\'s own darkest '
         'tone, flat shading with ordered 2x2 dithering for every gradient, strictly '
         'limited palette, no anti-aliasing, no blur, no soft gradients, no painterly '
         'brushwork, no photo texture, no text, no letters, no signage, no logos, '
         'no people')

# The shell every room take keeps: this is the plate in the game, DESCRIBED, so the takes
# are versions of it and not five different rooms. Same camera, same window hole, same
# three downlights, same empty floor waiting for the counter.
ROOM_SHELL = (
    'pixel art, EMPTY interior of a small city cocktail bar, seen straight on in '
    'one-point perspective with the vanishing point dead centre, no furniture, no '
    'bottles, no people, a wide empty floor filling the lower half, a tall '
    'steel-framed industrial window on the LEFT wall running floor to ceiling in '
    'perspective with flat chroma green #00FF00 glass panes, a flat ceiling with '
    'three small recessed downlights in a row across it, a slim pale cornice where '
    'wall meets ceiling and a matching skirting at the floor, parquet floor of '
    'square basketweave wood blocks receding to the back wall in clean straight '
    'pixel perspective lines')

# The shell every counter take keeps. EIGHT compartments is not decoration - ShelfCentrePx
# stands a bought glass in each one.
COUNTER_SHELL = (
    'pixel art, ONE isolated bar counter seen straight on at eye level, running the '
    'FULL width of the image from edge to edge, on a plain transparent background, '
    'NO wall and NO room behind it, nothing standing on it, no bottles and no '
    'glasses - a thick stone slab top with a dead-straight level front edge and a '
    'bright 1px highlight along its nose, and below it a continuous run of EIGHT '
    'compartments across the front: two panelled cabinet doors on the left with slim '
    'horizontal bar handles, three glass-fronted fridge doors with a pale diagonal '
    'reflection line and cool-lit shelves behind the glass, and three open shelf bays')

# -- the references every call carries ---------------------------------------
# create_image_pro takes up to FOUR LABELLED reference images, and the label is the whole
# point: each entry's "usage" tells the model what to take from that image, so three cheap
# PNGs do what no amount of adjective can. This is the lever the first pass missed - it
# described the palette in words and hoped.
#
#   palette  the Miami subset of the 55 (palette_miami.png, `swatch` builds it). The author
#            shared outdoor vice references FOR COLOUR ONLY - "sadece renk ve palet icin,
#            Miami renk tonlarinda" - and a swatch is that instruction in the one form a
#            model cannot misread. Lime, Graphite and Brick are left out: they are the drab
#            greys that make the shipped plate read dead next to those references.
#   layout   the plate in the game, labelled COMPOSITION ONLY - it is the blurry thing
#            being replaced, so its softness and its 40,859 colours must not ride along.
#   style    a project sprite that is genuinely pixel art (41 colours, 69% flat runs).
#            What "crisp" means here, SHOWN instead of asked for.
PALETTE_SWATCH = os.path.join(HERE, 'palette_miami.png')
STYLE_REF = os.path.join(ROOT, 'Assets', 'Resources', 'Items', 'v3_bourbon_redline_flat.png')
LAYOUT_REF = {'room': os.path.join(BACKGROUNDS, 'club_room.png'),
              'counter': os.path.join(BACKGROUNDS, 'counter.png')}

LAYOUT_USAGE = {
    'room': ('composition ONLY: the camera angle, the one-point perspective, where the '
             'window and the three ceiling lights sit. Do NOT copy its colours, its soft '
             'blurry shading or its muted grey-mauve tone'),
    'counter': ('geometry ONLY: the slab top, its height in frame, and the run of eight '
                'compartments across the front with their widths. Do NOT copy its colours '
                'or its soft blurry shading'),
}


def b64file(path):
    with io.open(path, 'rb') as f:
        return base64.b64encode(f.read()).decode('ascii')


def refs(family):
    """The labelled reference array for one take, as create_image_pro wants it (a JSON
    string). Sent as base64 rather than url because this client POSTs the payload itself -
    the schema's "prefer url" warning is about MCP clients truncating inline data."""
    out = [{'base64': b64file(PALETTE_SWATCH),
            'usage': 'colour palette: use ONLY these colours, Miami sunset tones'},
           {'base64': b64file(LAYOUT_REF[family]), 'usage': LAYOUT_USAGE[family]},
           {'base64': b64file(STYLE_REF),
            'usage': ('pixel art rendering style: hard 1px edges, large flat colour runs, '
                      'ordered dithering, no blur')}]
    return json.dumps(out)


# The counter is drawn in a WIDE SHORT frame, not a 640x360 one. The v3 tool drew counters
# inside a 16:9 canvas and cropped 150 rows out of it, which spends 60% of the model's
# pixel budget on empty air above the slab and leaves where the slab lands to chance.
# 640x160 is the plate plus ten rows of slack for the crop to find.
COUNTER_FRAME = (640, 160)

ASSETS = {
    # -- the room, three takes -------------------------------------------------
    'room_sunset': dict(kind='image', tool='create_image_pro', seed=41801, post='room',
        family='room', label='Miami gun batimi',
        note='Ayni beton kutu, Miami altin saatinde: pencereden giren amber ve magenta '
             'krem duvari yikiyor, parke sicak, uc spot havuzunu koyuyor. Referans '
             'gorsellerin tonu en dogrudan burada.',
        args=dict(width=REF_W, height=REF_H, no_background=False, description=(
            ROOM_SHELL + ', warm cream plaster walls #F2E8D5 shaded #C9BCA8, washed with '
            'hot magenta #E84DA6 and #FF7DC6 sunset light on the window side and amber '
            '#E8A33D deeper in, the right wall deep plum #362447 catching a magenta rim, '
            'warm oak parquet #8F5A1E with #4A2E14 seams and long dithered magenta and '
            'amber reflections, three amber #F5C97B downlight pools, ' + CRISP))),
    'room_neon': dict(kind='image', tool='create_image_pro', seed=41802, post='room',
        family='room', label='Neon gece',
        note='Ayni kutu karanlikta: duvarlar mor-laciverte cekiliyor, sokaktan magenta ve '
             'camdan cyan giriyor, spotlar odayi tek basina tutuyor. En atmosferik take.',
        args=dict(width=REF_W, height=REF_H, no_background=False, description=(
            ROOM_SHELL + ', deep purple-blue walls #241830 with #362447 panel seams, hot '
            'magenta #C23283 street light spilling through the window across the floor and '
            'up the left wall with a hard ordered-dither edge, cyan #26918F rim light '
            'along the window frame and cornice, three tight amber #E8A33D downlight pools '
            'the only warm light, near-black parquet #0D0813 with long dithered magenta '
            '#8F2464 and cyan #1B5F66 reflections, ' + CRISP))),
    'room_teal': dict(kind='image', tool='create_image_pro', seed=41803, post='room',
        family='room', label='Turkuaz oglen',
        note='Gunduz ve serin: turkuaz gun isigi, krem beton, gogus hizasinda petrol '
             'yesili lambri ve ince pirinc hat. Sakin secenek; tezgahla tek yapi okunuyor.',
        args=dict(width=REF_W, height=REF_H, no_background=False, description=(
            ROOM_SHELL + ', cream concrete panels #C9BCA8 with #9C8F80 seams above a deep '
            'petrol green panelled wainscot #123B45 with #1B5F66 panel faces running '
            'around the room at chest height, a thin brass trim line #C9822B along its '
            'top, bright cyan #3BC8BE daylight flooding in from the window with a dithered '
            'falloff, three warm amber #E8A33D downlight pools, malt parquet #6B4416 with '
            '#3A2410 seams, cream #F2E8D5 cornice, ' + CRISP))),
    # -- the counter, three takes ----------------------------------------------
    'counter_marble': dict(kind='image', tool='create_image_pro', seed=41811, post='counter',
        family='counter', label='Mermer & petrol',
        note='Oyundaki tezgahin kendisi, keskin cizilmis ve Miami tonuna cekilmis: krem '
             'mermer tabla, petrol yesili dograma, pirinc kulp, cyan isikli dolaplar.',
        args=dict(width=COUNTER_FRAME[0], height=COUNTER_FRAME[1], no_background=True,
                  description=(
            COUNTER_SHELL + ' - cream marble slab top #C9BCA8 with sparse thin #9C8F80 '
            'veins and an #F2E8D5 polished nose, deep petrol green cabinet frame #123B45 '
            'with #1B5F66 panel faces, brass handles and a thin brass reveal line #C9822B, '
            'fridge glass tinted #26918F with #7DF0E3 reflection lines and #3BC8BE lit '
            'shelves behind it, ' + CRISP))),
    'counter_noir': dict(kind='image', tool='create_image_pro', seed=41812, post='counter',
        family='counter', label='Gece mermeri & ceviz',
        note='Koyu tabla, sicak dolap: erik-siyahi mermer altin damarli, ceviz dograma, '
             'amber isikli dolaplar. Ustune konan acik bardak kontrasti tasiyor.',
        args=dict(width=COUNTER_FRAME[0], height=COUNTER_FRAME[1], no_background=True,
                  description=(
            COUNTER_SHELL + ' - near-black plum marble slab top #1A1023 with thin gold '
            '#C9822B veins and a bright #E8A33D polished nose, warm walnut cabinet frame '
            '#4A2E14 with #3A2410 panel faces, aged brass handles #E8A33D, fridge glass '
            'tinted #6B4416 with #E6B959 lit shelves behind it, ' + CRISP))),
    'counter_neon': dict(kind='image', tool='create_image_pro', seed=41813, post='counter',
        family='counter', label='Laciverte neon',
        note='Bar cephesinin kendisi isik kaynagi: laciverte dograma, alt kenarda magenta '
             'neon sizmasi, krom hirdavat, cyan camlar. Vitrin gibi duruyor.',
        args=dict(width=COUNTER_FRAME[0], height=COUNTER_FRAME[1], no_background=True,
                  description=(
            COUNTER_SHELL + ' - pale cream marble slab top #F2E8D5 shaded #C9BCA8 with a '
            'magenta #FF7DC6 reflected glow along its underside, deep club blue cabinet '
            'frame #1F2E66 with #2E4699 panel faces, a hot magenta #E84DA6 neon strip '
            'glowing under the slab and dithered magenta spill down the panels, chrome '
            'handles #F2E8D5 with #9C8F80 shadow, fridge glass tinted #123B45 with '
            '#7DF0E3 reflection lines, ' + CRISP))),
}

UUID = re.compile(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}')


def load():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def save(s):
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(s, indent=1))


def log(rec):
    rec['ts'] = time.strftime('%Y-%m-%dT%H:%M:%S')
    with io.open(LOG, 'a', encoding='utf-8') as f:
        f.write(json.dumps(rec, ensure_ascii=False) + '\n')


def texts(msgs):
    out = []
    for m in msgs:
        for c in ((m.get('result') or {}).get('content') or []):
            if c.get('type') == 'text':
                out.append(c['text'])
    return '\n'.join(out)


def images(msgs):
    out = []
    for m in msgs:
        for c in ((m.get('result') or {}).get('content') or []):
            if c.get('type') == 'image':
                out.append(Image.open(io.BytesIO(base64.b64decode(c['data']))).convert('RGBA'))
    return out


# -- queue / fetch -----------------------------------------------------------

def queue(only=None):
    st = load()
    for key, a in ASSETS.items():
        if only and key not in only:
            continue
        if st.get(key, {}).get('id'):
            print('%-16s already queued -> %s' % (key, st[key]['id'][:8]))
            continue
        args = dict(a['args'], seed=a['seed'], reference_images=refs(a['family']))
        msgs = pixellab.call(a['tool'], args, timeout=900)
        body = texts(msgs)
        m = UUID.search(body)
        st[key] = {'id': m.group(0) if m else None, 'kind': a['kind']}
        save(st)
        log({'asset': key, 'batch': 'scene-variants 2026-08-18', 'tool': a['tool'],
             'seed': a['seed'], 'prompt': a['args']['description'],
             'refs': ['palette_miami', 'layout:' + a['family'], 'style:v3_bourbon'],
             'size': [a['args']['width'], a['args']['height']], 'job': st[key]['id'],
             'event': 'queued' if m else 'queue-failed', 'raw': body[:300]})
        print('%-16s -> %s' % (key, st[key]['id'] or body[:120].replace('\n', ' ')))
        time.sleep(0.6)


def fetch():
    os.makedirs(RAW, exist_ok=True)
    st = load()

    def pending():
        return {k: v for k, v in st.items() if v.get('id')
                and not os.path.exists(os.path.join(RAW, k + '.png'))}

    for _ in range(80):
        if not pending():
            break
        moved = False
        for key, rec in sorted(pending().items()):
            msgs = pixellab.call('get_image', {'job_id': rec['id']}, timeout=300)
            ims, body = images(msgs), texts(msgs)
            if ims:
                ims[0].save(os.path.join(RAW, key + '.png'))
                print('fetched %-16s %dx%d' % (key, ims[0].width, ims[0].height))
                log({'asset': key, 'event': 'fetched'})
                moved = True
            elif 'failed' in body.lower():
                print('FAILED', key, body[:200].replace('\n', ' '))
                log({'asset': key, 'event': 'failed', 'raw': body[:300]})
                rec['id'] = None
                save(st)
                moved = True
        if pending() and not moved:
            print(' %d pending...' % len(pending()))
            time.sleep(25)
    print('missing:', sorted(pending()) if pending() else 'none')


# -- post --------------------------------------------------------------------

def key_green(im):
    """Cut the chroma green window panes. Vectorised twin of scene_v3_gen.key_green,
    same test: green clearly dominant over both other channels."""
    a = np.asarray(im.convert('RGBA')).copy()
    r, g, b = (a[:, :, i].astype(np.int16) for i in range(3))
    m = ((a[:, :, 3] > 0) & (g > 150) & (r < 110) & (b < 110)
         & (g - np.maximum(r, b) > 55))
    a[m] = (0, 0, 0, 0)
    return Image.fromarray(a, 'RGBA'), int(m.sum())


def content_top(im, cover=0.6):
    """First row that is SUBSTANTIALLY opaque, not merely non-empty.

    `.any(1)` was the first version and counter_marble is why it is not: that take drew a
    couple of stray pixels of back edge thirty rows above its slab, so the crop began on
    them and shipped a band of near-transparent nothing where the marble should have been.
    A counter strip spans the full width; a row with less than 60% coverage is not it."""
    op = (np.asarray(im)[:, :, 3] >= 128).sum(axis=1)
    rows = np.where(op >= cover * im.width)[0]
    return int(rows.min()) if len(rows) else 0


def post(only=None):
    """Key, ship, and shape. NO RESAMPLE anywhere on the happy path - the model was asked
    for the stage's own size, and a resize here would put back exactly the softness this
    batch exists to remove. The two resizes below are guards that fire only if PixelLab
    hands back something off-size, and they print when they do."""
    os.makedirs(STAGE, exist_ok=True)
    for key, a in ASSETS.items():
        if only and key not in only:
            continue
        src = os.path.join(RAW, key + '.png')
        if not os.path.exists(src):
            continue
        im = Image.open(src).convert('RGBA')
        im, cut = key_green(im)
        if a['post'] == 'room':
            print('  %-16s panes keyed: %d px (%.1f%%)'
                  % (key, cut, 100.0 * cut / (im.width * im.height)))
            if (im.width, im.height) != (REF_W, REF_H):
                print('    OFF-SIZE %dx%d -> resampling to %dx%d (softness warning)'
                      % (im.width, im.height, REF_W, REF_H))
                im = nb.crop_aspect(im, REF_W / float(REF_H)).resize((REF_W, REF_H), Image.BOX)
        else:
            im = nb.ship(im)                      # binary alpha BEFORE the row scan
            if im.width != REF_W:
                print('    OFF-SIZE width %d -> resampling to %d (softness warning)'
                      % (im.width, REF_W))
                im = im.resize((REF_W, round(im.height * REF_W / im.width)), Image.BOX)
            top = content_top(im)
            strip = Image.new('RGBA', (REF_W, COUNTER_H), (0, 0, 0, 0))
            strip.paste(im.crop((0, top, REF_W, min(top + COUNTER_H, im.height))), (0, 0))
            print('  %-16s slab top row %d, cropped to %dx%d' % (key, top, REF_W, COUNTER_H))
            im = strip
        out = nb.ship(im)
        out.save(os.path.join(STAGE, key + '.png'))
        print('  staged %-16s %dx%d  colours=%d'
              % (key, out.width, out.height, len(set(out.convert('RGB').getdata()))))
        log({'asset': key, 'event': 'staged', 'batch': 'scene-variants 2026-08-18',
             'quantized': nb.QUANTIZE, 'size': [out.width, out.height]})


# -- the preview -------------------------------------------------------------

def sky(hole):
    """scene_nb_post's derived sky, as an image instead of a shipped file - so a preview
    shows what the keyed panes actually look out on."""
    h, w = hole.shape
    rows = np.where(hole.any(1))[0]
    if not len(rows):
        return None
    ys = np.arange(h)[:, None].repeat(w, 1)
    t = (ys - rows.min()) / max(1, rows.max() - rows.min())
    bayer = (np.indices((h, w)).sum(0) % 2) * 0.5
    t = np.clip(t + (bayer - 0.25) * 0.14, 0, 1)
    out = np.zeros((h, w, 4), np.uint8)
    for c in range(3):
        out[:, :, c] = (nb.SKY_TOP[c] + (nb.SKY_LOW[c] - nb.SKY_TOP[c]) * t).astype(np.uint8)
    out[:, :, 3] = np.where(hole, 255, 0)
    out[~hole] = (0, 0, 0, 0)
    return Image.fromarray(out, 'RGBA')


def composite(room, counter):
    """The two plates as DiegeticStage stands them: sky behind the room's own hole, the
    counter hung from the rest line so only its top 130 rows show."""
    base = Image.new('RGBA', (REF_W, REF_H), (0, 0, 0, 255))
    s = sky(np.asarray(room)[:, :, 3] < 128)
    if s is not None:
        base.alpha_composite(s)
    base.alpha_composite(room)
    if counter is not None:
        visible = REF_H - COUNTER_TOP_ROW
        base.alpha_composite(counter.crop((0, 0, REF_W, min(visible, counter.height))),
                             (0, COUNTER_TOP_ROW))
    return base


def zoom(im, box, factor=3):
    """A nearest-neighbour blow-up of one detail, which is the only honest way to show
    whether a plate is crisp: at 1x a blurry plate and a pixelled one both look fine."""
    crop = im.crop(box)
    return crop.resize((crop.width * factor, crop.height * factor), Image.NEAREST)


def cells(counter):
    """ShelfCentrePx drawn ONTO the take: eight ticks where a bought glass stands, between
    the shelf board and the shelf floor.

    This replaces measuring the take's joinery. The measurement was tried first - a dark
    column profile over the cabinet band - and it found ten seams in the shipped front's
    eight bays, because an open bay's interior is dark too. A number that is wrong three
    times out of ten is worse than no number; the overlay answers the only question that
    matters (is every tick standing inside an opening, or is one on a divider?) by eye,
    and it cannot be wrong about where the code will put the glass."""
    from PIL import ImageDraw
    im = counter.copy()
    d = ImageDraw.Draw(im)
    for x in SHELF_CENTRE_PX:
        d.line([(x, SHELF_CEIL_PX), (x, SHELF_FLOOR_PX)], fill=(232, 77, 166, 255))
        d.line([(x - 3, SHELF_FLOOR_PX), (x + 3, SHELF_FLOOR_PX)], fill=(232, 77, 166, 255))
    d.line([(0, SHELF_CEIL_PX), (REF_W - 1, SHELF_CEIL_PX)], fill=(59, 200, 190, 128))
    d.line([(0, SHELF_FLOOR_PX), (REF_W - 1, SHELF_FLOOR_PX)], fill=(59, 200, 190, 128))
    return im


def colours(im):
    return len(im.convert('RGB').getcolors(maxcolors=1 << 24) or [])


def flatness(im):
    """Share of horizontally adjacent opaque pixel pairs that are IDENTICAL - the crispness
    number, and the one this batch is judged on.

    An earlier pass measured "% off-palette" instead and it was USELESS, in a way worth
    recording: the project's own v3 bottle - unmistakable pixel art, 41 colours - is also
    100% off the 55, because the flat era re-took the bottles without the snap. So off-
    palette measures palette compliance and says nothing about blur.

    Flat runs do separate them, cleanly, because it is a measure of the actual defect: an
    area-downscale averages neighbouring pixels and almost never lands on two identical
    ones, while pixel art is BUILT from flat runs. Measured on the shipped art: the room
    9.9%, its counter 36.5%, against 69.0% for the v3 bottle and 53.1% for the shaker."""
    a = np.asarray(im.convert('RGBA'))
    rgb, al = a[:, :, :3].astype(np.int16), a[:, :, 3]
    both = (al[:, :-1] >= 128) & (al[:, 1:] >= 128)
    if not both.any():
        return 0.0
    same = (rgb[:, :-1, :] == rgb[:, 1:, :]).all(2) & both
    return 100.0 * same.sum() / both.sum()


def snap_delta(raw_path, shipped):
    """What share of opaque pixels the palette snap MOVED, raw take vs staged plate.

    This is the number that settles whether the snap belongs on: near zero means the model
    drew inside the 55 and the snap is free, a big number means the take is off-palette and
    the author is choosing between its colours and the law - rather than having that choice
    made silently by a flag."""
    if not os.path.exists(raw_path):
        return None
    raw = Image.open(raw_path).convert('RGBA')
    if raw.size != shipped.size:
        return None
    a, b = np.asarray(raw), np.asarray(shipped)
    op = b[:, :, 3] >= 128
    if not op.any():
        return None
    moved = (a[:, :, :3][op] != b[:, :, :3][op]).any(1)
    return 100.0 * moved.mean()


def b64(im):
    buf = io.BytesIO()
    im.save(buf, format='PNG')
    return base64.b64encode(buf.getvalue()).decode('ascii')


def plate(name):
    p = os.path.join(STAGE, name + '.png')
    return Image.open(p).convert('RGBA') if os.path.exists(p) else None


def shipped(name):
    p = os.path.join(BACKGROUNDS, name + '.png')
    return Image.open(p).convert('RGBA') if os.path.exists(p) else None


# The detail each family is judged on, in art px. The room's is the window mullions and
# the corner where three planes meet - the first thing a downscale destroys. The
# counter's is the slab nose over the first cabinet door, where a soft edge shows worst.
ROOM_ZOOM = (0, 40, 160, 130)
COUNTER_ZOOM = (24, 20, 184, 110)


def report():
    room0, counter0 = shipped('club_room'), shipped('counter')
    if room0 is None or counter0 is None:
        raise SystemExit('the shipped plates are missing from ' + BACKGROUNDS)

    rows = [dict(key='shipped', family='both', label='Su an oyunda', tool='nano banana (elle)',
                 seed=None, native='2752 px boyanip 640\'a kucultuldu',
                 note='2026-08-18 Nano Banana batch\'i, scene_nb_post.py ile islendi. '
                      'Bulanikligin sebebi kayitli: alan-ortalamali kucultme dort boyali '
                      'pikseli bire katliyor, o yuzden hicbir kenar keskin degil.',
                 comp=composite(room0, counter0), room=room0, counter=counter0,
                 cells=cells(counter0),
                 zroom=zoom(room0, ROOM_ZOOM), zcounter=zoom(counter0, COUNTER_ZOOM),
                 cols=(colours(room0), colours(counter0)),
                 flat=(flatness(room0), flatness(counter0)), snap=(None, None))]

    for key, a in ASSETS.items():
        im = plate(key)
        if im is None:
            continue
        is_room = a['family'] == 'room'
        room = im if is_room else room0
        counter = im if not is_room else counter0
        rows.append(dict(
            key=key, family=a['family'], label=a['label'], note=a['note'],
            seed=a['seed'], tool=a['tool'],
            native='%dx%d dogrudan uretildi, yeniden olcekleme yok' % (im.width, im.height),
            prompt=a['args']['description'],
            comp=composite(room, counter),
            room=im if is_room else None, counter=im if not is_room else None,
            cells=cells(im) if not is_room else None,
            zroom=zoom(im, ROOM_ZOOM) if is_room else None,
            zcounter=zoom(im, COUNTER_ZOOM) if not is_room else None,
            cols=(colours(im), None) if is_room else (None, colours(im)),
            flat=(flatness(im), None) if is_room else (None, flatness(im)),
            snap=(snap_delta(os.path.join(RAW, key + '.png'), im),) * 2))

    os.makedirs(STAGE, exist_ok=True)
    out = os.path.join(STAGE, 'preview.html')
    io.open(out, 'w', encoding='utf-8').write(html(rows))
    print('wrote %s  (%d take)' % (os.path.relpath(out, ROOT), len(rows) - 1))


CSS = """
:root{
  --ink:#F2E8D5; --ink-dim:#C9BCA8; --ink-faint:#9C8F80;
  --ground:#1A1023; --panel:#241830; --panel-hi:#362447;
  --line:#4A3160; --rose:#E84DA6; --petrol:#3BC8BE; --brass:#E8A33D;
}
*{box-sizing:border-box}
body{
  margin:0; background:var(--ground); color:var(--ink);
  font-family:"IBM Plex Sans","Segoe UI",system-ui,sans-serif;
  font-size:15px; line-height:1.6; -webkit-font-smoothing:antialiased;
}
.wrap{max-width:1060px; margin:0 auto; padding:56px 26px 96px}
.eyebrow{margin:0; font-family:Silkscreen,"IBM Plex Mono",monospace; font-size:11px;
         letter-spacing:.18em; text-transform:uppercase; color:var(--rose)}
h1{font-family:Silkscreen,"IBM Plex Mono",monospace; font-weight:400;
   font-size:clamp(22px,3.6vw,34px); line-height:1.3; margin:16px 0 0;
   text-wrap:balance; color:var(--ink)}
.lede{max-width:64ch; color:var(--ink-dim); margin:18px 0 0}
.lede b{color:var(--ink); font-weight:600}
code{font-family:"IBM Plex Mono",monospace; font-size:.9em; color:var(--brass)}

.take{padding:40px 0; border-top:1px solid var(--line); display:grid; gap:18px}
.head{display:flex; flex-wrap:wrap; align-items:baseline; gap:12px}
.head h2{font-family:Silkscreen,"IBM Plex Mono",monospace; font-weight:400;
         font-size:18px; margin:0; color:var(--ink)}
.tag{font-family:"IBM Plex Mono",monospace; font-size:10.5px; letter-spacing:.1em;
     text-transform:uppercase; padding:3px 9px; border:1px solid var(--line);
     color:var(--ink-faint)}
.tag.room{color:var(--rose); border-color:#5C1B45}
.tag.counter{color:var(--petrol); border-color:#1B5F66}
.tag.now{color:var(--brass); border-color:#8F5A1E; background:#3A2410}
.note{max-width:64ch; color:var(--ink-dim); margin:0}

figure{margin:0; display:grid; gap:8px}
.shot{background:var(--panel); border:1px solid var(--line); padding:10px;
      overflow-x:auto}
.shot img{display:block; width:640px; max-width:100%; height:auto;
          image-rendering:pixelated}
.alpha .shot{background:
  repeating-conic-gradient(var(--panel-hi) 0 25%, var(--panel) 0 50%) 0 0/16px 16px}
.zoom .shot img{width:480px}
figcaption{font-family:"IBM Plex Mono",monospace; font-size:11.5px; color:var(--ink-faint)}
.pair{display:grid; grid-template-columns:repeat(auto-fit,minmax(300px,1fr)); gap:18px}

.meta{display:grid; grid-template-columns:repeat(auto-fit,minmax(160px,1fr));
      gap:12px 22px; margin:0; padding:14px 16px; background:var(--panel);
      border:1px solid var(--line)}
.meta > div{display:grid; gap:3px}
dt{font-family:"IBM Plex Mono",monospace; font-size:10.5px; letter-spacing:.1em;
   text-transform:uppercase; color:var(--ink-faint)}
dd{margin:0; font-family:"IBM Plex Mono",monospace; font-size:12.5px;
   font-variant-numeric:tabular-nums; color:var(--ink)}
dd.warn{color:var(--brass)}
dd.bad{color:var(--rose)}
details{border-top:1px solid var(--line); padding-top:12px}
summary{cursor:pointer; font-family:"IBM Plex Mono",monospace; font-size:11.5px;
        letter-spacing:.06em; text-transform:uppercase; color:var(--ink-faint)}
summary:focus-visible{outline:2px solid var(--rose); outline-offset:3px}
details p{font-family:"IBM Plex Mono",monospace; font-size:12px; line-height:1.75;
          color:var(--ink-dim); max-width:80ch}
footer{margin-top:52px; padding-top:22px; border-top:1px solid var(--line);
       color:var(--ink-faint); font-size:13px; max-width:72ch}
"""


def fig(im, caption, alt, cls=''):
    return ('<figure class="%s"><div class="shot"><img alt="%s" '
            'src="data:image/png;base64,%s"></div><figcaption>%s</figcaption></figure>'
            % (cls, alt, b64(im), caption))


def html(rows):
    p = []
    a = p.append
    a('<title>Tezgah &amp; Oda Takeleri</title>')
    a('<link rel="stylesheet" href="https://fonts.googleapis.com/css2?'
      'family=IBM+Plex+Mono:wght@400;500&family=IBM+Plex+Sans:wght@400;600&'
      'family=Silkscreen&display=swap">')
    a('<style>%s</style>' % CSS)
    a('<div class="wrap">')
    a('<p class="eyebrow">Last Call &middot; sahne sanati &middot; %s</p>'
      % time.strftime('%Y-%m-%d'))
    a('<h1>Tezgah &amp; oda takeleri</h1>')
    a('<p class="lede">Oyundaki iki plakanin PixelLab ile yeniden cekimleri. '
      '<b>Referans olarak alinan:</b> kamera acisi, olculer ve geometri &mdash; oda '
      '<b>640&times;360</b>, tezgah <b>640&times;150</b>, pencere solda perspektifte, '
      'tezgahin onunde sekiz goz. <b>Degisen:</b> uretim. Bu takeler sahnenin kendi '
      'olcusunde <b>dogrudan</b> ciziliyor, hicbir yerinde kucultme yok, ve her piksel '
      'paletin <b>55 rengine</b> oturuyor &mdash; oyundaki plakalarin bulanik durmasinin '
      'sebebi tam olarak bu ikisinin eksikligiydi. Her take sahnede duracagi gibi '
      'gosteriliyor: gokyuzu odanin kendi deliginden turetildi, tezgah <code>y 128</code> '
      'dayanma hattindan sarkiyor. Hicbiri oyuna kopyalanmadi.</p>')
    for r in rows:
        a('<section class="take">')
        a('<div class="head"><h2>%s</h2>%s</div>'
          % (r['label'],
             '<span class="tag now">oyunda</span>' if r['key'] == 'shipped'
             else '<span class="tag %s">%s</span>' % (r['family'], r['family'])))
        a('<p class="note">%s</p>' % r['note'])
        a(fig(r['comp'], 'Sahne bilesigi &mdash; oda + tezgah, 640&times;360',
              '%s sahnede' % r['label']))
        zooms = [z for z in (r.get('zroom'), r.get('zcounter')) if z is not None]
        if zooms:
            a('<div class="pair">')
            for z, cap in zip(zooms, ('Oda detayi, 3&times; nearest &mdash; pencere '
                                      'kayitlari ve uc duzlemin kosesi',
                                      'Tezgah detayi, 3&times; nearest &mdash; tabla '
                                      'burnu ve ilk dolap kapagi')):
                a(fig(z, cap, 'detay', 'zoom'))
            a('</div>')
        if r['key'] != 'shipped':
            plate_im = r.get('room') or r.get('counter')
            a(fig(plate_im,
                  'Plakanin kendisi &mdash; %dx%d, seffaf alanlar damali'
                  % plate_im.size, 'plaka', 'alpha'))
        a('<dl class="meta">')
        a('<div><dt>uretim</dt><dd>%s</dd></div>' % r['tool'])
        a('<div><dt>cozunurluk</dt><dd class="%s">%s</dd></div>'
          % ('bad' if r['key'] == 'shipped' else '', r['native']))
        a('<div><dt>seed</dt><dd>%s</dd></div>' % (r['seed'] or '&mdash;'))
        for val, name in zip(r['cols'], ('oda renk sayisi', 'tezgah renk sayisi')):
            if val is not None:
                a('<div><dt>%s</dt><dd>%d</dd></div>' % (name, val))
        for val, name in zip(r['flat'], ('oda duz alan', 'tezgah duz alan')):
            if val is not None:
                a('<div><dt>%s</dt><dd class="%s">%%%.1f</dd></div>'
                  % (name, 'bad' if val < 40 else '', val))
        if r['snap'][0] is not None:
            a('<div><dt>palet snapi oynatti</dt><dd class="%s">%%%.1f</dd></div>'
              % ('warn' if r['snap'][0] > 15 else '', r['snap'][0]))
        a('</dl>')
        if r.get('cells') is not None:
            a(fig(r['cells'],
                  'Sekiz goz &mdash; <code>ShelfCentrePx</code> nereye bardak koyuyor. '
                  'Her pembe tik bir gozun ortasi olmali; geçmeye denk gelen tik varsa '
                  'o tablo yeniden olculmeli.', 'goz kontrolu', 'alpha'))
        if r.get('prompt'):
            a('<details><summary>prompt</summary><p>%s</p></details>' % r['prompt'])
        a('</section>')
    a('<footer>Olculer sahnenin kendi sayilari: oda <code>DiegeticStage.Reference</code> '
      '640&times;360, tezgahin ust 130 satiri ekranda. &ldquo;Duz alan&rdquo; bulanikligin '
      'sayisal karsiligi: yan yana iki pikselin ayni olma orani &mdash; kucultme komsu '
      'pikselleri ortaladigi icin neredeyse hic ayni ikili birakmaz. Olcek: v3 sise %69, '
      'shaker %53, oyundaki oda %9,9. Tezgah takelerinde oyundaki '
      '<code>ShelfCentrePx</code> sekiz tik olarak '
      'plakanin uzerine cizildi; bir tik gozun degil gecmenin ustune duserse secilen '
      'take icin o tablo yeniden olculmeli. '
      'Palet snapi bu batch\'te <b>acik</b>; <code>--no-quantize</code> kapatiyor.'
      '</footer>')
    a('</div>')
    return '\n'.join(p)


# The Miami subset, as the eight ramps of the 55 this batch draws from. KEEP leaves out
# Lime (no business in a bar interior), Graphite and Brick (the drab greys that make the
# shipped plate read dead beside the author's references).
SWATCH_KEEP = [0, 1, 2, 3, 4, 5, 7, 8]   # Night Magenta Cyan Amber ViceRed ClubBlue Cream Malt


def make_swatch(cell=16):
    pal = nb.palette() if nb.PAL is None else nb.PAL
    ramps = [pal[i * 5:(i + 1) * 5] for i in SWATCH_KEEP]
    im = Image.new('RGB', (cell * 5, cell * len(ramps)))
    for y, ramp in enumerate(ramps):
        for x, c in enumerate(ramp):
            im.paste(tuple(int(v) for v in c),
                     (x * cell, y * cell, (x + 1) * cell, (y + 1) * cell))
    im.save(PALETTE_SWATCH)
    print('%s %dx%d, %d colours' % (os.path.relpath(PALETTE_SWATCH, ROOT),
                                    im.width, im.height, len(ramps) * 5))
    return im


# -- measuring what the stage measured off the old art -----------------------

def lamps(room):
    """The room's ceiling downlights, as DiegeticStage.LampArtPx wants them: art px, x from
    the left and y FROM THE TOP (ArtPxToWorld flips y against the sprite height).

    Same method the constants in the file were made by - cluster the warm-bright pixels in
    the ceiling band - written down here so the next room does not need it done by hand.
    A downlight is bright AND warm; a cyan rim light on the cornice is bright and is not,
    which is why the red-over-blue test is in the mask and not just luminance."""
    a = np.asarray(room.convert('RGB')).astype(np.int16)
    band = a[:110]                                     # the ceiling, generously
    lum = band.mean(axis=2)
    warm = (band[:, :, 0].astype(int) - band[:, :, 2]) > 18
    hot = (lum > np.percentile(lum, 97)) & warm
    cols = hot.any(axis=0)
    runs, start = [], None
    for x, on in enumerate(list(cols) + [False]):
        if on and start is None:
            start = x
        elif not on and start is not None:
            runs.append((start, x))
            start = None
    out = []
    for x0, x1 in runs:
        if x1 - x0 < 6:                                # a speck of warm wall, not a lamp
            continue
        ys, xs = np.where(hot[:, x0:x1])
        out.append((int(round(xs.mean())) + x0, int(round(ys.mean()))))
    return out


def slab_underside(counter):
    """The row where the bright slab gives way to the dark cabinet band - the candidate for
    DiegeticStage.ShelfCeilPx. Row means, first crossing of the midpoint between the
    plate's brightest and darkest row."""
    a = np.asarray(counter.convert('RGBA'))
    op = a[:, :, 3] >= 128
    lum = np.where(op, a[:, :, :3].mean(axis=2), np.nan)
    rows = np.nanmean(lum, axis=1)
    rows = np.where(np.isnan(rows), 0, rows)
    top = rows[:COUNTER_H // 3].max()
    mid = (top + rows[:COUNTER_H].min()) / 2.0
    for y in range(len(rows)):
        if rows[y] < mid:
            return y, rows
    return 0, rows


def measure(only=None):
    """Report every number the stage measured off a plate, for each staged take."""
    for key, a in ASSETS.items():
        if only and key not in only:
            continue
        im = plate(key)
        if im is None:
            continue
        print('[%s] %dx%d  colours=%d  flat runs=%.1f%%'
              % (key, im.width, im.height, colours(im), flatness(im)))
        if a['family'] == 'room':
            hole = np.asarray(im)[:, :, 3] < 128
            ys, xs = np.where(hole)
            if len(xs):
                print('  window hole: x %d..%d, y %d..%d (%d px)'
                      % (xs.min(), xs.max(), ys.min(), ys.max(), hole.sum()))
            else:
                print('  window hole: NONE - the green panes did not key')
            found = lamps(im)
            print('  LampArtPx candidates (x, y from top): %s'
                  % (', '.join('(%d, %d)' % c for c in found) or 'none found'))
            print('  in the file now: (222, 51), (331, 51), (455, 51)')
        else:
            y, rows = slab_underside(im)
            print('  slab underside row %d (ShelfCeilPx in the file: %d)' % (y, SHELF_CEIL_PX))
            print('  row luminance: %s'
                  % ' '.join('%d:%d' % (i, rows[i]) for i in range(0, 130, 10)))
            ov = os.path.join(STAGE, key + '_cells.png')
            cells(im).save(ov)
            print('  cell overlay -> %s' % os.path.relpath(ov, ROOT))


def ship(room_key, counter_key):
    """Copy two staged takes over the plates the game loads, re-derive the window plate from
    the new room's own hole, and re-measure the stage constants.

    The scene references these by PATH (DebugSceneCreator loads
    Assets/Art/Backgrounds/club_room.png), so overwriting in place needs no scene edit.
    The window plate is NOT copied - it is derived, by the same code that derived the last
    one, so it cannot fall out of register with a room it was not cut from."""
    room, counter = plate(room_key), plate(counter_key)
    if room is None or counter is None:
        raise SystemExit('stage both takes first: %s / %s' % (room_key, counter_key))
    if room.size != (REF_W, REF_H):
        raise SystemExit('room is %dx%d, the stage wants %dx%d' % (room.size + (REF_W, REF_H)))
    if counter.size != (REF_W, COUNTER_H):
        raise SystemExit('counter is %dx%d, the stage wants %dx%d'
                         % (counter.size + (REF_W, COUNTER_H)))
    room.save(os.path.join(BACKGROUNDS, 'club_room.png'))
    counter.save(os.path.join(BACKGROUNDS, 'counter.png'))
    print('shipped %s -> Assets/Art/Backgrounds/club_room.png' % room_key)
    print('shipped %s -> Assets/Art/Backgrounds/counter.png' % counter_key)
    nb.PAL = nb.palette() if nb.PAL is None else nb.PAL
    nb.post_window()                       # reads the shipped room, writes Scene/window_day
    log({'event': 'shipped', 'batch': 'scene-variants 2026-08-18',
         'room': room_key, 'counter': counter_key})
    print('\n-- constants to check against DiegeticStage.cs --')
    measure([room_key, counter_key])


def status():
    st = load()
    for key in ASSETS:
        rec = st.get(key, {})
        print('%-16s id=%-9s raw=%-5s staged=%s'
              % (key, (rec.get('id') or '-')[:8],
                 os.path.exists(os.path.join(RAW, key + '.png')),
                 os.path.exists(os.path.join(STAGE, key + '.png'))))


def main():
    argv = sys.argv[1:]
    # The snap is ON for this batch (the author's 2026-08-18 "quantize etme" was about a
    # painted batch; this one is drawn in the 55 on purpose). Kept as a flag so a take
    # can still be judged raw.
    nb.QUANTIZE = '--no-quantize' not in argv
    if '--no-quantize' in argv:
        argv.remove('--no-quantize')
    nb.PAL = nb.palette()
    cmd = argv[0] if argv else 'status'
    only = set(argv[1:]) or None
    if cmd == 'swatch':
        make_swatch()
    elif cmd == 'balance':
        pixellab.call('get_balance', {})
    elif cmd == 'queue':
        queue(only)
    elif cmd == 'fetch':
        fetch()
    elif cmd == 'post':
        post(only)
    elif cmd == 'report':
        report()
    elif cmd == 'measure':
        measure(only)
    elif cmd == 'ship':
        if len(argv) != 3:
            raise SystemExit('ship needs a room take and a counter take, e.g. '
                             'ship room_neon counter_marble')
        ship(argv[1], argv[2])
    else:
        status()


if __name__ == '__main__':
    main()
