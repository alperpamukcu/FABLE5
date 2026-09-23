# -*- coding: utf-8 -*-
"""THE JUICE CARTONS, WITH THE FRUIT PRINTED IN (2026-09-23, the author's eighth list: "Meyve suyu
kutularının küçük hallerini tekrar üret, üzerindeki meyveleri sonradan ekleme, üretimde üzerinde meyve
olan farklı tasarımlara sahip aynı perspektifte meyve suyu kutuları üret. Küçük ve büyük boylarda.").

  py -3 -X utf8 Tools/v4_bottles/juice_fruit.py probe          one large take, and what it cost
  py -3 -X utf8 Tools/v4_bottles/juice_fruit.py take           every job not yet collected (resumes)
  py -3 -X utf8 Tools/v4_bottles/juice_fruit.py status         what is in hand

WHAT WAS WRONG WITH THE ONES IN THE GAME. The five cartons of 2026-09-08 were generated plain - flat
coloured card, a brand band - and the SHELF copies were then told which fruit they held by a 13-pixel
mark drawn by hand and pressed on afterwards (fruit_marks.py -> cellar_fruit.py). That was the right
answer to "you cannot tell the juices apart" at the time, and it is exactly the thing the author now
does not want: a fruit stuck on a carton rather than a carton designed around its fruit.

WHAT THIS DOES INSTEAD. The fruit is IN the prompt, as the carton's own print, so it comes out of the
generator drawn in the carton's own light and palette. Two DESIGNS a juice (different compositions of
the same fruit), two seeds a design, at the pour scene's 96x192. The perspective is held by showing the
generator the carton it is replacing - the author's picked take - as a silhouette-and-camera reference,
beside the house's style anchor and camera reference, so a new design does not come back as a new box.

THE SHELF SIZE, BOTH WAYS. The house rule is that the 32x64 cellar copy is DERIVED from the master and
never generated apart (GDD_MEVCUT 9.18 - both were tried and looked worse). The author asked for the
small size to be produced as well, so both are offered and the report puts them side by side: the
cellar copy derived from each new master by the pipeline's own cellar_box, and native 32x64 takes of
the same designs (the generator returns sixteen candidates a call at that size, so it is cheap).

Nothing lands in Assets. Raw takes go to raw/<id>/fruit_*.png; report.html is built from them; the
author picks; only ship.py touches the game (memory bottle-art-v3-respec).
"""
import io
import json
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import brief  # noqa: E402
import gen    # noqa: E402

RAW = os.path.join(HERE, 'raw')
STATE = os.path.join(HERE, 'juice_fruit_state.json')
LARGE = {'width': 96, 'height': 192}
SMALL = {'width': 32, 'height': 64}
SEEDS = (31, 37)

# id -> (brand word, card colour, the two designs). Each design is the PRINT on the front face: the
# fruit drawn big and plainly enough to read at the shelf's size, never a small logo.
JUICES = {
    'orange_grove': ('GROVE', 'flat orange card', (
        'a big illustrated whole orange with a green leaf, and a cut orange half showing its segments beside it, printed large across the front face',
        'a bold round orange slice like a sunburst, with a green leaf, printed large in the middle of the front face',
    )),
    'lemon_fresh': ('LEMONADE', 'flat pale yellow card', (
        'a big illustrated yellow lemon with a green leaf, and a cut lemon half beside it, printed large across the front face',
        'three round lemon slices in a row with a small green leaf, printed large across the front face',
    )),
    'lime_fresh': ('LIMEADE', 'flat green card', (
        'a big illustrated green lime with a leaf, and a cut lime half showing its pale green segments, printed large across the front face',
        'a large lime wedge with a few juice drops, printed large across the front face',
    )),
    'cranberry_north': ('NORTH', 'flat deep red card', (
        'a big cluster of shiny round red cranberries with two green leaves, printed large across the front face',
        'a scatter of red cranberries tumbling down the front face with a sprig of leaves, printed large',
    )),
    'pineapple_isla': ('ISLA', 'flat golden-yellow card', (
        'a big illustrated whole pineapple with its green spiky crown, printed large across the front face',
        'a large pineapple ring slice with a red cherry in its centre and a green leaf, printed large across the front face',
    )),
}

# ROUND TWO (2026-09-23, the author on the first round: "Tekrar üret, raf boyutu ile beraber. Portakal suyu Cappy
# markasının muadili gibi gözüksün" and then "Tüm meyvesuları gerçek meyvesuyu markalarının tasarımına benzesin").
# Each juice now wears a real juice brand's TRADE DRESS under a parody name, the way the house's spirits already do
# (SMIRKOFF, GANDER, MALIBOO, LOCA): the brand's layout, colours and kind of picture, described in words - the real
# name is never in the prompt, so what comes back is an homage and not the mark. Cappy is Minute Maid's European
# name and shares its identity (a black box with a white logotype over a green horizon arc, big glossy fruit and a
# splash); Simply is a plain white-and-colour carafe line; Ocean Spray is deep red with a white wave swoosh; Dole is
# yellow with a sunburst in the O.
BRANDS = {
    'orange_grove': ('KAPPY', (
        'a bright orange carton fading to white near the top; near the top of the front face a black rectangular logo box '
        'with the word "KAPPY" in bold white letters and a thin curved green horizon stripe right under the box; below it '
        'a big glossy realistic orange cut in half with fresh juice splashing around it and two green leaves',
        'a bright orange carton; near the top of the front face a black rectangular logo box with the word "KAPPY" in bold '
        'white letters and a thin curved green horizon stripe right under the box; below it a swirl of orange juice '
        'splashing round two glossy orange slices and a whole orange',
    )),
    'lemon_fresh': ('SIMPLE', (
        'a clean white carton with a pale yellow lower half; near the top of the front face the word "SIMPLE" in plain '
        'black serif capitals with the smaller word "lemonade" under it; below it a fresh cut lemon half and a lemon '
        'slice with a green leaf, simple and bright',
        'a clean white carton with a pale yellow lower half; near the top of the front face the word "SIMPLE" in plain '
        'black serif capitals with the smaller word "lemonade" under it; below it a single big lemon with its leaves on '
        'a pale yellow wash',
    )),
    'lime_fresh': ('SIMPLE', (
        'a clean white carton with a fresh green lower half; near the top of the front face the word "SIMPLE" in plain '
        'black serif capitals with the smaller word "limeade" under it; below it a cut lime half and a lime slice with '
        'a green leaf, simple and bright',
        'a clean white carton with a fresh green lower half; near the top of the front face the word "SIMPLE" in plain '
        'black serif capitals with the smaller word "limeade" under it; below it a single big lime with its leaves on a '
        'pale green wash',
    )),
    'cranberry_north': ('SEA SPRAY', (
        'a deep red carton; near the top of the front face a white curved wave swoosh with the words "SEA SPRAY" in white '
        'lettering on the red; below it a glass of dark red juice beside a cluster of shiny red cranberries with green '
        'leaves',
        'a deep red carton; near the top of the front face a white curved wave swoosh with the words "SEA SPRAY" in white '
        'lettering on the red; below it a big heap of shiny red cranberries splashing in red juice',
    )),
    'pineapple_isla': ('DOLA', (
        'a bright yellow carton; near the top of the front face the word "DOLA" in bold red letters with a small yellow '
        'sunburst inside the letter O; below it a big realistic pineapple with its green spiky crown and a pineapple slice',
        'a bright yellow carton; near the top of the front face the word "DOLA" in bold red letters with a small yellow '
        'sunburst inside the letter O; below it pineapple rings and chunks tumbling in golden juice',
    )),
}
ROUND = 1                      # set from the command line: 1 = the fruit round, 2 = the brand round

# the carton each new design must keep the shape and angle of: the author's picked take (picks.json)
PICKED = {'orange_grove': 's26', 'lemon_fresh': 's25', 'lime_fresh': 's26', 'cranberry_north': 's25',
          'pineapple_isla': 's25'}


def look(cid, design):
    """The card's own silhouette sentence from the frozen brief, with the plain card swapped for a
    printed one: the family, the ratio and the camera stay the brief's, only the print is new."""
    fam, ratio, base, _, _, _ = brief.CARDS[cid]
    if ROUND == 2:
        word, designs = BRANDS[cid]
        # the brief's silhouette sentence names a flat card colour; the brand's own colours replace it
        if ' in flat ' in base:
            head, tail = base.split(' in flat ', 1)
            base = head + tail[tail.index(' with '):] if ' with ' in tail else head
        return ('%s, about %.1f times as tall as it is wide. The carton is printed like a real supermarket juice brand: '
                '%s. ' % (base, ratio, designs[design]))
    word, colour, designs = JUICES[cid]
    return ('%s, about %.1f times as tall as it is wide. The carton is %s and its front face carries a '
            'bright printed design: %s, with the brand name "%s" in a clean band near the top of the '
            'front face. ' % (base, ratio, colour, designs[design], word))


# A PALE CARTON IS KEYED OUT WITH THE BACKGROUND (measured, round one: the lemon's pale body went with it and the
# pipeline refilled the hole in amber). Round two's white and cream cartons would lose most of their body that way, so
# the generator is asked for a solid magenta ground instead of a transparent one, and the tool keys that ground off
# itself - flood-filled in from the canvas edge, so nothing inside the carton's outline can be taken.
CHROMA = ('The carton stands alone in the middle of a flat, solid, pure bright magenta background that fills the whole '
          'frame edge to edge; there is no shadow and no floor. ')
# THE CAP MUST STAND OUT FROM THE GABLE (measured on the first KAPPY probe): a white screw cap on a white gable is one
# pale shape, and the pipeline cannot find the cap to unscrew it. The gable is asked for in the brand's colour instead.
GABLE = {
    'orange_grove': 'The sloping gable top of the carton is printed solid bright orange, with the small white screw cap in its middle. ',
    'lemon_fresh': 'The sloping gable top of the carton is printed solid lemon yellow, with the small white screw cap in its middle. ',
    'lime_fresh': 'The sloping gable top of the carton is printed solid lime green, with the small white screw cap in its middle. ',
    'cranberry_north': 'The top of the carton is printed solid deep red, with the small white screw cap in its middle. ',
    'pineapple_isla': 'The sloping gable top of the carton is printed solid golden yellow, with the small white screw cap in its middle. ',
}


def describe(cid, design):
    if ROUND == 2:
        return look(cid, design) + GABLE[cid] + brief.SEALED_NOTE + brief.CAMERA + brief.STYLE.replace(
            ', transparent background. ', '. ') + CHROMA
    return look(cid, design) + brief.SEALED_NOTE + brief.CAMERA + brief.STYLE


def key_ground(path, tol=70):
    """Takes the generator's solid ground off a round-two take: the colour at the four corners, flood-filled in from
    every edge pixel within `tol` of it, made transparent. The untouched take is kept beside it as *_bg.png."""
    from PIL import Image
    im = Image.open(path).convert('RGBA')
    w, h = im.size
    px = im.load()
    corners = [px[0, 0], px[w - 1, 0], px[0, h - 1], px[w - 1, h - 1]]
    ref = tuple(sorted(c[i] for c in corners)[1] for i in range(3))
    def near(c):
        return c[3] > 0 and sum((c[i] - ref[i]) ** 2 for i in range(3)) <= tol * tol
    seen = set()
    stack = [(x, y) for x in range(w) for y in (0, h - 1)] + [(x, y) for y in range(h) for x in (0, w - 1)]
    while stack:
        x, y = stack.pop()
        if (x, y) in seen or not (0 <= x < w and 0 <= y < h):
            continue
        seen.add((x, y))
        if not near(px[x, y]):
            continue
        px[x, y] = (0, 0, 0, 0)
        stack += [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
    bg = path.replace('.png', '_bg.png')
    if not os.path.exists(bg):
        Image.open(path).save(bg)
    im.save(path)
    return ref


def refs(cid):
    out = gen.references(True)
    picked = os.path.join(RAW, cid, PICKED[cid] + '.png')
    if os.path.exists(picked):
        out.append({'base64': gen._b64(picked),
                    'usage': 'silhouette and camera: keep this exact carton shape, proportions and viewing angle; '
                             'only the printed design on its front face changes'})
    return out


def args_for(cid, design, seed, size):
    a = {'description': describe(cid, design), 'width': size['width'], 'height': size['height'],
         'no_background': ROUND != 2, 'seed': seed}
    r = refs(cid)
    if r:
        a['reference_images'] = json.dumps(r)
    if os.path.exists(gen.ANCHOR):
        a['style_image_base64'] = gen._b64(gen.ANCHOR)
        a['style_copy'] = json.dumps(['color_palette', 'outline', 'detail', 'shading'])
    return a


NATIVE = {'width': 64, 'height': 128}   # round two's shelf take: 2x the shelf, where the generator returns whole cartons


def jobs():
    """Every job this round wants, as (tag, card, design, seed, size, out path)."""
    if ROUND == 2:
        out = []
        for cid in BRANDS:
            for d in (0, 1):
                for seed in SEEDS:
                    out.append(('%s:B%d:%d' % (cid, d, seed), cid, d, seed, LARGE,
                                os.path.join(RAW, cid, 'brand_L%d_s%d.png' % (d, seed))))
                out.append(('%s:BN%d' % (cid, d), cid, d, SEEDS[0], NATIVE,
                            os.path.join(RAW, cid, 'brand_N%d_s%d.png' % (d, SEEDS[0]))))
        return out
    out = []
    for cid in JUICES:
        for d in (0, 1):
            for seed in SEEDS:
                out.append(('%s:L%d:%d' % (cid, d, seed), cid, d, seed, LARGE,
                            os.path.join(RAW, cid, 'fruit_L%d_s%d.png' % (d, seed))))
            out.append(('%s:S%d' % (cid, d), cid, d, SEEDS[0], SMALL,
                        os.path.join(RAW, cid, 'fruit_S%d_s%d.png' % (d, SEEDS[0]))))
    return out


def _state():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def _save(st):
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(st, indent=1))


def remaining():
    text, _ = gen._call('get_balance', {})
    for line in text.splitlines():
        if line.startswith('generations_remaining'):
            return int(line.split(':')[1])
    return None


def probe():
    before = remaining()
    tag, cid, d, seed, size, out = jobs()[0]
    os.makedirs(os.path.dirname(out), exist_ok=True)
    got = gen._run(tag, args_for(cid, d, seed, size), out)
    if ROUND == 2:
        for g in got:
            print('keyed ground', key_ground(g), 'off', os.path.basename(g))
    after = remaining()
    print('probe %s -> %s' % (tag, got))
    print('generations: %s -> %s (cost %s)' % (before, after, (before - after) if before and after else '?'))


def take():
    """Submit every job that has no file yet, back to back, then collect them all - the round takes the
    time of the slowest job, not the sum. The state file holds the job ids, so a second run collects
    what the first one left rather than paying for it again."""
    st = _state()
    pending = {}
    for tag, cid, d, seed, size, out in jobs():
        if os.path.exists(out):
            continue
        os.makedirs(os.path.dirname(out), exist_ok=True)
        jid = st.get(tag)
        if not jid:
            text, msgs = gen._call(brief.TOOL, args_for(cid, d, seed, size), timeout=300)
            jid = gen._job_id(text, msgs)
            if not jid:
                print('  !! %s: no job id: %s' % (tag, text[:200]))
                continue
            st[tag] = jid
            _save(st)
            print('  queued %-26s %s' % (tag, jid[:8]))
        pending[tag] = (jid, out)
    print('collecting %d jobs...' % len(pending))
    t0 = time.time()
    while pending and time.time() - t0 < 3600:
        for tag in list(pending):
            jid, out = pending[tag]
            text, msgs = gen._call('get_image', {'job_id': jid}, timeout=120)
            imgs = gen._images_from(text, msgs)
            if imgs:
                for i, png in enumerate(imgs):
                    o = out if i == 0 else out.replace('.png', '_c%d.png' % i)
                    io.open(o, 'wb').write(png)
                    if ROUND == 2:
                        key_ground(o)
                print('  -> %-26s %d image(s) (%.0fs)' % (tag, len(imgs), time.time() - t0))
                del pending[tag]
            elif 'failed' in text.lower():
                print('  !! %s failed: %s' % (tag, text[:200]))
                del pending[tag]
        if pending:
            time.sleep(10)
    if pending:
        print('  still out:', ', '.join(pending))


def status():
    have = 0
    for tag, cid, d, seed, size, out in jobs():
        ok = os.path.exists(out)
        have += ok
        print('%-28s %s' % (tag, 'have' if ok else '-'))
    print('%d of %d in hand' % (have, len(jobs())))


if __name__ == '__main__':
    if '--round2' in sys.argv:
        ROUND = 2
        STATE = os.path.join(HERE, 'juice_brand_state.json')
        sys.argv.remove('--round2')
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'status'
    {'probe': probe, 'take': take, 'status': status}.get(cmd, lambda: print(__doc__))()
