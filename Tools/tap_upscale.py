# -*- coding: utf-8 -*-
"""The two chosen taps, redrawn at 200x200 (2026-08-21).

The author: "sana atacagim 2 gorselin 200x200 halde tekrardan uret kucugunun daha
detaylisi olsun baska bir sey ekleme degistirme. Sadece 2 adet gorsel uret."

WHY edit_image AND NOT create_1_direction_object. Reading the tool list properly - 91
tools, which is what the author asked for - turns up the right one:

    edit_image: "returns YOUR image, edited - the pose, composition and pixel style are
    preserved and only what you asked for changes", with `width`/`height` for the output.

That is the whole brief in one sentence. Generating a new object at 200 would produce a
DIFFERENT tap that happens to look similar; editing the existing one keeps the drawing
and raises its resolution, which is what "ayni tasarim, sadece daha detayli" means.

Two facts from the same docs that shape this file:
  - inputs are capped at 512x512 (the 64x64 sources are fine) and the frame grid is
    limited by OUTPUT size: 16 frames at <=64, 9 at <=80, 4 at <=128, and ONE above that.
    At 200 that is one image per call, so two taps is exactly two calls - the author's
    budget, not a coincidence to be talked around.
  - cost is billed per call by the whole grid, so batching is impossible here anyway.

A MISTAKE OF MINE THE DOCS ALSO CORRECT: create_1_direction_object does take a style
reference, and it was my parameter name that was wrong - `style_images`, a list of
{"base64", "format"}, not `style_image_base64`. It would not have helped here (the docs
say style_images cannot be combined with `size`, and size is the entire point of this
round) but the earlier note that the tool "has no style image support" was wrong and is
corrected here rather than left standing.

Run:  py tap_upscale.py queue    then    py tap_upscale.py fetch
"""
import base64, io, json, os, re, sys, time
from PIL import Image
import pixellab

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'scene_cast_raw')
STATE = os.path.join(HERE, 'tap_upscale_state.json')
LOG = os.path.join(HERE, 'AssetPipeline', 'generation_log.jsonl')
UUID = re.compile(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}')

OUT_SIZE = 200

# The instruction. Every clause here is a FENCE, not a request: the author said "baska bir
# sey ekleme degistirme", so the only thing being asked for is resolution and the shading
# that resolution allows. The words are chosen against this project's own scar tissue -
# "ornate", "decorated", "turned" and their relatives have each dragged in a material or a
# keyline nobody asked for, so none of them appear.
DETAIL = (
    'redraw this exact same object at higher resolution with finer pixel detail: keep the '
    'identical design, identical silhouette, identical proportions, identical colours and '
    'the identical number of spouts and handles. Add no new parts, remove no parts, change '
    'no shapes. The extra resolution goes only into smoother shading steps along the same '
    'gold ramp, cleaner curves on the same edges, and finer definition of the parts that '
    'are already there. No new decoration, no engraving, no pattern, no text, no logo, '
    'no background, keep the transparent background'
)

# -- round two on the three-mouth alone (2026-08-21) ------------------------
# The author pointed at the 64 px three-mouth again and asked for it once more, at 200,
# "natural kontras olsun". So this is a fresh attempt from the SAME source rather than a
# refinement of the first 200 - "tekrardan uret" is generate again, not tidy up.
#
# WHAT "NATURAL CONTRAST" MEANS FOR THIS SPRITE, and it is not a mood. The source carries
# a hard black keyline - measured at 31% of its pixels - and the first 200 only knocked it
# to 10%. A black rim around a gold object is the opposite of natural contrast: it is the
# harshest jump available, and it is why this tap has never sat beside the one-mouth one,
# which has none. So the clause added here asks for the outline in the object's own
# darkest gold, which is 14 SS3's standing rule as well as what the eye wants.
#
# Named rather than done quietly, because the author's earlier instruction was "baska bir
# sey ekleme degistirme". Removing the black IS a change. It is the change "natural
# kontras" asks for as I read it - but if the black rim was wanted, this is the line to
# say so about.
NATURAL = (
    " Use natural gentle contrast: shading steps close together along the gold ramp with "
    "no harsh jumps, no pure black anywhere, and the outline drawn in the object's own "
    "darkest gold tone rather than in black."
)

JOBS = {
    'tap_gold_1_200': ('tap_gold_1.png', 7101),
    'tap_gold_3mouth_200': ('tap_gold_3mouth.png', 7103),
    'tap_gold_3mouth_200b': ('tap_gold_3mouth.png', 7203),
}
NATURAL_FOR = {'tap_gold_3mouth_200b'}


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
    out = []
    for m in msgs:
        for c in ((m.get('result') or {}).get('content') or []):
            if c.get('type') == 'image':
                out.append(Image.open(io.BytesIO(base64.b64decode(c['data']))).convert('RGBA'))
    return out


def queue():
    st = load()
    for key, (src, seed) in JOBS.items():
        if st.get(key, {}).get('id'):
            print('%-20s already queued' % key)
            continue
        p = os.path.join(RAW, src)
        if not os.path.exists(p):
            sys.exit('missing source: ' + p)
        b64 = base64.b64encode(io.open(p, 'rb').read()).decode()
        desc = DETAIL + (NATURAL if key in NATURAL_FOR else '')
        msgs = pixellab.call('edit_image', dict(
            images_base64=[b64], description=desc,
            width=OUT_SIZE, height=OUT_SIZE, seed=seed), timeout=900)
        b = texts(msgs)
        m = UUID.search(b)
        st[key] = {'id': m.group(0) if m else None, 'src': src, 'seed': seed}
        save(st)
        with io.open(LOG, 'a', encoding='utf-8') as f:
            f.write(json.dumps({'asset': key, 'tool': 'edit_image', 'source': src,
                                'seed': seed, 'prompt': desc, 'job': st[key]['id'],
                                'event': 'queued' if m else 'queue-failed'}) + '\n')
        print('%-20s -> %s' % (key, st[key]['id'] or b[:220].replace('\n', ' ')))
        time.sleep(0.6)


def fetch():
    st = load()
    for _ in range(40):
        pending = [k for k in JOBS if (st.get(k) or {}).get('id')
                   and not os.path.exists(os.path.join(RAW, k + '.png'))]
        if not pending:
            break
        moved = False
        for key in pending:
            msgs = pixellab.call('get_image', {'job_id': st[key]['id']}, timeout=300)
            ims, b = images(msgs), texts(msgs)
            if ims:
                ims[0].save(os.path.join(RAW, key + '.png'))
                print('fetched %-20s %s' % (key, ims[0].size))
                moved = True
            elif 'failed' in b.lower():
                print('FAILED %-20s %s' % (key, b[:200].replace('\n', ' ')))
                st[key]['id'] = None
                save(st)
                moved = True
        if not moved:
            print(' %d pending...' % len(pending))
            time.sleep(25)


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'status'
    if cmd == 'queue':
        queue()
    elif cmd == 'fetch':
        fetch()
    else:
        print(json.dumps(load(), indent=1))
