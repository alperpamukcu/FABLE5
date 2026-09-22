# -*- coding: utf-8 -*-
"""The wall TV's adverts, second round (2026-09-22, the author's seventh list: "Televizyon reklamlarını güncelle
farklı reklamlar çıksın görsel olarak daha düzgün").

Eight candidates, drawn with create_image_pro (the first round's pixflux ads came back muddy at 64x40) and snapped
to the house palette by Tools/tv_build.py when a pick ships. Nothing here enters Assets: the author picks from
Tools/tv_ads2 and the picks page, and only then does tv_build take them.

The rules of the first round hold (see Tools/tv_ads_gen.py): no text, no baked glow or reflection, flat matte
colour, drawn at size.

  py -3 -X utf8 Tools/tv_ads2_gen.py take      submit / collect (re-run until done)
"""
import io, json, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'tv_ads2')
STATE = os.path.join(OUT, 'state.json')
sys.path.insert(0, HERE)
import room_variants4_gen as r4    # noqa: E402  the shared PixelLab call

W, H = 64, 40
STYLE = ("1980s Miami pixel art television advertisement, the picture filling the whole screen edge to edge, "
         "bold simple shapes readable at a tiny size, flat matte colours, hard pixel edges, "
         "palette of flamingo pink, turquoise, coral, cream and sunset orange on deep plum, "
         "no text, no letters, no words, no logo, no glow, no bloom, no reflections, no cast shadows")

ADS = {
    'surf':      (8201, "a single surfboard standing upright in the sand in front of a big setting sun over the sea"),
    'speedboat': (8203, "a white speedboat cutting across turquoise water, a spray of foam behind it, palms on the shore"),
    'vinyl':     (8205, "a record turntable seen from above, a spinning pink vinyl record, neon purple background"),
    'tiki':      (8207, "a tropical cocktail served in a pineapple with a paper umbrella and a straw, teal background"),
    'dolphin':   (8209, "a dolphin leaping out of the sea in front of a full moon, night sky, calm water"),
    'skate':     (8211, "one pink roller skate with a lightning bolt on its side, on a checkered floor"),
    'lighthouse':(8213, "a striped lighthouse on rocks at dusk, pink sky, its beam a simple pale wedge"),
    'boombox':   (8215, "a chunky boombox sitting on a beach towel, palm tree shadow, bright sand"),
}


def _state():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def _save(s):
    os.makedirs(OUT, exist_ok=True)
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(s, indent=1))


def take():
    os.makedirs(OUT, exist_ok=True)
    s = _state()
    waiting = 0
    for key, (seed, subject) in ADS.items():
        png = os.path.join(OUT, key + '.png')
        if os.path.exists(png):
            continue
        e = s.get(key) or {}
        if e.get('job'):
            text, images = r4._call('get_image', {'job_id': e['job']}, timeout=180)
            if images:
                io.open(png, 'wb').write(images[0]); print('  ->', key)
            else:
                waiting += 1
            continue
        text, images = r4._call('create_image_pro', {'description': subject + ', ' + STYLE,
                                                     'width': W, 'height': H, 'no_background': False, 'seed': seed})
        if images:
            io.open(png, 'wb').write(images[0]); print('  ->', key); continue
        m = re.search(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}', text, re.I)
        s.setdefault(key, {})['job'] = m.group(0) if m else None
        _save(s); waiting += 1
        print('  queued %s %s' % (key, m.group(0) if m else text.strip()[:80]))
    have = sum(1 for k in ADS if os.path.exists(os.path.join(OUT, k + '.png')))
    print('%d of %d drawn, %d still cooking' % (have, len(ADS), waiting))


if __name__ == '__main__':
    take()
