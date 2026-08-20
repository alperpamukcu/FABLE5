# -*- coding: utf-8 -*-
"""Compose the live button demo for the preview page (2026-08-21).

The author cannot open Unity on this machine, so the page has to answer the question Unity
would: does one sprite really serve every label? Static screenshots of a few sizes only
show that it worked for those few. So the page gets the nine-slice itself - the key cut
into its nine pieces as data URIs, reassembled in CSS by the browser, with real text on
top. Type a longer label and the button grows, exactly as the game would draw it.

CSS does this natively with border-image, which IS a nine-slice: `border-image-slice` takes
the same four numbers Unity's spriteBorder does. So the demo is not a simulation of the
mechanism - it is the same mechanism, running in the reader's browser.

Writes the fragment that ui_preview.src.html includes.
"""
import base64, io, json, os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'scene_cast_raw')
PEND = os.path.join(os.path.dirname(HERE), 'Assets', 'Art', 'Pending~')

BORDER = (18, 18, 18, 24)     # l, b, r, t  (Unity order)


def uri(path, scale=1):
    im = Image.open(path).convert('RGBA')
    if scale != 1:
        im = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
    buf = io.BytesIO()
    im.save(buf, 'PNG')
    return 'data:image/png;base64,' + base64.b64encode(buf.getvalue()).decode(), im.size


def main():
    out = {}
    for name, rel in (('key', 'ui/ui_key.png'),
                      ('key_down', 'ui/ui_key_down.png'),
                      ('icon', 'ui/ui_icon_beer.png'),
                      ('states', 'ui/_sheet_states.png'),
                      ('slice', 'ui/_sheet_slice_proof.png'),
                      ('room', 'background/room_v4.png'),
                      ('closed', 'counter/mockup_drawer_closed.png'),
                      ('open', 'counter/mockup_drawer_open.png'),
                      ('taps', 'taps/_sheet_mouths.png'),
                      ('taps200', 'taps/_sheet_200_compare.png')):
        p = os.path.join(PEND, rel)
        if os.path.exists(p):
            out[name], size = uri(p)
            print('%-9s %s' % (name, size))
    # CSS border-image takes top right bottom left; Unity's Vector4 is l,b,r,t.
    l, b, r, t = BORDER
    out['slice_css'] = '%d %d %d %d' % (t, r, b, l)
    io.open(os.path.join(RAW, 'ui_preview_data.json'), 'w', encoding='utf-8').write(
        json.dumps(out))
    print('border-image-slice:', out['slice_css'], ' (from Unity l,b,r,t = %d,%d,%d,%d)'
          % BORDER)


if __name__ == '__main__':
    main()
