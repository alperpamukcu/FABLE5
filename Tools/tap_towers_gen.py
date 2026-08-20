# -*- coding: utf-8 -*-
"""Three gold beer-tap towers, one per tier (2026-08-21).

The author's brief: "3 adet fici bira muslugu kulesi uret masaya bagli olan unitelerden.
gold color natural kontrast beer tap single-mouth. 3 adet uret 1-2-3 baslikli olmak
uzere ... bu mekana uyumlu olmali cizim olarak. boyutu 64x64 olmali"

WHAT THE TIERS ARE. All three are SINGLE-MOUTH - the tier is not the number of spouts,
which is what the old set did (fx_tap_single / double / triple, 14 §5c). Here the ladder is
craft: a plain column, then a fluted one with a collar, then a deco tower with a crown. That
matches how the bar's other upgrades read (14 §6's counter tiers are one material getting
richer, not one counter growing extra parts) and it keeps all three interchangeable in the
same 64x64 socket.

WHY GOLD READS HERE. The tap stands ON the counter the author drew - a blue cabinet under a
near-black slab with a magenta edge line. Brass against that is the strongest legible
contrast the room's own palette offers, and it is already the house's gold: 14 §3's Amber
ramp, #4A2E14 / #8F5A1E / #C9822B / #E8A33D / #F5C97B, used here by name so the towers land
inside the palette rather than beside it.

LINE LANGUAGE, and it differs from the cast's on purpose. The people are drawn with NO
keyline at all; the author's counter is drawn WITH one. A tap sits on the counter, not in
the crowd, so it follows the counter - but the outline is asked for in the object's own
darkest tone rather than in black, which is 14 §3's standing rule and also what keeps a
64 px object from turning into a silhouette.

"Natural kontrast" is taken literally: the shading steps are asked to stay gentle. A tap
lit like a chrome render would be a second sun in a room lit by URP 2D lights, which is the
one rule every brief in this project shares.

Commands:  queue | fetch | sheet
State:     Tools/tap_towers_state.json     Raw: Tools/scene_cast_raw/
"""
import io, json, os, re, sys, time
from PIL import Image
import pixellab

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
STATE = os.path.join(HERE, 'tap_towers_state.json')
RAW = os.path.join(HERE, 'scene_cast_raw')
LOG = os.path.join(HERE, 'AssetPipeline', 'generation_log.jsonl')
UUID = re.compile(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}')

SIZE = 64

# Prepended to all three so a change reaches the whole set, the way HOUSE_RULES does for
# the cast. Everything here is a rule this project has already paid for once.
STYLE = (
    'pixel art, one single object centred on a transparent background, straight-on front '
    'view, flat matte local colour, natural gentle contrast with soft shading steps, '
    'no baked lighting, no specular highlights, no reflections, no glow, no cast shadow, '
    'form shaded only by stepping along the brass ramp #4A2E14 #8F5A1E #C9822B #E8A33D '
    '#F5C97B, outline in the object\'s own darkest brass tone and never pure black, '
    'no text, no logo, no label, no beer glass, no counter, no background'
)

# ── the mouth variants (2026-08-21) ────────────────────────────────────────
# The author looked at the three craft tiers and re-pointed the ladder: "bu muslugun
# birebir tarzinda sadece bunun 2 ve 3 baslisini uret aynı tasarim olacak ama birisinde 2
# musluk olacak digerinde 3 musluk". So the rung is the MOUTH COUNT after all, and tier
# one's plain column is the design all three share. The fluted and deco towers above are
# not deleted - they are simply not what was asked for; whether they stay in the set is
# the author's call, not something to decide by overwriting them.
#
# TIER ONE DESCRIBED FROM ITS OWN PIXELS, not from the prompt that made it. Zoomed to 6x:
# a plain straight gold cylinder with a slight lip at the top, standing on a small flared
# round foot; one horizontal arm out to the LEFT near the top ending in a down-turned
# spout; an amber teardrop handle on a thin stem above that arm; a lighter gold highlight
# stripe down the left of the column and a darker edge on the right. Writing it from the
# art rather than re-using the old words is the only way "birebir" means anything - the
# old words produced this by luck as much as by instruction.
TAP1_DESIGN = (
    'a plain straight round polished gold brass beer tap column with a slight lip at the '
    'top, standing on a small flared round gold foot, a lighter gold highlight stripe '
    'down its left side and a darker gold edge down its right, no decoration, no fluting, '
    'no collar, no crown'
)
SPOUT = ('a horizontal gold arm near the top ending in a short down-turned spout, with a '
         'small amber-gold teardrop handle on a thin stem above it')

MOUTHS = {
    'tap_gold_2mouth': ('TWO spouts on the same column: ' + SPOUT + ' on the LEFT, and an '
                        'identical one on the RIGHT, ' + TAP1_DESIGN),
    'tap_gold_3mouth': ('THREE spouts on the same column: ' + SPOUT + ' on the LEFT, an '
                        'identical one on the RIGHT, and a third identical one in the '
                        'CENTRE facing the viewer, ' + TAP1_DESIGN),
}
# tap_gold_1 rides along as the style image so "birebir" has something to copy rather than
# only something to read. If create_1_direction_object does not accept one the call fails
# in validation - before generating, so before costing anything - the way `seed` did.
MOUTH_STYLE = os.path.join(RAW, 'tap_gold_1.png')

TAPS = {
    # T1 - the tap a bar opens with. Plain on purpose: the ladder needs a bottom rung that
    # looks honest rather than cheap.
    'tap_gold_1': (41, 'a plain brass beer tap tower for a bar counter, ONE single spout, '
                       'a straight round polished brass column standing on a round brass '
                       'base plate, one short amber-gold tap handle on the front, simple '
                       'and undecorated'),
    # T2 - the same tower with craft in it: a collar and fluting.
    # RE-ROLLED ONCE (2026-08-21). The first take came back BROWN - 37% of it inside the
    # amber ramp against 53% for its neighbours, the column drawn as wood grain (#7a4d2f,
    # #47270d) - and carrying a pure-black outline at 21.8% of its pixels, which the style
    # block forbids in as many words.
    #
    # The likely culprit is one word: "TURNED". A turned collar is a lathe term and the
    # lathe the model knows is a woodworker's; asked for a turned column it drew timber and
    # then inked it the way it inks wood. Same shape as the cast's lesson - silverbob's
    # blazer, spanishsuit's waistcoat, the parquet's seams - where the cure was never to
    # ask harder but to delete the word that drags the material in. So "turned" goes, and
    # the metal is named twice more so there is nothing to mistake it for.
    'tap_gold_2': (42, 'an elegant POLISHED GOLD BRASS METAL beer tap tower for a bar '
                       'counter, ONE single spout, a tapered gold metal column with fine '
                       'vertical fluting and a plain ring collar at its middle, standing '
                       'on a wider stepped gold base plate, one curved amber-gold tap '
                       'handle on the front, all metal, no wood, no wood grain'),
    # T3 - deco, because the room is deco. The crown is what reads at 64 px.
    'tap_gold_3': (43, 'an ornate art-deco brass beer tap tower for a bar counter, ONE '
                       'single spout, a tall stepped deco column with vertical fluted '
                       'panels and a scalloped crown on top, standing on a broad stepped '
                       'brass base plate, one long curved amber-gold tap handle'),
}


def load():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def save(s):
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(s, indent=1))


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
            if c.get('type') == 'image':
                out.append(Image.open(io.BytesIO(base64.b64decode(c['data']))).convert('RGBA'))
    return out


def queue_mouths(only=None):
    import base64
    st = load()
    for key, body in MOUTHS.items():
        if only and key not in only:
            continue
        if st.get(key, {}).get('id'):
            print('%-16s already queued' % key)
            continue
        desc = body + ', ' + STYLE
        args = dict(size=SIZE, view='sidescroller', description=desc)
        args['style_image_base64'] = base64.b64encode(
            io.open(MOUTH_STYLE, 'rb').read()).decode()
        msgs = pixellab.call('create_1_direction_object', args, timeout=900)
        b = texts(msgs)
        if 'style_image_base64' in b and 'Unexpected keyword' in b:
            print('  (no style image support - retrying on words alone)')
            args.pop('style_image_base64')
            msgs = pixellab.call('create_1_direction_object', args, timeout=900)
            b = texts(msgs)
        m = UUID.search(b)
        st[key] = {'id': m.group(0) if m else None}
        save(st)
        with io.open(LOG, 'a', encoding='utf-8') as f:
            f.write(json.dumps({'asset': key, 'tool': 'create_1_direction_object',
                                'prompt': desc, 'job': st[key]['id'],
                                'event': 'queued' if m else 'queue-failed'}) + '\n')
        print('%-16s -> %s' % (key, st[key]['id'] or b[:200].replace('\n', ' ')))
        time.sleep(0.6)


def queue(only=None):
    st = load()
    for key, (seed, body) in TAPS.items():
        if only and key not in only:
            continue
        if st.get(key, {}).get('id'):
            print('%-12s already queued' % key)
            continue
        desc = body + ', ' + STYLE
        # NO SEED. create_1_direction_object does not take one - it is an image-tool
        # argument only, and passing it fails the call with a pydantic error rather than
        # being ignored. That means these three are not reproducible by re-running; the
        # prompt in the log is the record, and the fetched PNG is the artefact.
        msgs = pixellab.call('create_1_direction_object',
                             dict(size=SIZE, view='sidescroller',
                                  description=desc), timeout=900)
        b = texts(msgs)
        m = UUID.search(b)
        st[key] = {'id': m.group(0) if m else None, 'seed': seed}
        save(st)
        with io.open(LOG, 'a', encoding='utf-8') as f:
            f.write(json.dumps({'asset': key, 'tool': 'create_1_direction_object',
                                'seed': seed, 'prompt': desc, 'job': st[key]['id'],
                                'event': 'queued' if m else 'queue-failed'}) + '\n')
        print('%-12s -> %s' % (key, st[key]['id'] or b[:160].replace('\n', ' ')))
        time.sleep(0.6)


def fetch():
    st = load()
    for _ in range(40):
        pending = [k for k in list(TAPS) + list(MOUTHS) if (st.get(k) or {}).get('id')
                   and not os.path.exists(os.path.join(RAW, k + '.png'))]
        if not pending:
            break
        moved = False
        for key in pending:
            msgs = pixellab.call('get_object',
                                 {'object_id': st[key]['id'], 'include_preview': True},
                                 timeout=300)
            ims, b = images(msgs), texts(msgs)
            if ims:
                ims[0].save(os.path.join(RAW, key + '.png'))
                print('fetched', key, ims[0].size)
                moved = True
            elif 'failed' in b.lower():
                print('FAILED', key, b[:180].replace('\n', ' '))
                st[key]['id'] = None
                save(st)
                moved = True
        if not moved:
            print(' %d pending...' % len(pending))
            time.sleep(25)


def sheet():
    """The three towers standing on the author's own counter slab, at 1x and at 4x.

    A tap judged on a white page is a tap judged in the wrong room: these are 64 px objects
    that will only ever be seen against a near-black bar top, so the contact sheet puts
    them there.
    """
    from PIL import ImageDraw
    counter = Image.open(os.path.join(HERE, 'AssetPipeline', 'sources', 'pixellab_user',
                                      'backba-opened-png.png')).convert('RGBA')
    slab = counter.crop((0, 112, counter.width, 178))     # the slab and its magenta edge
    pad, scale = 26, 4
    cells = []
    for i, key in enumerate(sorted(TAPS), 1):
        p = os.path.join(RAW, key + '.png')
        if not os.path.exists(p):
            continue
        im = Image.open(p).convert('RGBA')
        cells.append((str(i), im))
    if not cells:
        sys.exit('nothing fetched yet')
    w = pad + len(cells) * (SIZE * scale + pad)
    h = pad + 30 + SIZE * scale + slab.height + pad
    out = Image.new('RGBA', (w, h), (20, 18, 26, 255))
    # The slab is STRETCHED to the sheet's width rather than tiled. Tiling a 638 px sprite
    # across an 872 px sheet left a visible seam and a notch mid-frame, which is a flaw in
    # the contact sheet reading as a flaw in the art - the one thing a contact sheet must
    # not do. The slab is a flat band with a horizontal edge line, so stretching it costs
    # nothing.
    out.alpha_composite(slab.resize((w, slab.height), Image.NEAREST),
                        (0, pad + 30 + SIZE * scale - 8))
    d = ImageDraw.Draw(out)
    for i, (label, im) in enumerate(cells):
        x = pad + i * (SIZE * scale + pad)
        d.text((x, pad + 6), 'TIER ' + label, fill=(240, 234, 222, 255))
        big = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
        out.alpha_composite(big, (x + (SIZE * scale - big.width) // 2,
                                  pad + 30 + SIZE * scale - big.height - 8))
    out.convert('RGB').save(os.path.join(RAW, '_taps.png'))
    print('wrote _taps.png', out.size)


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'status'
    if cmd == 'queue':
        queue(only=set(sys.argv[2:]) or None)
    elif cmd == 'mouths':
        queue_mouths(only=set(sys.argv[2:]) or None)
    elif cmd == 'fetch':
        fetch()
    elif cmd == 'sheet':
        sheet()
    else:
        print(json.dumps(load(), indent=1))
