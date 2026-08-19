# -*- coding: utf-8 -*-
"""The order bubble over a customer's head, through PixelLab (2026-08-19).

WHAT THIS SOLVES. The ticket over a stool already grows: TycoonHud measures its
widest line and sets the rect (TagMaxW caps it at 236, past which the order wraps
and the card grows DOWNWARD instead). What it has never had is a drawing - it is a
flat tinted rectangle with one neon rule under it. Hanging a picture of a balloon
on a rect that changes size every refresh is exactly how a prop gets stretched.

THE WAY ROUND IT, and it is not new here: a 9-SLICE BODY PLUS A SEPARATE TAIL.
BackBarArt.InfoPlate/InfoTail already do it for the bottle card - the plate is
9-sliced so a bottle's name decides its width, and the spout is its own sprite
placed in code, because a tail inside a stretched band smears along it.

But a GENERATED bubble cannot be 9-sliced as it comes back. A 9-slice may only
stretch a run that is uniform ALONG the edge it lies on, and a generator draws a
picture of a balloon: its top edge wobbles, its fill has texture, its left and
right sides are not the same. Slice that and the wobble smears.

So this script does not slice the generated image. It HARVESTS it:

    - find the bubble, find the tail, cut them apart
    - measure how deep the rounded corner runs (the first line whose middle is
      flat is where the corner ends - same test as market_borders.py)
    - rebuild a CANONICAL sprite, (2K+1) x (2K+1): the four K x K corners taken
      whole from the generated art, and a ONE-pixel cross between them, taken
      from the middle of each side and from the fill
    - border = (K, K, K, K), so at any size Unity draws the four real corners
      untouched and repeats single-pixel runs between them

A single-pixel run cannot smear - there is no detail along it to smear. The look
is the generator's; the geometry is ours. That is the whole trick.

THE LIT STATE IS DERIVED, NEVER GENERATED (see memory open-states-derive, and the
three times that trap has been walked into). The ticket already has two states -
resting, and lit when the drink is built and this customer can take it. Asking
PixelLab for a "lit version" comes back a different balloon. The lit plate here is
the resting plate with each pixel walked along its own ramp, so the two are the
same shape to the pixel and the 9-slice border measured once is true for both.

Commands:  balance | queue | fetch | post | proof | status
State:     Tools/bubble_state.json        Raw:    Tools/bubble_raw/
Staged:    Tools/AssetPipeline/staging/bubble/
Log:       Tools/AssetPipeline/generation_log.jsonl (15 SS5)

NOTHING HERE WRITES INTO Assets/. New art is reported first and goes into the game
after a pick (memory bottle-art-v3-respec).
"""
import io, json, os, re, sys, time
from PIL import Image
import pixellab

HERE = os.path.dirname(os.path.abspath(__file__))
STATE = os.path.join(HERE, 'bubble_state.json')
RAW = os.path.join(HERE, 'bubble_raw')
STAGE = os.path.join(HERE, 'AssetPipeline', 'staging', 'bubble')
LOG = os.path.join(HERE, 'AssetPipeline', 'generation_log.jsonl')

# The 55 (UITheme.cs verbatim; 14 v3 SS3). Every opaque pixel is snapped to one of
# these, so a generated balloon cannot bring its own colours into the room.
PALETTE = [
    0x0D0813, 0x1A1023, 0x241830, 0x362447, 0x4A3160,   # Night
    0x5C1B45, 0x8F2464, 0xC23283, 0xE84DA6, 0xFF7DC6,   # Magenta
    0x123B45, 0x1B5F66, 0x26918F, 0x3BC8BE, 0x7DF0E3,   # Cyan
    0x4A2E14, 0x8F5A1E, 0xC9822B, 0xE8A33D, 0xF5C97B,   # Amber
    0x3D1220, 0x6E1B32, 0xA62B44, 0xD9455C, 0xF27D8A,   # ViceRed
    0x131B3D, 0x1F2E66, 0x2E4699, 0x4467CC, 0x6E93F0,   # ClubBlue
    0x16331B, 0x2A5926, 0x479938, 0x6FCC4B, 0xA8F077,   # Lime
    0x453E38, 0x6E6459, 0x9C8F80, 0xC9BCA8, 0xF2E8D5,   # Cream
    0x3A2410, 0x6B4416, 0x9E6A1D, 0xC98F2B, 0xE6B959,   # Malt
    0x14161A, 0x24272D, 0x383D45, 0x545A64, 0x808893,   # Graphite
    0x38161A, 0x5C2226, 0x7E3130, 0x9C4740, 0xB96253,   # Brick
]
RAMPS = {name: PALETTE[i * 5:i * 5 + 5] for i, name in enumerate(
    'Night Magenta Cyan Amber ViceRed ClubBlue Lime Cream Malt Graphite Brick'.split())}

# The ticket is read across the room all night, so the balloon has ONE job: be a
# quiet dark field that cream, cyan and magenta type sits on. No pattern in the
# fill, no highlight, no gloss - the fill is where the words go.
#
# NO LIGHT IS PAINTED IN (2026-08-15). The room is lit in URP; a highlight baked
# into a plate glows in the dark and sits on the wrong side when the light moves.
STYLE = ('flat matte fill with no texture and no pattern, clean 1px outline, '
         'no anti-aliasing, no gradient, no highlight, no gloss, no reflection, '
         'no drop shadow, no glow, completely empty inside, blank interior, '
         'no text, no letters, no words, no symbols, no characters, no people')

# Four takes, so there is something to choose between (15 SS3). They differ in the
# ONE thing that decides whether a balloon belongs in this bar: what its edge is.
ASSETS = {
    'bub_round': dict(seed=43101, args=dict(width=128, height=96, description=(
        'empty speech balloon, wide rounded rectangle with softly rounded corners '
        'and a short pointed tail at the bottom centre, deep near-black purple '
        'fill #1A1023, clean outline in bright cyan #3BC8BE, ' + STYLE))),
    'bub_neon': dict(seed=43102, args=dict(width=128, height=96, description=(
        'empty speech balloon shaped like a neon bar sign, wide rectangle with '
        'chamfered cut corners and a short pointed tail at the bottom centre, '
        'dark navy fill #131B3D, double outline of cyan #26918F inside and '
        'pale cyan #7DF0E3 outside, ' + STYLE))),
    'bub_enamel': dict(seed=43103, args=dict(width=128, height=96, description=(
        'empty enamel bar plaque shaped as a speech balloon, wide rectangle with '
        'rounded corners and a short pointed tail at the bottom centre, dark '
        'graphite fill #24272D, thick cream rim #C9BCA8 with a darker inner edge '
        '#9C8F80, ' + STYLE))),
    'bub_card': dict(seed=43104, args=dict(width=128, height=96, description=(
        'empty order card shaped as a speech balloon, wide rectangle with square '
        'corners and a short pointed tail at the bottom centre, very dark purple '
        'fill #241830, thin magenta outline #C23283 with a brighter top edge '
        '#E84DA6, ' + STYLE))),
}

# ROUND TWO (2026-08-19), after the first four were held against the proof sheet.
# Three things the sheet showed, and each one is answered in the prompt below:
#
#   1. The fills came back MID-TONE. Quantize honestly snapped the generator's
#      indigo to ClubBlue[2] and its grey to Cream[1] — and cream, cyan and magenta
#      type on a mid-tone plate is the same unreadable ticket the rectangle was.
#      The fill is now asked for as near-black, by name and by hex.
#   2. bub_round measured a 25px radius, so its plate is 51 tall — and a ticket with
#      one line on it is 40. A 9-slice cannot draw a rect shorter than its own two
#      borders; the corners overlapped and the balloon collapsed. The radius is now
#      asked for small, and post() refuses a plate that cannot make 40.
#   3. The spouts came back as thin curved swooshes hung off the bottom-LEFT. A
#      swoosh at 1x is a scratch. Asked for solid, triangular, and centred.
#
# The plate is asked for WIDE AND SHORT now (128x64, not 128x96): the real ticket
# runs 156x40 to 236x84, and a generator given a squarer canvas draws a squarer
# balloon with a radius to match.
ROUND2 = ('wide and short, small corner radius of about 5 pixels, solid filled '
          'triangular spout at the bottom centre pointing straight down, '
          'thick spout at least 12 pixels wide where it joins')
ASSETS.update({
    'bub2_night': dict(seed=43201, args=dict(width=128, height=64, description=(
        'empty speech balloon, ' + ROUND2 + ', very dark near-black purple fill '
        '#1A1023, clean 1px outline in bright cyan #3BC8BE, ' + STYLE))),
    'bub2_slate': dict(seed=43202, args=dict(width=128, height=64, description=(
        'empty speech balloon, ' + ROUND2 + ', very dark graphite fill #14161A, '
        'clean 2px cream rim #C9BCA8, ' + STYLE))),
    'bub2_ink': dict(seed=43203, args=dict(width=128, height=64, description=(
        'empty speech balloon, ' + ROUND2 + ', black-purple fill #0D0813, thin '
        'magenta outline #C23283, ' + STYLE))),
    'bub2_brass': dict(seed=43204, args=dict(width=128, height=64, description=(
        'empty bar plaque shaped as a speech balloon, chamfered cut corners, ' +
        ROUND2 + ', very dark fill #14161A, thin brass outline #C9822B with a '
        'lighter amber top edge #E8A33D, ' + STYLE))),
})

# pixflux, not create_1_direction_object: the object tool takes no seed (so a take
# cannot be reproduced) and only draws SQUARE, and a balloon that is as tall as it
# is wide is a thought bubble. pixflux takes width/height and no_background, which
# is the transparent wide plate this needs.
TOOL = 'create_image_pixflux'

UUID = re.compile(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}')


def load():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def save(s):
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(s, indent=2))


def log(rec):
    os.makedirs(os.path.dirname(LOG), exist_ok=True)
    with io.open(LOG, 'a', encoding='utf-8') as f:
        f.write(json.dumps(rec) + '\n')


def texts(msgs):
    out = []
    for m in msgs:
        for c in ((m.get('result') or {}).get('content') or []):
            if c.get('type') == 'text':
                out.append(c['text'])
    return '\n'.join(out)


def images(msgs):
    import base64
    out = []
    for m in msgs:
        for c in ((m.get('result') or {}).get('content') or []):
            if c.get('type') == 'image' and c.get('data'):
                out.append(Image.open(io.BytesIO(base64.b64decode(c['data']))).convert('RGBA'))
    return out


def queue(only=None):
    st = load()
    for key, a in ASSETS.items():
        if only and key not in only:
            continue
        if st.get(key, {}).get('id'):
            continue
        args = dict(a['args'], seed=a['seed'], no_background=True)
        msgs = pixellab.call(TOOL, args, timeout=900)
        body = texts(msgs)
        m = UUID.search(body)
        st[key] = {'id': m.group(0) if m else None}
        save(st)
        log({'asset': key, 'tool': TOOL, 'seed': a['seed'],
             'prompt': a['args']['description'], 'job': st[key]['id'],
             'event': 'queued' if m else 'queue-failed', 'raw': body[:300]})
        print('%-12s -> %s' % (key, st[key]['id'] or body[:140].replace('\n', ' ')))
        time.sleep(0.6)


def fetch():
    os.makedirs(RAW, exist_ok=True)
    st = load()

    def pending():
        return {k: v for k, v in st.items() if v.get('id')
                and not os.path.exists(os.path.join(RAW, k + '.png'))}

    left = pending()
    for _ in range(80):
        if not left:
            break
        moved = False
        for key, rec in sorted(left.items()):
            msgs = pixellab.call('get_image', {'job_id': rec['id']}, timeout=300)
            ims, body = images(msgs), texts(msgs)
            if ims:
                # One job, one drawing.
                ims[0].save(os.path.join(RAW, key + '.png'))
                print('fetched', key, ims[0].size)
                log({'asset': key, 'event': 'fetched'})
                moved = True
            elif 'failed' in body.lower():
                print('FAILED', key, body[:200].replace('\n', ' '))
                log({'asset': key, 'event': 'failed', 'raw': body[:300]})
                rec['id'] = None
                save(st)
                moved = True
        left = pending()
        if left and not moved:
            print(' %d pending...' % len(left))
            time.sleep(25)
    print('missing:', sorted(left) if left else 'none')


# ── post: key -> quantize -> cut -> rebuild the 9-slice -> derive the lit state ──

def key_green(im):
    """#00FF00 to alpha before quantize ever sees it (the house chroma)."""
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if g > 180 and r < 90 and b < 90:
                px[x, y] = (0, 0, 0, 0)
    return im


def _near(rgb):
    r, g, b = rgb
    best, bd = PALETTE[0], 1 << 30
    for p in PALETTE:
        pr, pg, pb = (p >> 16) & 255, (p >> 8) & 255, p & 255
        d = (pr - r) ** 2 + (pg - g) ** 2 + (pb - b) ** 2
        if d < bd:
            best, bd = p, d
    return ((best >> 16) & 255, (best >> 8) & 255, best & 255)


def quantize(im):
    px = im.load()
    seen = {}
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a < 128:
                px[x, y] = (0, 0, 0, 0)
                continue
            k = (r, g, b)
            if k not in seen:
                seen[k] = _near(k)
            px[x, y] = seen[k] + (255,)
    return im


def bbox(im):
    return im.getbbox()


def _opaque_cols(im):
    px = im.load()
    return [x for x in range(im.width)
            if any(px[x, y][3] > 0 for y in range(im.height))]


def split_tail(im):
    """Cut the balloon from its tail, and say where the tail POINTS.

    The first take of this measured how many pixels a row had and called the
    widest ones body. That is wrong twice: a hollow balloon (bub_neon came back as
    an outline with nothing in it) has two opaque pixels on its middle rows, and a
    ROUNDED bottom corner narrows the last few body rows below any threshold that
    still excludes a tail. Both put the seam inside the body, and the "tail" came
    back 98 wide.

    What actually separates them is SPAN, not count: every body row reaches from
    near the left edge to near the right edge whether it is filled or hollow, and
    no tail does. So the seam is the lowest row still spanning more than half the
    drawing, and everything under it is the spout.

    The tip is measured too. The generator hangs the tail wherever it likes — left
    of centre on three of these four — but the thing it has to point at is the
    customer's head, which is under the balloon's MIDDLE. So the caller is handed
    the tip's x within the tail, and places the tail by it.
    """
    im = im.crop(bbox(im))
    px = im.load()
    W, H = im.size
    span = []
    for y in range(H):
        xs = [x for x in range(W) if px[x, y][3] > 0]
        span.append((xs[0], xs[-1]) if xs else None)

    seam = None
    for y in range(H - 1, -1, -1):
        s = span[y]
        if s and s[1] - s[0] >= W * 0.55:
            seam = y
            break
    if seam is None or seam >= H - 3:
        return im, None, 0                   # no spout under it: no tail

    body = im.crop((0, 0, W, seam + 1))
    tail = im.crop((0, seam + 1, W, H))
    tb = bbox(tail)
    tail = tail.crop(tb)

    # The TIP is the lowest opaque pixel — the end of the spout, the pixel that has
    # to land over the head. Taken as the middle of that lowest row, so a two-pixel
    # tip does not bias the placement by half a pixel.
    tpx = tail.load()
    tip = tail.width // 2
    for y in range(tail.height - 1, -1, -1):
        xs = [x for x in range(tail.width) if tpx[x, y][3] > 0]
        if xs:
            tip = (xs[0] + xs[-1]) // 2
            break

    # THE SKIRT (the same trick InfoTail uses, upside down). The body's bottom
    # outline runs straight across the place the spout joins it, so a tail simply
    # butted underneath leaves a rule drawn through the join and the two read as
    # two objects. Two rows of the body's own FILL are grown on top of the tail;
    # placed overlapping the body by those two rows, they erase the outline exactly
    # where the balloon should be open, and the pair reads as one shape.
    fill = _fill_colour(body)
    if fill is not None:
        skirted = Image.new('RGBA', (tail.width, tail.height + 2), (0, 0, 0, 0))
        skirted.paste(tail, (0, 2))
        spx = skirted.load()
        xs = [x for x in range(tail.width) if tpx[x, 0][3] > 0]
        if xs:
            for y in (0, 1):
                for x in range(xs[0], xs[-1] + 1):
                    spx[x, y] = fill
        tail = skirted
    return body, tail, tip


def _fill_colour(body):
    """The balloon's own fill: the commonest opaque colour on its middle rows.

    Middle rows, because the top and bottom are outline. A hollow balloon has no
    fill and says so with None — its spout then joins without a skirt, which is
    right: there is no outline to erase when the shape is only outline.
    """
    px = body.load()
    W, H = body.size
    tally = {}
    for y in range(int(H * 0.35), int(H * 0.65)):
        for x in range(int(W * 0.2), int(W * 0.8)):
            c = px[x, y]
            if c[3] == 255:
                tally[c] = tally.get(c, 0) + 1
    if not tally:
        return None
    best = max(tally, key=tally.get)
    return best if tally[best] >= (W * 0.6 * H * 0.3) * 0.5 else None


def corner_depth(im):
    """How far the CORNERS reach in, which is what a 9-slice border must clear.

    The first version of this borrowed market_borders.py's test — walk in until a
    line's middle is flat — and it was the wrong question. That test finds where a
    detailed FRAME ends, and a balloon has no frame: the middle of its very top row
    is already flat straight edge, so every shape measured 0 and got the floor.

    The question a rounded corner actually answers is where the ARC ends, and the
    arc announces itself: on a rounded rectangle the top row does not start at x=0,
    it starts at x=r. So each corner is measured on both of its edges — how far in
    the edge row begins, and how far down the edge column begins — and the deepest
    of the eight readings is the radius the border has to cover. One number for all
    four sides, because Unity draws one border and a lopsided one would put a
    corner's tail into the stretched run.
    """
    px = im.load()
    W, H = im.size

    def first_on_row(y, rev=False):
        rng = range(W - 1, -1, -1) if rev else range(W)
        for i, x in enumerate(rng):
            if px[x, y][3] > 0:
                return i
        return 0

    def first_on_col(x, rev=False):
        rng = range(H - 1, -1, -1) if rev else range(H)
        for i, y in enumerate(rng):
            if px[x, y][3] > 0:
                return i
        return 0

    d = max(first_on_row(0), first_on_row(0, True),
            first_on_row(H - 1), first_on_row(H - 1, True),
            first_on_col(0), first_on_col(0, True),
            first_on_col(W - 1), first_on_col(W - 1, True))
    # +2: the border must own the arc AND the first straight pixel after it. A
    # border that ends ON the arc leaves the last curved pixel to be the one Unity
    # repeats along the whole edge, which draws the end of the curve forever.
    return max(3, min(d + 2, min(W, H) // 2 - 1))


def canonical(im, k):
    """The (2k+1) square: four real corners, and a one-pixel cross between them.

    This is the whole reason the balloon can grow without being stretched. Unity
    draws the corners 1:1 whatever the rect and repeats the cross, and a run one
    pixel long has no detail along it to smear.

    The cross is taken from the MIDDLE of each side and of the fill, which is the
    quietest place on a generated drawing - a run picked next to a corner carries
    the end of the arc into every size the balloon is ever drawn at.
    """
    W, H = im.size
    S = 2 * k + 1
    out = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    # corners, whole
    out.paste(im.crop((0, 0, k, k)), (0, 0))
    out.paste(im.crop((W - k, 0, W, k)), (k + 1, 0))
    out.paste(im.crop((0, H - k, k, H)), (0, k + 1))
    out.paste(im.crop((W - k, H - k, W, H)), (k + 1, k + 1))
    # the cross: top and bottom rows, left and right columns, and the fill
    midx, midy = W // 2, H // 2
    top = im.crop((midx, 0, midx + 1, k)).resize((1, k), Image.NEAREST)
    bot = im.crop((midx, H - k, midx + 1, H)).resize((1, k), Image.NEAREST)
    lef = im.crop((0, midy, k, midy + 1)).resize((k, 1), Image.NEAREST)
    rig = im.crop((W - k, midy, W, midy + 1)).resize((k, 1), Image.NEAREST)
    out.paste(top, (k, 0))
    out.paste(bot, (k, k + 1))
    out.paste(lef, (0, k))
    out.paste(rig, (k + 1, k))
    out.paste(im.crop((midx, midy, midx + 1, midy + 1)), (k, k))
    return out


def ramp_of(rgb):
    """Which ramp a palette colour belongs to, and at which step."""
    v = (rgb[0] << 16) | (rgb[1] << 8) | rgb[2]
    for name, steps in RAMPS.items():
        if v in steps:
            return name, steps.index(v)
    return None, None


def light(im):
    """The LIT plate, DERIVED (never generated).

    The ticket lights when the drink is built and this customer can take it. Every
    pixel walks to Cyan at the step it already sat on - so the fill stays the fill
    and the rim stays the rim, the shape is identical to the pixel, and the border
    measured on the resting plate is true for this one too.
    """
    im = im.copy()
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            name, step = ramp_of((r, g, b))
            if name is None:
                continue
            # up one step as well as across: "lit" is brighter, not merely bluer.
            v = RAMPS['Cyan'][min(4, step + 1)]
            px[x, y] = ((v >> 16) & 255, (v >> 8) & 255, v & 255, a)
    return im


def lawful(im, fill_rgb):
    """The candidate with its RIM walked onto Cyan — derived, not regenerated.

    16 SS5: money is Amber, the story is Magenta, information and the clock are
    Cyan, and a sacred colour is not reused for decoration. The generator was given
    brass and magenta rims because they look good on a bar plaque, and they do —
    but an order ticket edged in Amber is the money colour spent on a border, and
    the tag already lights CYAN when the drink can be taken. Cyan is the ramp this
    object actually belongs to, and it was already using it.

    So the rim moves and nothing else does. The fill's own ramp is found first (it
    is whatever most of the plate is made of) and left alone — the fill is the dark
    the words sit on and it belongs to Night or Graphite, neither of which signals.
    Every pixel on any OTHER ramp walks to Cyan at the step it already stood on, so
    a two-tone rim stays two-tone and the shape is identical to the pixel.
    """
    im = im.copy()
    px = im.load()
    # The fill's ramp is TOLD, not counted. Counting was the first attempt and it
    # got the answer backwards on the small plates: a canonical 13x13 is almost
    # entirely corner and rim, its fill is one pixel, so the tally crowned the RIM
    # as the fill and the remap then moved the wrong half. The caller hands in the
    # plate's centre pixel, which is the fill by construction.
    fill_ramp, _ = ramp_of(fill_rgb[:3]) if fill_rgb else (None, None)
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            name, step = ramp_of((r, g, b))
            if name is None or name == fill_ramp or name == 'Cyan':
                continue
            v = RAMPS['Cyan'][step]
            px[x, y] = ((v >> 16) & 255, (v >> 8) & 255, v & 255, a)
    return im


def post():
    os.makedirs(STAGE, exist_ok=True)
    out = {}
    for key in sorted(ASSETS):
        src = os.path.join(RAW, key + '.png')
        if not os.path.exists(src):
            print('%-12s -- not fetched' % key)
            continue
        im = quantize(key_green(Image.open(src).convert('RGBA')))
        body, tail, tip = split_tail(im)
        k = corner_depth(body)
        plate = canonical(body, k)
        # A 9-slice cannot draw a rect shorter than its own two borders, and the
        # shortest ticket in the game is 40 (one line). A plate that fails this is
        # not a near miss to be nudged — it collapses on every single-line ticket,
        # which is most of them. Said out loud rather than staged quietly.
        if 2 * k >= 40:
            print('%-12s REJECT: border %d needs a ticket %d tall; one line is 40'
                  % (key, k, 2 * k))
            continue
        lit = light(plate)
        plate.save(os.path.join(STAGE, key + '_plate.png'))
        lit.save(os.path.join(STAGE, key + '_plate_lit.png'))
        fill_rgb = plate.getpixel((k, k))   # the middle of the cross: the fill itself
        lawful(plate, fill_rgb).save(os.path.join(STAGE, key + '_cyan.png'))
        if tail is not None:
            tail.save(os.path.join(STAGE, key + '_tail.png'))
            light(tail).save(os.path.join(STAGE, key + '_tail_lit.png'))
            lawful(tail, fill_rgb).save(os.path.join(STAGE, key + '_cyan_tail.png'))
        # the generated drawing, kept beside them so a pick is made against what
        # the generator actually drew and not only against the harvest
        body.save(os.path.join(STAGE, key + '_source.png'))
        out[key] = dict(border=k, plate=list(plate.size), source=list(body.size),
                        tail=list(tail.size) if tail is not None else None,
                        tail_tip_x=tip if tail is not None else None)
        print('%-12s border=%d  plate=%dx%d  tail=%s' % (
            key, k, plate.size[0], plate.size[1],
            '%dx%d' % tail.size if tail is not None else 'NONE - no neck found'))
    io.open(os.path.join(STAGE, 'measured.json'), 'w', encoding='utf-8').write(
        json.dumps(out, indent=2))
    return out


def nine_slice(plate, k, w, h):
    """Draw a 9-slice by hand, the way Unity's sliced Image does.

    Here so the proof sheet is made by the SAME rule the game will draw by. A proof
    rendered any other way proves nothing about the thing that ships.

    Both images are PIL's, so row 0 is the TOP of both. The first version of this
    read the plate bottom-up — Unity's convention, not PIL's — and quietly drew
    every candidate UPSIDE DOWN. It was invisible on a near-symmetric balloon and
    it is exactly the kind of thing a proof sheet exists to not have: the one row
    with a lit top edge showed its rim along the bottom instead.
    """
    S = plate.size[0]
    mw = mh = S - 2 * k                    # the middle run, 1x1 by construction
    out = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    cw, ch = max(w - 2 * k, 0), max(h - 2 * k, 0)
    out.paste(plate.crop((0, 0, k, k)), (0, 0))
    out.paste(plate.crop((S - k, 0, S, k)), (w - k, 0))
    out.paste(plate.crop((0, S - k, k, S)), (0, h - k))
    out.paste(plate.crop((S - k, S - k, S, S)), (w - k, h - k))
    if cw > 0:
        out.paste(plate.crop((k, 0, k + mw, k)).resize((cw, k), Image.NEAREST), (k, 0))
        out.paste(plate.crop((k, S - k, k + mw, S)).resize((cw, k), Image.NEAREST), (k, h - k))
    if ch > 0:
        out.paste(plate.crop((0, k, k, k + mh)).resize((k, ch), Image.NEAREST), (0, k))
        out.paste(plate.crop((S - k, k, S, k + mh)).resize((k, ch), Image.NEAREST), (w - k, k))
    if cw > 0 and ch > 0:
        out.paste(plate.crop((k, k, k + mw, k + mh)).resize((cw, ch), Image.NEAREST), (k, k))
    return out


def hang(plate, k, tail, tip, w, h):
    """One ticket at one size: the plate at w x h with the spout under its middle.

    The tail is placed by its TIP, not by its own middle — the spout has to end
    over the customer's head, and three of these four were drawn with the spout off
    to one side. It overlaps the plate by the two skirt rows so the plate's bottom
    outline is erased exactly where the balloon opens.
    """
    th = tail.height - 2 if tail is not None else 0
    out = Image.new('RGBA', (w, h + max(0, th)), (0, 0, 0, 0))
    out.paste(nine_slice(plate, k, w, h), (0, 0))
    if tail is not None:
        out.alpha_composite(tail, (w // 2 - tip, h - 2))
    return out


# The three sizes a real ticket takes, measured off TycoonHud: it never draws
# narrower than BustW + 48 and never wider than TagMaxW, and its height is its rows
# packed (one line, two, or an order that wrapped).
SIZES = [(156, 40, 'JEN  /  one line'),
         (196, 62, 'MARCO  /  BOURBON NEAT'),
         (236, 84, 'ANNIKA  /  SEX ON THE BEACH (wrapped)')]


def proof(variant=''):
    """The sheet the pick is made against: every candidate at every size it will
    really be drawn at, sliced by the rule the game slices by, on the room's own
    dark. Rendered at 1x and again at 3x, because a pixel decision made at 1x on a
    modern screen is a decision made blind (15 SS3)."""
    meas = json.load(io.open(os.path.join(STAGE, 'measured.json'), encoding='utf-8'))
    keys = sorted(meas)
    pad, gap = 16, 14
    rowh = max(h for _, h, _ in SIZES) + 34 + gap
    W = pad * 2 + sum(w for w, _, _ in SIZES) + gap * (len(SIZES) - 1) + 150
    H = pad * 2 + rowh * len(keys)
    sheet = Image.new('RGBA', (W, H), (0x1A, 0x10, 0x23, 0xFF))
    from PIL import ImageDraw
    d = ImageDraw.Draw(sheet)
    y = pad
    for key in keys:
        m = meas[key]
        k = m['border']
        pn = '_cyan' if variant == 'cyan' else '_plate'
        pp = os.path.join(STAGE, key + pn + '.png')
        if not os.path.exists(pp):
            continue
        plate = Image.open(pp).convert('RGBA')
        tp = os.path.join(STAGE, key + ('_cyan_tail' if variant == 'cyan' else '_tail') + '.png')
        tail = Image.open(tp).convert('RGBA') if os.path.exists(tp) else None
        d.text((pad, y + 6), '%s   border=%d   plate=%dx%d' % (key, k, *plate.size),
               fill=(0xF2, 0xE8, 0xD5, 0xFF))
        x = pad + 150
        for w, h, label in SIZES:
            tile = hang(plate, k, tail, m.get('tail_tip_x') or 0, w, h)
            sheet.alpha_composite(tile, (x, y + 24))
            d.text((x, y + 24 + h + (tail.height if tail else 0) + 2), label,
                   fill=(0x9C, 0x8F, 0x80, 0xFF))
            x += w + gap
        y += rowh
    tag = variant or 'asis'
    sheet.save(os.path.join(STAGE, 'proof_%s_1x.png' % tag))
    sheet.resize((W * 3, H * 3), Image.NEAREST).save(
        os.path.join(STAGE, 'proof_%s_3x.png' % tag))
    print('proof %s -> %dx%d' % (tag, *sheet.size))


def status():
    st = load()
    for key in sorted(ASSETS):
        rec = st.get(key, {})
        got = os.path.exists(os.path.join(RAW, key + '.png'))
        print('%-12s id=%s raw=%s' % (key, (rec.get('id') or '-')[:8], 'yes' if got else 'no'))
    pixellab.call('get_balance', {})


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'status'
    only = sys.argv[2:] or None
    if cmd == 'balance':
        pixellab.call('get_balance', {})
    elif cmd == 'queue':
        queue(only)
    elif cmd == 'fetch':
        fetch()
    elif cmd == 'post':
        post()
    elif cmd == 'proof':
        proof(); proof('cyan')
    elif cmd == 'status':
        status()
    else:
        raise SystemExit(__doc__)
