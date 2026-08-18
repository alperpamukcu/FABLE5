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
        "stepping along each material's own colour ramp, ordered 2x2 dither where a "
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

COUNTER_BASE = ('below the top a graphite cabinet base: cabinet faces #383D45, '
                'recessed panel insets #24272D, thin lit rims #545A64, outline '
                '#14161A, small brass door handles #C9822B, two glass-door '
                'fridge sections with frames #383D45, dark glass #24272D, wire '
                'racks #545A64 and a very faint deep-teal interior light '
                '#123B45, and one open shelf niche with interior #24272D and '
                'shelf edges #545A64')

# THE CALM LITANY (2026-08-18, after the homework pass). Every plate prompt ends in
# this. It is 14 SS7b said as generation words, plus the doctrine the world examples
# agree on (VA-11 Hall-A, Coffee Talk, PC-98 interiors; Slynyrd's band construction):
# a room is 3-5 flat horizontal value bands, saturation is light rather than paint,
# detail lives in clusters of at least 2x2, and nothing painted glows.
CALM = ("flat matte local colour, big readable colour shapes, minimum detail "
        "cluster 2x2 px, clean 1px outlines in each material's darkest tone, "
        'no anti-aliasing, no dithering, no specular highlights, no '
        'reflections, no cast shadows, no rim light, no glow, no bloom, '
        'even neutral daylight, no people, no text, no signage')

# The room shell, said as SS5a's per-object law with SS5b's WISH composition (the
# window punched in the back wall - the straight-on stage cannot use the left-wall
# perspective shopfront the third batch painted). Frame budget per the doctrine
# pass: quiet ramps own the frame, the only chroma is the navy window frame.
ROOM_DESC = (
    'pixel art, flat straight-on front elevation of an EMPTY bar interior, a '
    'theatrical stage backdrop with no perspective and no vanishing point, '
    'built from big flat horizontal bands. Ceiling band: matte plaster '
    '#9C8F80 with a thin cornice line #6E6459 and two slim graphite air '
    'ducts #383D45 with top edge #545A64 and straps #24272D. Back wall '
    'band: matte cream plaster #C9BCA8 with sparse lighter patches #F2E8D5, '
    'faint shade #9C8F80 and two hairline cracks #6E6459. Centered on the '
    'back wall one large rectangular window, navy painted wood frame '
    '#2E4699 with bevel #4467CC, shadow side #1F2E66 and outline #131B3D, '
    'holding three tall window panes each filled with FLAT solid pure green '
    '#00FF00, separated by thin navy mullions. The right third of the back '
    'wall is exposed bordeaux brick: brick field #7E3130, shadowed courses '
    '#5C2226, sparse lit brick faces #9C4740, mortar lines #38161A. Where '
    'wall meets floor a graphite baseboard #383D45 with top edge #545A64. '
    'Floor band filling the bottom third: espresso wood planks in straight '
    'horizontal rows, plank faces alternating #241830 and #1A1023, sparse '
    'short grain ticks #362447, seam lines #0D0813. NO furniture, NO '
    'window view, ' + CALM)

T1_DESC = (
    'pixel art, straight-on front elevation of one long straight bar '
    'counter running the full image width, isolated on a transparent '
    'background, nothing above and nothing behind the counter top. Flat '
    'oak top slab: top field #8F5A1E with sparse straight grain lines '
    '#4A2E14 and #C9822B, front edge band #4A2E14, the top surface line '
    'dead straight and horizontal across the entire width, no foot rail. '
    + COUNTER_BASE + '. NO wall, NO floor, NO room, NO bottles, NO '
    'glasses, ' + CALM)

T2_DESC = (
    'pixel art, straight-on front elevation of one long straight bar '
    'counter running the full image width, isolated on a transparent '
    'background, nothing above and nothing behind the counter top. Flat '
    'white marble top slab: top field #C9BCA8, thin 1px veins #F2E8D5 and '
    '#9C8F80 as sparse short runs never dense fields, front edge #9C8F80, '
    'the top surface line dead straight and horizontal across the entire '
    'width, one slim steel foot rail #545A64 with a single 1px light line '
    '#808893. ' + COUNTER_BASE + '. NO wall, NO floor, NO room, NO '
    'bottles, NO glasses, ' + CALM)

T3_DESC = (
    'pixel art, straight-on front elevation of one long straight bar '
    'counter running the full image width, isolated on a transparent '
    'background, nothing above and nothing behind the counter top. Flat '
    'navy marble top slab: top field #1F2E66 with subtle flat mottling '
    '#131B3D and #2E4699, thin 1px pale veins #C9BCA8 and a few short '
    'gold vein ticks #E8A33D as sparse short runs never dense fields, '
    'polished front edge #2E4699, outline #131B3D, the top surface line '
    'dead straight and horizontal across the entire width. Along the '
    'front lip one slim brass rail: body #C9822B, upper face #E8A33D, a '
    'single 1px pale line #F2E8D5. ' + COUNTER_BASE + '. NO wall, NO '
    'floor, NO room, NO bottles, NO glasses, ' + CALM)


# Second room take (same sitting): the first four seeds all put the wall-floor line at
# y~255-285, because the prompt said "bottom third" - and the FIXTURE SLOTS are the real
# constraint. Wall pieces (sconce, neon) hang at art y~138, floor tables stand at y~231:
# the one line that serves both is the vertical MIDDLE, which is also what the shipped
# room measures (as-built y=181), so no slot in fixtures.json moves. The wish table's
# y~130 would put the sconces on the floor; it is the wish that bends, not the data.
ROOM_DESC2 = ROOM_DESC.replace(
    'Where '
    'wall meets floor a graphite baseboard #383D45 with top edge #545A64. '
    'Floor band filling the bottom third: espresso wood planks in straight '
    'horizontal rows',
    'Where wall meets floor a graphite baseboard #383D45 with top edge '
    '#545A64, and this wall-floor line runs dead straight and horizontal '
    'across the full width at exactly HALF the image height. Floor band '
    'filling the entire lower half of the image: espresso wood planks in '
    'straight horizontal rows'
).replace(
    'Centered on the back wall one large rectangular window',
    'Centered on the back wall, its sill resting just above the wall-floor '
    'line at half height, one large rectangular window'
).replace(
    'two hairline cracks #6E6459',
    'only a few faint small wear stains and two hairline cracks #6E6459, '
    'mostly clean flat plaster')


# THE MIAMI SHELL (2026-08-18, the author: "genis tavanli baska bir ic mekan yarat,
# brick kurali nerden geliyorsa ordan gelen kurallari dikkate alma. Tema Miami
# club/bar"). SS5a's cream-and-brick material table is deliberately SET ASIDE for this
# room - that is the author's call and it is scoped: the shell/prop split, the green
# key, the palette law, the calm litany and the half-height wall-floor line all stand,
# because none of them come from the mockup sitting the brick came from. Miami is said
# the way the world references say it (VA-11 Hall-A doctrine): DARK architecture in the
# palette's own plum and navy, cream art-deco trim, and the loud ramps only as thin
# deco lines - the neon, the palms and the sunset arrive later as props and the window
# plate, not as wall paint. The GRAND CEILING is the ask: the top quarter of the frame
# is ceiling, which reads as height precisely because the wall band stays short.
MIAMI_DESC = (
    'pixel art, flat straight-on front elevation of an EMPTY Miami art-deco '
    'night club interior with a GRAND HIGH CEILING, no perspective and '
    'no vanishing point, built from big '
    'flat horizontal bands. Ceiling band filling the entire top quarter of '
    'the image: deep plum night #1A1023, a stepped art-deco cove of three '
    'thin horizontal lines #362447 #4A3160 #362447 where ceiling meets '
    'wall, two long slim recessed light troughs drawn dark and unlit '
    '#241830 with edges #0D0813. Wall band: deep navy flat panels #1F2E66 '
    'separated by wide flat pilasters #131B3D with thin vertical deco '
    'fluting lines #2E4699, one continuous cream deco chair-rail line '
    '#C9BCA8 running the full width, and above it a thin stepped deco '
    'accent line in muted magenta #8F2464 and one in deep teal #123B45, '
    'thin 1-2px lines only never fields. Below the chair rail a dark navy '
    'wainscot #131B3D with flat recessed panel insets #1F2E66. Centered on '
    'the back wall, its sill resting just above the wall-floor line at '
    'exactly HALF the image height, one wide rectangular window with a '
    'rounded-corner cream art-deco frame #C9BCA8, frame shadow #9C8F80, '
    'outline #453E38, holding three tall panes each filled with FLAT solid '
    'pure green #00FF00 separated by thin cream mullions. The wall-floor '
    'line runs dead straight and horizontal at exactly half the image '
    'height over a graphite baseboard #383D45 with top edge #545A64. Floor '
    'band filling the entire lower half: dark polished terrazzo, field '
    '#241830 with large flat tone patches #1A1023, sparse small 2x2 px '
    'stone flecks #362447 and #4A3160, one straight cream deco border '
    'line #9C8F80 running horizontally near the wall. NO furniture, NO '
    'palm trees, NO neon signs, ' + CALM)


# THE 2.5D MIAMI SHELL (2026-08-19, the author, with the reference image in hand:
# "2.5D olmali bar arkaplani, ve biraz daha detayli olabilir. club_miami_b'nin
# renklerini duvarlarini zeminin ve catisini begendim"). The flat elevation gives way
# to the AS-BUILT box perspective the code was already re-measured for on 08-18 - a
# central vanishing point, flat back wall, receding side walls, visible ceiling and
# floor planes, the window down the LEFT wall - carrying club_miami_b's materials:
# plum ceiling, navy deco panels and pilasters, cream trim, one magenta line, dark
# terrazzo. "Biraz daha detayli" is honoured inside the doctrine: more visible joints,
# moldings and seams, still in clusters of 2x2 or better, still no baked light.
MIAMI25_DESC = (
    'pixel art, interior of an EMPTY Miami art-deco night club in mild '
    '2.5D box perspective: one-point perspective with a single central '
    'vanishing point, a flat back wall parallel to the picture plane, the '
    'left wall, right wall, ceiling plane and floor plane all receding '
    'gently toward it, GRAND HIGH CEILING. Ceiling plane: deep plum night '
    '#1A1023 with a stepped deco cove #362447 #4A3160 along its edges, '
    'two long recessed vent troughs #241830 edged #0D0813, three small '
    'round recessed downlight discs #C9BCA8, unlit. Walls: deep navy flat '
    'panels #1F2E66 with visible panel joint lines #131B3D, wide flat '
    'pilasters #131B3D with thin vertical deco fluting #2E4699, a '
    'continuous cream deco chair-rail line #C9BCA8, one thin stepped '
    'muted magenta deco line #8F2464 above it, and below a dark navy '
    'wainscot #131B3D with recessed panel insets #1F2E66 and a cream cap '
    'line #9C8F80. The LEFT receding wall carries one tall shopfront '
    'window in the same perspective: rounded-corner cream art-deco frame '
    '#C9BCA8, frame shadow #9C8F80, outline #453E38, three tall panes '
    'each filled with FLAT solid pure green #00FF00 divided by thin '
    'cream mullions. The back wall meets the floor at just past half the '
    'image height. Floor plane: dark polished terrazzo #241830 with '
    'large flat tone patches #1A1023, sparse 2x2 px stone flecks #362447 '
    'and #4A3160, thin straight tile seam lines #0D0813 receding toward '
    'the vanishing point, a cream deco border line #9C8F80 along the '
    'wall base. Medium detail. NO furniture, NO palm trees, NO neon, '
    + CALM)

# The tier-2 counter, cut off by the frame (2026-08-19, the author: "tezgah olarak
# counter_t2_a kullanalim ama sahnenin en sagindan en soluna uzanmali") - t2_a
# measured 67-69px of empty margin at each side, so the ask is a full-bleed take,
# not surgery on a slab that was drawn with ends.
T2_DESC2 = T2_DESC.replace(
    'one long straight bar counter running the full image width, isolated '
    'on a transparent background, nothing above and nothing behind the '
    'counter top.',
    'one long straight bar counter that extends PAST both sides of the '
    'frame, cut off by the left image edge and by the right image edge, '
    'no visible counter ends, isolated on a transparent background, '
    'nothing above and nothing behind the counter top.')


def _plate(desc, seed, transparent):
    return dict(kind='image', tool='create_image_pro', seed=seed,
        args=dict(width=640, height=360, no_background=transparent,
                  description=desc),
        post='counter' if transparent else 'room')


# kind: image (create_image_pro / pixflux -> job id -> get_image)
#       object (create_1_direction_object -> object id -> get_object)
#
# The plates are queued as CANDIDATE SETS - same prompt, different seeds - so the
# author picks from a contact sheet instead of accepting whatever one roll gave
# (bottle-art rule: new assets go through an HTML report BEFORE the game).
ASSETS = {
    'club_room_a': _plate(ROOM_DESC, 41401, False),
    'club_room_b': _plate(ROOM_DESC, 41402, False),
    'club_room_c': _plate(ROOM_DESC, 41403, False),
    'club_room_d': _plate(ROOM_DESC, 41404, False),
    'club_room_e': _plate(ROOM_DESC2, 41405, False),
    'club_room_f': _plate(ROOM_DESC2, 41406, False),
    'club_room_g': _plate(ROOM_DESC2, 41407, False),
    'club_room_h': _plate(ROOM_DESC2, 41408, False),
    'club_miami_a': _plate(MIAMI_DESC, 41701, False),
    'club_miami_b': _plate(MIAMI_DESC, 41702, False),
    'club_miami_c': _plate(MIAMI_DESC, 41703, False),
    'club_miami_d': _plate(MIAMI_DESC, 41704, False),
    'club_miami25_a': _plate(MIAMI25_DESC, 41801, False),
    'club_miami25_b': _plate(MIAMI25_DESC, 41802, False),
    'club_miami25_c': _plate(MIAMI25_DESC, 41803, False),
    'club_miami25_d': _plate(MIAMI25_DESC, 41804, False),
    'counter_t2_b': _plate(T2_DESC2, 41505, True),
    'counter_t2_c': _plate(T2_DESC2, 41506, True),
    'counter_t2_d': _plate(T2_DESC2, 41507, True),
    'counter_t1_a': _plate(T1_DESC, 41501, True),
    'counter_t1_b': _plate(T1_DESC, 41502, True),
    'counter_t1_c': _plate(T1_DESC, 41503, True),
    'counter_t2_a': _plate(T2_DESC, 41504, True),
    'counter_t3_a': _plate(T3_DESC, 41601, True),
    'counter_t3_b': _plate(T3_DESC, 41602, True),
    'counter_t3_c': _plate(T3_DESC, 41603, True),
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
