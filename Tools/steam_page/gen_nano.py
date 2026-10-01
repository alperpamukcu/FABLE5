# -*- coding: utf-8 -*-
"""Painted key art for the Steam page through Google's Gemini image model ("Nano Banana").

The model paints the SCENE only. Roxy's own sprite goes in as a reference picture so every capsule shows the same
woman (wavy red hair, burgundy halter jumpsuit with black lapels, gold hoops). The MALIBU CLUB sign is NEVER drawn
by the model: it is painted from the shipped logo maps (logo_render.py) and laid on top, so the logo stays the game's
own, letter for letter.

    GEMINI_API_KEY=...  python3 Tools/steam_page/gen_nano.py [asset ...] [--takes 3]
        -> Tools/steam_page/out/nano/raw/<asset>_<n>.png   (what the model returned, untouched)
        -> Tools/steam_page/out/nano/<asset>_<n>.png       (cropped to Steam's size, logo laid on)

GEMINI_MODEL picks the model (default gemini-2.5-flash-image). Each take is one paid request; pick the best take
by eye and copy it over the shipping name. Nothing random is seeded on our side: a rerun gives new takes.
"""
import argparse, base64, io, json, os, sys, time, urllib.request
from PIL import Image
import scene as S
import compose as K

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'out', 'nano')
MODEL = os.environ.get('GEMINI_MODEL', 'gemini-2.5-flash-image')
URL = 'https://generativelanguage.googleapis.com/v1beta/models/%s:generateContent' % MODEL

STYLE = ("Premium high-detail hi-bit pixel art illustration (crisp pixels, rich dithering, cinematic lighting), the "
         "look of a top-selling indie game's Steam key art. Palette: deep night violet, hot magenta, neon cyan, "
         "sunset amber and warm cream. Miami beach cocktail club at dusk, neon signs, "
         "palm silhouettes, a big striped synthwave sun over the sea. Neon signs are pictures only (a palm, a cocktail "
         "glass, a flamingo, a wave), never words. ABSOLUTELY NO text, letters, numbers, logos, watermark or UI "
         "anywhere in the picture.")
ROXY = ("ROXY, the hostess, exactly as in the reference sprite: a woman of 28 with long wavy dark-red hair, warm tan "
        "skin, gold hoop earrings, a burgundy sleeveless halter jumpsuit with black satin lapels and a gold belt, wide "
        "legs, black heels. Confident friendly smile, looking straight at the viewer.")

# asset: (size, model aspect, where the logo goes or None, prompt)
ASSETS = {
    'main_capsule': ((1232, 706), '16:9', ('left', 0.52, 0.04),
        "Wide shot inside the bar, seen from behind the counter. %s She stands on the right third, waist-up and large, "
        "holding a pink sugar-rimmed coupe cocktail at shoulder height. Behind her a glossy black bar counter with a "
        "magenta neon edge, bottles and glasses glowing, two or three customers on stools, and a huge window onto the "
        "sunset sea. Keep the upper-left 50 percent width by 40 percent height calm and dark enough for a title."),
    'header_capsule': ((920, 430), '21:9', ('left', 0.58, 0.07),
        "%s Waist-up on the right side, holding up a customer's ID card toward the viewer with one hand and a coupe "
        "cocktail in the other. Neon bar interior at dusk, counter with bottles. Keep the left 55 percent quiet and "
        "dark for a title."),
    'small_capsule': ((462, 174), '21:9', ('left', 0.62, 0.10),
        "%s Head and shoulders only, small, tucked into the far right edge (she fills only the right 28 percent of "
        "the width), lit by magenta and cyan neon. Simple dark violet bar background with soft bokeh. The left 70 "
        "percent is completely plain, dark and empty."),
    'vertical_capsule': ((748, 896), '4:5', ('top', 0.92, 0.03),
        "Tall portrait. %s Full figure from the knees up, centred, holding a coupe cocktail, standing in front of the "
        "bar counter; behind her the giant striped sunset sun, palms and the sea through tall windows. Keep the top "
        "28 percent empty sky for a title."),
    'library_capsule': ((600, 900), '2:3', ('top', 0.94, 0.03),
        "Tall portrait. %s Full figure, centred, one hand on her hip, the other holding a coupe cocktail; the neon bar "
        "and sunset sea behind her. Keep the top 26 percent empty sky for a title."),
    'library_hero': ((3840, 1240), '21:9', None,
        "Very wide panoramic scene, no character close-up: the whole Malibu Club bar at dusk seen from behind the "
        "counter, a row of five varied customers on stools, %s standing at the right, neon signs, bottles, a sunset "
        "sea through a wall of windows. Keep the lower-left quarter darker (a logo goes there in the Steam library)."),
    'page_background': ((1438, 810), '16:9', None,
        "Atmospheric, low-contrast, dark background plate of an empty neon cocktail bar at night with a sea view; no "
        "people; soft, out of focus, mostly deep violet."),
    'event_header': ((1920, 622), '21:9', ('left', 0.45, 0.08),
        "%s On the right, waist-up, raising a coupe cocktail in a toast. Neon bar at dusk. Left half quiet and dark."),
    'event_cover': ((800, 450), '16:9', ('left', 0.56, 0.06),
        "%s Waist-up on the right, winking, coupe cocktail in hand, neon bar at dusk. Left half quiet and dark."),
    'gif_backdrop': ((1232, 720), '16:9', None,
        "An empty cocktail bar at dusk seen from behind the bar, eye level, perfectly straight-on and symmetrical: a "
        "long glossy black bar counter with a magenta neon edge runs across the bottom 22 percent of the picture, the "
        "counter top is completely empty; behind it, a wall of tall windows onto the sea with the striped sunset sun "
        "in the middle and palm silhouettes; a couple of neon picture-signs high on the side walls. No people, no "
        "bottles, no glasses, nothing standing on the counter."),
    'community_icon': ((184, 184), '1:1', None,
        "%s Close portrait of her face and shoulders on a magenta-to-amber sunset gradient, centred, bold readable "
        "silhouette for a small icon."),
}


def roxy_reference():
    """Her idle frame, enlarged 4x on a neutral ground, as the model's character sheet."""
    f = S.sprite('hostess', 'idle').crop((50, 0, 170, 216))
    ref = Image.new('RGBA', (f.width + 40, f.height + 20), S.C('Cream[3]'))
    ref.alpha_composite(f, (20, 10))
    buf = io.BytesIO()
    S.up(ref, 4).convert('RGB').save(buf, 'PNG')
    return base64.b64encode(buf.getvalue()).decode()


def ask(prompt, aspect, key, ref_b64):
    parts = [{'text': STYLE + '\n\n' + prompt}]
    if ref_b64:
        parts.insert(0, {'inline_data': {'mime_type': 'image/png', 'data': ref_b64}})
        parts.insert(1, {'text': 'Reference character sheet for ROXY (pixel sprite). Match her face, hair, outfit '
                                 'and colours exactly; render her at the new size and pose.'})
    body = {'contents': [{'parts': parts}],
            'generationConfig': {'responseModalities': ['IMAGE'], 'imageConfig': {'aspectRatio': aspect}}}
    if os.environ.get('GEMINI_IMAGE_SIZE'):          # '2K' / '4K' on models that take it (Nano Banana Pro)
        body['generationConfig']['imageConfig']['imageSize'] = os.environ['GEMINI_IMAGE_SIZE']
    req = urllib.request.Request(URL, json.dumps(body).encode(), {'Content-Type': 'application/json',
                                                                   'x-goog-api-key': key})
    with urllib.request.urlopen(req, timeout=180) as r:
        data = json.load(r)
    for c in data.get('candidates', []):
        for p in c.get('content', {}).get('parts', []):
            blob = p.get('inline_data') or p.get('inlineData')
            if blob:
                return Image.open(io.BytesIO(base64.b64decode(blob['data']))).convert('RGB')
    raise RuntimeError('no image in response: %s' % json.dumps(data)[:400])


def fit(img, size):
    """Cover-crop to Steam's exact size (centre), then LANCZOS - this is painted art, not the sprite grid."""
    W, H = size
    s = max(W / img.width, H / img.height)
    img = img.resize((round(img.width * s), round(img.height * s)), Image.LANCZOS)
    x, y = (img.width - W) // 2, (img.height - H) // 2
    return img.crop((x, y, x + W, y + H))


def lay_logo(img, where):
    side, frac, top = where
    W, H = img.size
    lg = K.logo_fit(int(W * frac))
    out = img.convert('RGBA')
    x = int(W * 0.03) if side == 'left' else (W - lg.width) // 2
    out.alpha_composite(lg, (x, int(H * top)))
    return out.convert('RGB')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('assets', nargs='*', default=list(ASSETS))
    ap.add_argument('--takes', type=int, default=2)
    ap.add_argument('--tag', default='', help='suffix for the take files, to keep a second model apart')
    a = ap.parse_args()
    key = os.environ.get('GEMINI_API_KEY')
    if not key:
        sys.exit('GEMINI_API_KEY is not set (add it to the environment settings).')
    os.makedirs(os.path.join(OUT, 'raw'), exist_ok=True)
    ref = roxy_reference()
    for name in a.assets:
        size, aspect, where, prompt = ASSETS[name]
        text = prompt % ROXY if '%s' in prompt else prompt
        for n in range(1, a.takes + 1):
            t0 = time.time()
            raw = ask(text, aspect, key, ref if '%s' in prompt else None)
            name_n = '%s%s_%d' % (name, a.tag, n)
            raw.save(os.path.join(OUT, 'raw', name_n + '.png'))
            img = fit(raw, size)
            if where:
                img = lay_logo(img, where)
            img.save(os.path.join(OUT, name_n + '.png'))
            print('%-18s take %d  %dx%d  %.0fs' % (name, n, size[0], size[1], time.time() - t0))


if __name__ == '__main__':
    main()
