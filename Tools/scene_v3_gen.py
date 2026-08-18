# -*- coding: utf-8 -*-
"""The v3 scene set through PixelLab (14 v3 SS11, 2026-08-17): the EMPTY room
shell, the three counter tiers, the three window plates and the first two props.
Queue, fetch, post-process, stage. Day masters only - night variants are EDITS
of approved masters and wait for approval (SS11.D; edit_image caps at 512 wide,
so the 640 masters will be relit in halves or graded in post, decided then).

Size law that shaped this file: create_image_pro allows 688x384 at 16:9, so the
room ships in ONE call at 640x360; the counters are drawn as isolated strips on
transparent (no_background) and cropped to 640x150 from the surface's own top
row; windows go through pixflux (max 400) at 160x72; props go through
create_1_direction_object (max 256, transparent by trade).

Commands:  balance | queue | fetch | post | status
State:     Tools/scene_v3_state.json      Raw: Tools/scene_v3_raw/
Staged:    Tools/AssetPipeline/staging/scene_v3/
Log:       Tools/AssetPipeline/generation_log.jsonl (15 SS5)
"""
import base64, io, json, os, re, sys, time
from PIL import Image
import pixellab

HERE = os.path.dirname(os.path.abspath(__file__))
STATE = os.path.join(HERE, 'scene_v3_state.json')
RAW = os.path.join(HERE, 'scene_v3_raw')
STAGE = os.path.join(HERE, 'AssetPipeline', 'staging', 'scene_v3')
LOG = os.path.join(HERE, 'AssetPipeline', 'generation_log.jsonl')

STYLE = ('clean 1px outlines in each material\'s darkest tone, flat shading, '
         'no anti-aliasing, no text, no people')

# The vice pass (the author, 2026-08-17, on the first backbar take: "renk paleti,
# keskinlik cok nizami" - too dark, too regimented). Brighter Miami tones, silver/
# gold/glass materials, and language that breaks the CAD-straight sterility. The
# quantize step still snaps every pixel to the 55, so the looseness costs no law.
# NO LIGHT IS PAINTED IN (2026-08-15, the author: "uretilen gorsellerde yansima ve
# isiklandirma olmamali, bunlarin hepsini unity icerisinde ekleyecegiz"). This string used
# to ASK for "glass reflections, soft dithered light gradients" -- the two things the room
# must not carry. The scene is lit in URP: a global light that shifts with the shift, a
# Light2D per fixture, and DiegeticStage.ContactShadow puts the contact shadow down. A
# highlight baked into the plate glows in a dark room, sits on the wrong side when the light
# moves, and reflects a source that is not there. Form comes from the RAMP's own steps.
VICE = ('subtle wear and uneven texture, flat matte local colour, form shaded only by '
        'stepping along each material's own colour ramp, ordered 2x2 dither where a '
        'surface must gradate, lively hand-pixelled detail, no specular highlights, '
        'no reflections, no cast shadows, no rim light, no glow, no bloom, '
        'even flat lighting, no text, no people')

# The 55 (UITheme.cs verbatim; 14 v3 SS3). Quantize maps every opaque pixel to
# its nearest of these; #00FF00 is keyed to alpha BEFORE quantize ever sees it.
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

COUNTER_BASE = ('graphite cabinet base #24272D with panel faces #383D45, '
                'brass handles #C9822B, glass fridge doors on the right with a '
                'faint cool interior light #123B45')

# kind: image (create_image_pro / pixflux -> job id -> get_image)
#       object (create_1_direction_object -> object id -> get_object)
ASSETS = {
    'club_room': dict(kind='image', tool='create_image_pro', seed=41301,
        args=dict(width=640, height=360, no_background=False, description=(
            'pixel art, EMPTY interior of a Miami vice cocktail lounge at '
            'golden hour, straight-on view, no furniture - warm cream plaster '
            'walls #C9BCA8 with a faint sunset-pink tint #FF7DC6 ONLY on the '
            'window-side edge, espresso wood plank floor #241830 with a violet '
            'plank step #362447, bordeaux brick wall on the right '
            '#9C4740 with deep mortar #38161A, silver-blue window frame '
            '#4467CC with polished chrome edges #808893 holding three flat '
            'chroma green #00FF00 panels, thin gold trim lines #E8A33D along '
            'the cornice, ' + VICE)),
        post='room'),
    'counter_t1': dict(kind='image', tool='create_image_pro', seed=41211,
        args=dict(width=640, height=360, no_background=True, description=(
            'pixel art, isolated bar counter strip running the full image '
            'width, viewed straight on, on a transparent background - plain '
            'oak top #8F5A1E with grain #4A2E14 and #C9822B, dead-straight '
            'level top surface, no foot rail, ' + COUNTER_BASE + ', ' + STYLE)),
        post='counter'),
    'counter_t2': dict(kind='image', tool='create_image_pro', seed=41212,
        args=dict(width=640, height=360, no_background=True, description=(
            'pixel art, isolated bar counter strip running the full image '
            'width, viewed straight on, on a transparent background - white '
            'marble top #C9BCA8 with thin veins #F2E8D5 and #9C8F80, '
            'dead-straight level top surface, one steel foot rail #545A64 '
            'with a light line #808893, ' + COUNTER_BASE + ', ' + STYLE)),
        post='counter'),
    'counter_t3': dict(kind='image', tool='create_image_pro', seed=41313,
        args=dict(width=640, height=360, no_background=True, description=(
            'pixel art, isolated luxurious bar counter strip running the full '
            'image width, viewed straight on, on a transparent background - '
            'royal navy marble top #2E4699 with lively irregular veins in '
            'gold #E8A33D, silver #808893 and cream #F2E8D5, glossy polished '
            'surface with faint pink #FF7DC6 and cyan #7DF0E3 reflections, '
            'dead-straight level top surface, one gold foot rail #E8A33D '
            'with bright glints #F5C97B, cabinet base in deep violet-charcoal '
            '#362447 with silver handles #808893 and glass door panels '
            'reflecting cool cyan light #3BC8BE, ' + VICE)),
        post='counter'),
    'backbar': dict(kind='image', tool='create_image_pro', seed=41315,
        args=dict(width=640, height=360, no_background=False, description=(
            'pixel art, grand back bar shelving wall of a Miami vice '
            'cocktail lounge, viewed straight on, EMPTY, built to hold '
            'dozens of bottles - open shelving spanning the ENTIRE wall '
            'edge to edge, four full-width shelf niches stacked from just '
            'above the ledge to the ceiling, only a thin polished silver '
            'pilaster #808893 at each far edge, niche interiors rich royal '
            'blue #1F2E66 softly lit from above with cyan #3BC8BE and '
            'magenta #E84DA6 glow gradients, long shelves of thick glass '
            'with bright cyan edge light #7DF0E3, gold shelf front edges '
            '#E8A33D with warm glints #F5C97B, deep violet #362447 wall '
            'crown with silver trim, at the bottom a royal navy marble '
            'ledge #2E4699 with lively gold #E8A33D and silver #808893 '
            'veins and gold trim, a narrow espresso wood floor strip '
            '#241830 at the very bottom, ' + VICE +
            ', no bottles, no barrels')),
        post='plain'),
    'window_day': dict(kind='image', tool='create_image_pixflux', seed=41221,
        args=dict(width=160, height=72, no_background=False, description=(
            'pixel art sky seen through a window, soft golden morning, warm '
            'amber #E8A33D fading up into pale cream #F2E8D5, faint distant '
            'palm silhouettes, flat shading, no anti-aliasing, no text')),
        post='plain'),
    'window_sunset': dict(kind='image', tool='create_image_pixflux', seed=41222,
        args=dict(width=160, height=72, no_background=False, description=(
            'pixel art vice sunset sky, hot magenta #E84DA6 through #C23283 '
            'down into deep purple night #362447, ordered 2x2 dither gradient, '
            'palm tree and city skyline silhouettes in dark purple #1A1023, '
            'no anti-aliasing, no text')),
        post='plain'),
    'window_night': dict(kind='image', tool='create_image_pixflux', seed=41223,
        args=dict(width=160, height=72, no_background=False, description=(
            'pixel art night sky over a dark sleeping city, deep purple-black '
            '#0D0813 to #241830, a few tiny lit windows in amber #E8A33D, '
            'near-black palm silhouettes, no anti-aliasing, no text')),
        post='plain'),
    'prop_platform': dict(kind='object', tool='create_1_direction_object', seed=41231,
        args=dict(size=192, view='sidescroller', description=(
            'low wide rectangular marble platform pedestal, white marble top '
            '#F2E8D5 shaded #C9BCA8 with sparse thin gold veins #E8A33D, '
            'dark warm outline #453E38')),
        post='prop'),
    'prop_shelf': dict(kind='object', tool='create_1_direction_object', seed=41232,
        args=dict(size=144, view='sidescroller', description=(
            'wall-mounted open wooden shelf unit with two empty shelves, '
            'golden oak #C9822B with lit front edges #E8A33D, shadowed dark '
            'interior #8F5A1E, deep brown outline #4A2E14')),
        post='prop'),
}

UUID = re.compile(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}')


def load():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def save(s):
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(s, indent=1))


def log(rec):
    rec['ts'] = time.strftime('%Y-%m-%dT%H:%M:%S')
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
    out = []
    for m in msgs:
        for c in ((m.get('result') or {}).get('content') or []):
            if c.get('type') == 'image':
                out.append(Image.open(io.BytesIO(base64.b64decode(c['data']))).convert('RGBA'))
    return out


def queue(only=None):
    st = load()
    for key, a in ASSETS.items():
        if only and key not in only:
            continue
        if st.get(key, {}).get('id'):
            continue
        args = dict(a['args'], seed=a['seed'])
        msgs = pixellab.call(a['tool'], args, timeout=900)
        body = texts(msgs)
        m = UUID.search(body)
        st[key] = {'id': m.group(0) if m else None, 'kind': a['kind']}
        save(st)
        log({'asset': key, 'tool': a['tool'], 'seed': a['seed'],
             'prompt': a['args']['description'], 'job': st[key]['id'],
             'event': 'queued' if m else 'queue-failed', 'raw': body[:300]})
        print('%-14s -> %s' % (key, st[key]['id'] or body[:120].replace('\n', ' ')))
        time.sleep(0.6)


def fetch():
    os.makedirs(RAW, exist_ok=True)
    st = load()
    pending = {k: v for k, v in st.items() if v.get('id')
               and not os.path.exists(os.path.join(RAW, k + '.png'))
               and not v.get('review')}
    for _ in range(80):
        if not pending:
            break
        moved = False
        for key, rec in sorted(pending.items()):
            if rec['kind'] == 'image':
                msgs = pixellab.call('get_image', {'job_id': rec['id']}, timeout=300)
            else:
                msgs = pixellab.call('get_object',
                                     {'object_id': rec['id'], 'include_preview': True},
                                     timeout=300)
            ims, body = images(msgs), texts(msgs)
            if ims:
                if len(ims) == 1:
                    ims[0].save(os.path.join(RAW, key + '.png'))
                else:  # review candidates: stage all, pick with select_object_frames
                    for i, im in enumerate(ims):
                        im.save(os.path.join(RAW, '%s_cand%d.png' % (key, i)))
                    rec['review'] = len(ims)
                    save(st)
                print('fetched', key, '(%d candidate%s)' % (len(ims), 's' if len(ims) > 1 else ''))
                log({'asset': key, 'event': 'fetched', 'candidates': len(ims)})
                moved = True
            elif 'failed' in body.lower():
                print('FAILED', key, body[:200].replace('\n', ' '))
                log({'asset': key, 'event': 'failed', 'raw': body[:300]})
                rec['id'] = None
                save(st)
                moved = True
        pending = {k: v for k, v in st.items() if v.get('id')
                   and not os.path.exists(os.path.join(RAW, k + '.png'))
                   and not v.get('review')}
        if pending and not moved:
            print(' %d pending...' % len(pending))
            time.sleep(25)
    print('missing:', sorted(pending) if pending else 'none')


# -- post-processing: key green -> quantize 55 -> shape per kind -> stage ------

def key_green(im):
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a and g > 160 and r < 90 and b < 90 and g - max(r, b) > 60:
                px[x, y] = (0, 0, 0, 0)
    return im


def quantize(im):
    pal = [((c >> 16) & 255, (c >> 8) & 255, c & 255) for c in PALETTE]
    px = im.load()
    cache = {}
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a < 128:
                px[x, y] = (0, 0, 0, 0)
                continue
            k = (r, g, b)
            if k not in cache:
                cache[k] = min(pal, key=lambda p: (p[0]-r)**2 + (p[1]-g)**2 + (p[2]-b)**2)
            q = cache[k]
            px[x, y] = (q[0], q[1], q[2], 255)
    return im


def content_rows(im):
    rows = [y for y in range(im.height)
            if any(im.getpixel((x, y))[3] >= 128 for x in range(0, im.width, 4))]
    return (rows[0], rows[-1]) if rows else (0, im.height - 1)


def post():
    os.makedirs(STAGE, exist_ok=True)
    for key, a in ASSETS.items():
        src = os.path.join(RAW, key + '.png')
        if not os.path.exists(src):
            continue
        im = Image.open(src).convert('RGBA')
        if a['post'] == 'room':
            im = quantize(key_green(im))
            assert im.size == (640, 360), im.size
        elif a['post'] == 'counter':
            im = quantize(key_green(im))
            top, bot = content_rows(im)
            strip = Image.new('RGBA', (640, 150), (0, 0, 0, 0))
            crop = im.crop((0, top, 640, min(top + 150, bot + 1)))
            strip.paste(crop, (0, 0))
            im = strip
        elif a['post'] == 'plain':
            im = quantize(im)
        elif a['post'] == 'prop':
            im = quantize(im)
            xs = [x for x in range(im.width)
                  if any(im.getpixel((x, y))[3] >= 128 for y in range(0, im.height, 2))]
            top, bot = content_rows(im)
            if xs:
                im = im.crop((xs[0], top, xs[-1] + 1, bot + 1))
        im.save(os.path.join(STAGE, key + '.png'))
        print('staged %-14s %dx%d' % (key, im.width, im.height))
        log({'asset': key, 'event': 'staged', 'size': [im.width, im.height]})


def status():
    st = load()
    for key in ASSETS:
        rec = st.get(key, {})
        raw = os.path.exists(os.path.join(RAW, key + '.png'))
        stg = os.path.exists(os.path.join(STAGE, key + '.png'))
        print('%-14s id=%s raw=%s staged=%s%s' % (
            key, (rec.get('id') or '-')[:8], raw, stg,
            ' review:%d' % rec['review'] if rec.get('review') else ''))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'status'
    if cmd == 'balance':
        pixellab.call('get_balance', {})
    elif cmd == 'queue':
        queue(only=set(sys.argv[2:]) or None)
    else:
        {'fetch': fetch, 'post': post, 'status': status}[cmd]()
