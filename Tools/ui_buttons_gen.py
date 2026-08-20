# -*- coding: utf-8 -*-
"""Buttons in the arrow's style, sliceable to any label width (2026-08-21).

The author: "UI icin uretim yaptim. Bu tarz butonlar uret, butonlar icerisindeki yaziya
gore boyutu degisebilir olmali her seferinde yeni uretmemeliyiz."

THE SECOND SENTENCE IS THE WHOLE DESIGN, and it rules out the obvious reading of the
first. "A button per label" is not a set of buttons, it is a set of pictures of buttons:
change one word of copy and the art is wrong again. What the ask describes is a NINE-SLICE
- corners drawn once and never stretched, edges repeated along their own axis, centre
filled - so ONE sprite serves "OK" and "MAKE A DRINK" alike.

This project already works that way and has the machinery for it, which is why nothing new
is being invented here:
    Tools/market_borders.py       measures where a frame's detailed border ends
    PatronArtPostprocessor.cs     stamps that border onto the sprite at import
    Image.Type.Sliced             is what KeyPlate and TycoonHud already draw with
ChromeArt says it out loud at line 1124: "9-sliced, because '$8' and '+$105' are the same
object at two widths."

So the generation is ONE call. What it has to produce is not three buttons but one button
whose corners survive being cut away from its middle.

THE STYLE REFERENCE IS THE ARROW, CUT OUT OF THE SHUTTER. The author's standalone arrow was
pasted into chat and never landed on disk, and there is no way to pull an image out of a
message. The same arrow is drawn on backbar-kapak.png, so it is cropped from there and its
grey slat background keyed away - 23x29 px of pink fill, white inner rim, magenta outer rim,
which is exactly the palette and the outline language the buttons have to inherit. If the
standalone differs from the one on the shutter, this is the line that was wrong.

Run:  py ui_buttons_gen.py style | queue | fetch | slice
"""
import base64, io, json, os, re, sys, time
from PIL import Image
import pixellab

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SRC = os.path.join(HERE, 'AssetPipeline', 'sources', 'pixellab_user')
RAW = os.path.join(HERE, 'scene_cast_raw')
STATE = os.path.join(HERE, 'ui_buttons_state.json')
LOG = os.path.join(HERE, 'AssetPipeline', 'generation_log.jsonl')
UUID = re.compile(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}')

ARROW = os.path.join(RAW, '_arrow_style.png')

# 688x384 is one of the aspect-gated maxima the docs allow (16:9); a square 512 would give
# the buttons less width to show their edge treatment, and edge treatment is the only part
# of a button a nine-slice actually keeps.
W, H = 688, 384

DESC = (
    'a clean pixel-art UI button set for a bar game menu: wide rounded rectangular '
    'buttons with a bright pink face, a crisp white inner rim just inside the edge, and a '
    'dark magenta outer outline one pixel thick, flat matte fill with one gentle lighter '
    'band across the top of the face, square-cut pixel corners, no gradient, no glow, no '
    'bevel shading, no drop shadow, no text, no letters, no icons, no ornament'
)

# ── round two: RAISED buttons, two shape classes (2026-08-21) ───────────────
# The author asked for depth ("3 boyutlu olmali") and for both states, and answered the
# sizing question the right way round: widths come from the nine-slice, but SHAPES do not.
# A wide button squeezed to 40x40 keeps its corner radius while its body vanishes, so the
# square icon key is its own drawing. Two classes here, each nine-sliced within itself.
#
# COLOUR IS GIVEN ROOM. "renk konumlari degisebilir ama beyaz pembe magenta gibi renklere
# yakin olsun" - so the three tones are named and the model is left to place them, which
# is also how a raised button gets its depth: the same three colours, ordered light on top
# and dark underneath.
#
# BEVEL, NOT SHADING. The word is chosen carefully. "Three-dimensional" to an image model
# invites a rendered gradient, and this project has spent four rounds proving that baked
# light does not belong in art the game lights itself. A pixel-art bevel is geometry, not
# lighting: a lit top face, a dark bottom face, and a body between them.
DESC3D = (
    'a pixel-art UI button set for a bar game menu, drawn with real thickness: each button '
    'is a raised key seen slightly from above, with a bright pink top face, a crisp white '
    'highlight line along its top and left edges, a dark magenta shadow face along its '
    'bottom and right edges giving it visible depth, and a one-pixel dark magenta outline '
    'around the whole shape. Flat matte colour blocks with hard pixel steps, no soft '
    'gradient, no glow, no blur, no drop shadow on the ground, no text, no letters, no '
    'icons, no ornament'
)


def make_style():
    """Cut the arrow off the shutter and key its grey field away."""
    im = Image.open(os.path.join(SRC, 'backbar-kapak.png')).convert('RGBA')
    px = im.load()
    xs, ys = [], []
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a > 40 and r > 120 and r - g > 40 and b > g:
                xs.append(x); ys.append(y)
    if not xs:
        sys.exit('no arrow found on the shutter')
    bb = (min(xs) - 2, min(ys) - 2, max(xs) + 3, max(ys) + 3)
    crop = im.crop(bb).convert('RGBA')
    cp = crop.load()
    # Key the slats: anything that is not part of the arrow's own three tones goes. The
    # arrow is pink/white/magenta; the slats are neutral grey, so "is it neutral?" is a
    # sufficient test and does not need the exact hexes.
    for y in range(crop.height):
        for x in range(crop.width):
            r, g, b, a = cp[x, y]
            if a and max(r, g, b) - min(r, g, b) < 26:
                cp[x, y] = (0, 0, 0, 0)
    crop.save(ARROW)
    print('style plate %dx%d -> %s' % (crop.width, crop.height, ARROW))


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


SHEETS = {
    'sheet':   dict(desc='DESC',   seed=5501, elements=['button', 'button', 'button']),
    'sheet3d': dict(desc='DESC3D', seed=5601,
                    elements=['button', 'icon_button', 'button', 'icon_button']),
}


def queue(which='sheet3d'):
    st = load()
    if st.get(which, {}).get('id'):
        print('already queued', st[which]['id'])
        return
    if not os.path.exists(ARROW):
        make_style()
    cfg = SHEETS[which]
    args = dict(description=globals()[cfg['desc']], width=W, height=H,
                color_palette='bright pink, magenta and white',
                elements=cfg['elements'],
                no_background=True, seed=cfg['seed'],
                style_image_base64=base64.b64encode(io.open(ARROW, 'rb').read()).decode())
    msgs = pixellab.call('create_ui_asset', args, timeout=900)
    b = texts(msgs)
    m = UUID.search(b)
    # Keyed by SHEET NAME. The first version of this hard-coded 'sheet' here while the rest
    # of the function had already moved to `which`, so queuing the 3D sheet wrote its job
    # id over the flat sheet's. Nothing was lost - the flat sheet's art was already on disk
    # - but a state file that lies about which job is which is the kind of bug that costs a
    # regeneration later, so it is fixed rather than worked around.
    st[which] = {'id': m.group(0) if m else None}
    save(st)
    with io.open(LOG, 'a', encoding='utf-8') as f:
        f.write(json.dumps({'asset': which, 'tool': 'create_ui_asset',
                            'prompt': args['description'], 'job': st[which]['id'],
                            'event': 'queued' if m else 'queue-failed'}) + '\n')
    print('%s -> %s' % (which, st[which]['id'] or b[:300].replace('\n', ' ')))


def fetch(which='sheet3d'):
    st = load()
    jid = (st.get(which) or {}).get('id')
    if not jid:
        sys.exit('nothing queued')
    for _ in range(40):
        msgs = pixellab.call('get_ui_asset', {'ui_asset_id': jid}, timeout=300)
        ims, b = images(msgs), texts(msgs)
        if ims:
            for i, im in enumerate(ims):
                im.save(os.path.join(RAW, 'ui_%s_%d.png' % (which, i)))
            print('fetched %d image(s)' % len(ims))
            return
        if 'failed' in b.lower():
            print('FAILED', b[:300].replace('\n', ' '))
            return
        print(' pending...')
        time.sleep(25)


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'status'
    if cmd == 'style':
        make_style()
    elif cmd == 'queue':
        queue(sys.argv[2] if len(sys.argv) > 2 else 'sheet3d')
    elif cmd == 'fetch':
        fetch(sys.argv[2] if len(sys.argv) > 2 else 'sheet3d')
    else:
        print(json.dumps(load(), indent=1))
