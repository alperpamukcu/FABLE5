# -*- coding: utf-8 -*-
"""The wall TV's advertisements (2026-09-04, the author: "Televizyon icinde
gozukecek animasyonlar olustur ... her reklamdan sonra televizyon kapanacak").

Only the AD PICTURES are generated. The cabinet, the CRT's shut-down and its
warm-up are DERIVED in Tools/tv_build.py from these plates - the rule this
project has paid for three times (see memory open-states-derive): a separately
generated "off" screen comes back a different television.

Budget: create_image_pixflux is ONE generation per call, takes a forced palette
(color_image_base64) and caps at 400px a side - which is why it is the tool
here and not create_image_pro (20-40 a call, and the ads are 64x40).

Art rules that are NOT optional (memory art-direction-rules):
  * no baked light, glow, bloom, reflection or cast shadow - URP 2D lights the
    room, and the CRT's own glow is a Light2D the stage hangs (tv_build writes
    none into the pixels);
  * flat matte local colour, form from the palette's own ramps;
  * the plate is generated AT SIZE, never painted big and shrunk.
Text is not asked for: the generator cannot spell (LAST CALL -> LAST COLL), so
every ad is a PICTURE and any wording is pressed on later in a pixel font.
"""
import base64, io, json, os, re, sys, time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import pixellab

RAW = os.path.join(HERE, 'tv_ads_raw')
STATE = os.path.join(HERE, 'tv_ads_state.json')
PALETTE = os.path.join(HERE, 'palette_miami.png')

# The screen's own pixels. 64x40 is the glass inside an 80x60 cabinet; both
# axes divisible by 4 (a create call that is not fails at GET, never at POST).
W, H = 64, 40

# One shared tail so every ad reads as the same broadcast rather than four
# unrelated pictures. Kept short: the description caps at 2000 characters and
# the "no X" queue is the first thing that gets cut.
STYLE = ("1980s Miami vice pixel art advertisement on a bar television, "
         "flat matte colours, hard pixel edges, ordered dither, "
         "bold simple shapes readable at a tiny size, filling the whole frame, "
         "no text, no letters, no words, no writing, "
         "no glow, no bloom, no reflections, no cast shadows, no rim light, "
         "flat even ambient lighting")

ADS = [
    ('cocktail', "a tall neon pink cocktail glass with a green olive on a stick, "
                 "centred on a deep teal background with a sunburst behind it"),
    ('flamingo', "a pink flamingo standing on one leg in front of a big orange "
                 "setting sun, teal water below, palm silhouette at the side"),
    ('palmcar',  "a white convertible car driving past two tall palm trees "
                 "toward an orange and pink sunset horizon"),
    ('beer',     "a full golden beer glass with a thick white foam head, "
                 "centred on a purple background with simple rising bubbles"),
]


def _b64(path):
    with open(path, 'rb') as f:
        return base64.b64encode(f.read()).decode('ascii')


def _load_state():
    if os.path.exists(STATE):
        return json.load(io.open(STATE, encoding='utf-8'))
    return {}


def _save_state(st):
    with io.open(STATE, 'w', encoding='utf-8') as f:
        json.dump(st, f, indent=1, ensure_ascii=False)


def _job_id(msgs):
    """The id off the first 'id: <uuid>' line. Matched as a WHOLE line, because
    the reply also carries a hint line with the same uuid inside it and a loose
    'id' in line test picks that one up as well."""
    for m in msgs:
        for c in ((m.get('result') or {}).get('content') or []):
            if c.get('type') != 'text':
                continue
            for line in c['text'].splitlines():
                mt = re.match(r'^id:\s*([0-9a-fA-F-]{36})\s*$', line)
                if mt:
                    return mt.group(1)
    return None


def queue():
    """One pixflux call per ad = one generation per ad. Ids land in the state
    file BEFORE anything is fetched, so a dropped connection costs no credits."""
    assert W % 4 == 0 and H % 4 == 0, 'both axes must divide by 4'
    pal = _b64(PALETTE)
    st = _load_state()
    for name, subject in ADS:
        if st.get(name, {}).get('job_id'):
            print('skip (already queued):', name)
            continue
        desc = subject + ', ' + STYLE
        assert len(desc) <= 2000, (name, len(desc))  # over 2000 => job never records
        msgs = pixellab.call('create_image_pixflux', {
            'description': desc,
            'width': W, 'height': H,
            'no_background': False,          # a screen is a scene, not a cut-out
            'color_image_base64': pal,       # the 40 Miami colours, forced
            'shading': 'flat shading',
            'detail': 'low detail',
            'outline': 'selective outline',
            'text_guidance_scale': 9.0,
        })
        job = _job_id(msgs)
        st[name] = {'job_id': job, 'desc': desc}
        _save_state(st)
        print(name, '->', job)


def fetch():
    """Pull every finished plate into tv_ads_raw/."""
    if not os.path.isdir(RAW):
        os.makedirs(RAW)
    st = _load_state()
    for name, _ in ADS:
        job = st.get(name, {}).get('job_id')
        if not job:
            print('no job for', name); continue
        out = os.path.join(RAW, name + '.png')
        if os.path.exists(out):
            print('have', name); continue
        msgs = pixellab.call('get_image', {'job_id': job})
        url = None
        for m in msgs:
            for c in ((m.get('result') or {}).get('content') or []):
                if c.get('type') == 'text' and 'http' in c['text']:
                    for tok in c['text'].split():
                        if tok.startswith('http'):
                            url = tok.strip().rstrip(',)')
        if not url:
            print('not ready:', name); continue
        data = pixellab.fetch_url(url)
        with open(out, 'wb') as f:
            f.write(data)
        print('wrote', out, len(data), 'bytes')


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'queue'
    {'queue': queue, 'fetch': fetch}[cmd]()
