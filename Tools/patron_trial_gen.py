# -*- coding: utf-8 -*-
"""The 2026-08-19 cast trial: one neutral figure, three line languages, two poses.

The author asked to rebuild the customer spec from scratch and to CHOOSE the drawing
language before any animation exists ("farkli tarzlarda ilk basta animasyonsuz sadece
karakteri yaratalim ve iskeleti onun uzerinden kuralim"). So this queues six stills and
nothing else - no animation jobs, no clips - and the rig (canvas, foot line, wrist line,
HeadY, hand anchors) is measured off whichever of them the author picks.

Six, because two axes are being asked at once and they must not be confounded:
    3 line languages (inked / selective / lineless)  x  2 poses (neutral / at the bar)
The FIGURE is identical across all six, so the only difference the eye can find is the
one being chosen. Everything the six have in common is in patron_prompts.py, which is
the brief and the thing to edit; this file is only the errand.

Size 220px: the largest of the three scale candidates being compared in the room, so the
small ones can be previewed by exact nearest-neighbour reduction from a real drawing
rather than by generating three sets. Whatever the author picks is then generated AT ITS
OWN SIZE - the house's native-resolution law (14 art bible 11.A) is not suspended for a
comparison, it just does not apply to a preview that is labelled as one.

Usage:
    patron_trial_gen.py queue     queue the six (idempotent - already-queued ones skip)
    patron_trial_gen.py poll      report status, download whatever has landed
"""
import base64
import io
import json
import os
import sys
import time

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, HERE)
import pixellab                    # noqa: E402
import patron_prompts as brief     # noqa: E402

STATE = os.path.join(HERE, 'patron_trial_state.json')
RAW = os.path.join(HERE, 'AssetPipeline', 'sources', 'patron_trial')
LOG = os.path.join(HERE, 'AssetPipeline', 'generation_log.jsonl')

SIZE = brief.PIVOT_CANVAS_PX
VIEW = brief.PIVOT_VIEW    # eye level: the author looked at the top-down round and said no
POSES = (('neutral', brief.NEUTRAL_POSE), ('bar', brief.BAR_POSE))

# Round two (2026-08-19): the language and the ruler are settled, so the axis being asked
# about is WHO. One still per candidate customer, neutral pose only - create_character
# ignores pose text anyway (round one proved it), and the author asked for the standing
# pictures alone. Three generations, not six.
ROUND_TWO = True


def call(tool, args, timeout=900):
    _, body = pixellab.post({'jsonrpc': '2.0', 'id': 1, 'method': 'tools/call',
                             'params': {'name': tool, 'arguments': args}}, timeout=timeout)
    text, images = '', []
    for m in pixellab.sse(body):
        for c in ((m.get('result') or {}).get('content') or []):
            if c.get('type') == 'text':
                text += c['text'] + '\n'
            elif c.get('type') == 'image':
                images.append(Image.open(io.BytesIO(base64.b64decode(c['data']))).convert('RGBA'))
    return text, images


def load():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def save(s):
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(s, indent=1))


def log(rec):
    rec['ts'] = time.strftime('%Y-%m-%dT%H:%M:%S')
    with io.open(LOG, 'a', encoding='utf-8') as f:
        f.write(json.dumps(rec, ensure_ascii=False) + '\n')


def jobs():
    if ROUND_TWO:
        for name in TRYING:
            yield (name, brief.PIVOT_LANGUAGE, brief.NEUTRAL_POSE,
                   brief.FIGURE_OPTIONS[name])
        return
    for language in brief.LINE_LANGUAGES:
        for pose_name, pose in POSES:
            yield '%s_%s' % (language, pose_name), language, pose, brief.TRIAL_FIGURE


def queue(only=None):
    state = load()
    for key, language, pose, figure in jobs():
        if only and only not in key:
            continue
        if state.get(key, {}).get('character_id'):
            print('  %-20s already queued (%s)' % (key, state[key]['character_id'][:8]))
            continue
        text, _ = call('create_character', {
            'description': brief.character_prompt(figure, pose, language),
            'name': 'trial %s' % key,
            'mode': 'v3',
            'size': SIZE,
            'view': VIEW,
            'outline': brief.outline_hint(language),
            # BACK TO 'medium'. It was raised to 'high' to cure a flat 27-colour roll, and
            # what it actually bought was LINE WORK: every face generated at high detail
            # since has come back with a keyline (85%, 87%, 93%) while the whole approved
            # cast - drawn at medium - sits between 3 and 33. Detail and delineation are
            # the same dial to this model, and the house wants one without the other. The
            # colour-count gate catches flatness on its own.
            'detail': 'medium detail',
        })
        # The answer names the id inside a ready-to-paste call - get_character(
        # character_id="...") - so the id is pulled as a UUID, not as "everything after
        # the colon", which swallowed the whole line the first time this ran.
        import re
        m = re.search(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}',
                      text, re.I)
        cid = m.group(0) if m else None
        if not cid:
            print('  %-20s NO ID:\n%s' % (key, text[:400]))
            continue
        state[key] = {'character_id': cid, 'language': language, 'size': SIZE}
        save(state)
        log({'asset': 'patron_trial/' + key, 'event': 'queued', 'character_id': cid,
             'size': SIZE, 'view': VIEW})
        print('  %-20s queued %s' % (key, cid))


def rotations_from_zip(blob):
    """The rotation stills, SOUTH FIRST - the view the rig is measured from.

    A trial character has no animations, so everything in the zip is a rotation; they
    are named by direction, and 'south' is the one that faces the camera. The rest are
    kept in file order behind it so the report can show the turn-around too.
    """
    import zipfile
    with zipfile.ZipFile(io.BytesIO(blob)) as z:
        names = [n for n in z.namelist() if n.lower().endswith('.png')]
        names.sort(key=lambda n: (0 if 'south' in n.lower().split('/')[-1] else 1, n))
        return [Image.open(io.BytesIO(z.read(n))).convert('RGBA') for n in names]


def poll():
    os.makedirs(RAW, exist_ok=True)
    state = load()
    for key in sorted(state):
        cid = state[key]['character_id']
        text, images = call('get_character', {'character_id': cid, 'include_preview': True})
        first = text.strip().splitlines()[0] if text.strip() else '(silent)'
        # 'status: completed', not 'completed' anywhere: the answer's own hint says
        # "...runs after creation completes", which made every still look finished.
        if 'status: completed' not in text:
            print('  %-20s %s' % (key, first))
            continue
        # The south-facing rotation is the one the rig is measured from; the preview
        # image the tool hands back IS that view, so no authenticated fetch is needed
        # for a look (the rotation URLs 403 without a Bearer header - see memory).
        # include_preview does not always put an image in the answer, and the rotation
        # URLs 403 without a Bearer header, so the character's own download zip is the
        # reliable way to the pixels - the same route patron_gen.py takes to the clips.
        if not images:
            url = None
            for line in text.splitlines():
                if line.strip().startswith('download:'):
                    url = line.split('download:', 1)[1].strip()
            if not url:
                print('  %-20s completed, but no download url' % key)
                continue
            import patron_gen
            blob = patron_gen.download_zip(cid, url)
            io.open(os.path.join(RAW, key + '.zip'), 'wb').write(blob)
            images = rotations_from_zip(blob)
        if images:
            p = os.path.join(RAW, key + '.png')
            images[0].save(p)
            st = figure_stats(images[0]) or {}
            state[key]['measured'] = {k: (round(v, 3) if isinstance(v, float) else v)
                                      for k, v in st.items()}
            judge(key, images[0])
            print('  %-20s completed -> %s (%dx%d)'
                  % (key, os.path.basename(p), images[0].width, images[0].height))
            state[key]['png'] = os.path.relpath(p, HERE)
            save(state)
        else:
            print('  %-20s completed, nothing drawable in the answer' % key)


# ── animation, one clip at a time (2026-08-19, round three) ──────────────────
# The author kept two of the three candidates (clubgirl, heavyset) and asked to go
# through the clips IN ORDER rather than queueing a whole cast's worth at once, so this
# takes the clip name on the command line and does exactly that one.
#
# v3 custom mode, 16 frames - the tool's own ceiling and the answer to "animasyon
# uzunlugunu gercekcilik icin maksimumda tutalim". Cost is canvas x frames: a 220px
# canvas at 10 frames is ~8 generations per direction, so one clip for the two of them
# is ~16. That is the reason this queues one clip, looks, and only then queues the next.
#
# Direction is part of the clip, not a separate axis: the idle is watched from the front
# (south), and the walk-in crosses the room right to left, which is WEST.
KEPT = ('clubgirl', 'heavyset')
# Who is being LOOKED AT this round, as opposed to who is in the game. The author asks for
# stills first and animations only after approval, every time - so a new face is queued
# here, judged, and only then added to KEPT and given clips.
TRYING = ('silkwoman', 'pastelman')

# -- the clip table (2026-08-19, round five) ---------------------------------
# EVERY ONE-SHOT IS DRAWN IN TWO HALVES, the author's own idea and a good one:
# "animasyonu ikiye ayirmak mantikli olur mu baslangic-ortasi ortasi-sonu gibi boylece
# basladigi pozisyona smooth sekilde gelebilir ... boylece FPS'i arttirmis oluruz".
#
# Half A runs from the idle pose to the middle of the action. Half B starts on A's last
# frame and INTERPOLATES to the idle frame - the tool animates between two given poses,
# so the return is DRAWN rather than reversed, and the clip provably ends where the idle
# stands. Concatenated that is ~17 frames instead of 9, which is what buys the higher
# frame rate: more frames at 12fps is the same length, smoother.
#
# The canvas ceiling is 8 generated frames whatever we do (a 220px character is padded
# onto a 256 animation canvas), so halves are the ONLY road to a longer clip.
CLIPS = {
    'look_right': dict(directions=['south'], frames=8, start='idle'),
    'look_left':  dict(directions=['south'], frames=8, start='idle'),

    'order_a': dict(directions=['south'], frames=8, start='idle'),
    'order_b': dict(directions=['south'], frames=8, start=('order_a', 'last'), end=('order_a', 'first')),

    # ONE drink, and the GLASS IS DRAWN IN THE HAND (2026-08-19, the author: "eski tarza
    # geri donelim sadece 1 tarz drinking olsun, ayri ayri uretme, bardak elinde olsun
    # normal su bardagi gibi"). Three grips and an empty hand for the game to fill were
    # both tried; this is simpler and it is what was asked for, so the vessel classes and
    # the hand-anchor table go with it.
    'drink_a': dict(directions=['south'], frames=8, start='idle'),
    'drink_b': dict(directions=['south'], frames=8, start=('drink_a', 'last'), end=('drink_a', 'first')),

    'cheer_a': dict(directions=['south'], frames=8, start='idle'),
    'cheer_b': dict(directions=['south'], frames=8, start=('cheer_a', 'last'), end=('cheer_a', 'first')),
    'upset_a': dict(directions=['south'], frames=8, start='idle'),
    'upset_b': dict(directions=['south'], frames=8, start=('upset_a', 'last'), end=('upset_a', 'first')),

    # The walk is a CYCLE, not a one-shot: it already ends where it begins, by being a
    # loop. West, because the walk-in crosses the room right to left.
    'walk': dict(directions=['west'], frames=8),
}
FRAMES = 8

# RESTRAINT IS IN EVERY DESCRIPTION, because it has had to be twice already (the author:
# "cok abartili hareketler vermemeliler, normal insan gibi sakin az hareket etmeliler").
# A v3 clip left to itself performs; what stops it is naming the amount and naming what
# must not move, in the same breath as what must.
CALM = ('a small restrained movement, calm and natural, '
        'the body stays where it is, no exaggeration, no big gesture')

CUSTOM = {
    # NO OBJECTS AND NO PLACES IN THESE STRINGS. The tool says so in its own schema
    # ("focusing on the movement or pose only ... avoid environmental details like
    # locations or objects"), and the one description that ignored it - "resting on a bar
    # top" - came back as a failed generation for both characters, twice.
    #
    # Every one of them is STANDING, arms down, and every half A ends on the pose half B
    # is asked to come back from.
    'look_right': ('turning only the head to the right, a small glance to the side, '
                   'then holding that look, the shoulders do not turn, '
                   'the arms stay down at the sides, ' + CALM),
    'look_left': ('turning only the head to the left, a small glance to the side, '
                  'then holding that look, the shoulders do not turn, '
                  'the arms stay down at the sides, ' + CALM),

    # SPEAKING, over two halves so it lasts (the author: "konusulan animasyonlar daha uzun
    # surmeli"). A opens the mouth and leans in; B settles back.
    'order_a': ('beginning to speak, the mouth opens and the head lifts and tilts slightly '
                'towards the listener, the chin comes up a little, '
                'the arms stay down at the sides and do not gesture, ' + CALM),
    'order_b': ('finishing the sentence and settling, the mouth closes, the head comes '
                'level again and the chin lowers, '
                'the arms stay down at the sides, ' + CALM),

    # THE GLASS IS DRAWN, after all. It was left out on purpose for a while - an empty
    # hand lets the GAME pin the served vessel into it, so a customer drinks whatever was
    # actually poured - and the author has decided the simpler thing: one clip, one plain
    # glass, drawn. Worth writing down that this makes every customer drink from the same
    # glass whatever the recipe said, which is a real trade and a deliberate one.
    #
    # "Plain" and "water" are load-bearing words: asked for something "tall and heavy" the
    # model drew a white blob and swung the hand behind the head, and asked for nothing at
    # all it drew a fist. A drinking glass is a thing it knows.
    #
    # And ONLY ONE ARM MOVES, named explicitly: clubgirl's first attempt brought both
    # hands to her face, which is somebody about to sneeze.
    'drink_a': ('raising the right hand, holding a plain clear drinking glass, up in front '
                'of the chest until the glass reaches the mouth, and tilting the head back '
                'a little to drink from it, as if drinking a glass of water, '
                'ONLY the right arm moves, the left arm hangs straight down and does not '
                'move at all, ' + CALM),
    'drink_b': ('lowering the right hand with the glass back down to the side and bringing '
                'the head level again, the left arm stays down, ' + CALM),
    # MORE DEFINITE than the first take (the author: "sevinme ve uzulme animasyonlari
    # biraz daha belirgin olmali"). Still nothing thrown in the air - what is wanted is
    # legibility at sixty pixels of head, not theatre, so the whole FACE moves and the
    # shoulders go with it.
    'cheer_a': ('a clearly pleased reaction, a broad smile, the eyebrows lift and the head '
                'nods forward once, the shoulders rise and the chest opens, '
                'the arms stay down at the sides, ' + CALM),
    'cheer_b': ('settling back from the smile, the head comes level and the shoulders '
                'lower again, still pleased, the arms stay down, ' + CALM),
    'upset_a': ('a clearly displeased reaction, a deep frown, the brows pull together, the '
                'head turns away and shakes once, the shoulders drop and the chin lowers, '
                'the arms stay down at the sides, ' + CALM),
    'upset_b': ('settling back from the frown, the head comes level and forward again, '
                'still unhappy, the arms stay down, ' + CALM),

    'walk': ('walking forward with small short steps at a calm unhurried pace, '
             'the feet stay close to the ground, the arms swing very little, ' + CALM),
}


# WHERE THE COUNTER CUTS, in canvas rows: the foot line is 210 and the game drops the
# feet 93 art pixels below the bar's top edge (TycoonHud.CharFootDrop), so everything from
# row 117 down is behind the wood and nobody ever sees it. Measuring a keyline there is
# measuring a part of the drawing that is not in the game - and it is not a small
# correction: this round's leopard carries 413 of her 657 keyline pixels on her legs.
VISIBLE_CUT = 117


def edge_darkness(im, bottom=None):
    """What share of the silhouette is drawn as a KEYLINE, as a percentage.

    Not simply "how dark is the edge" - that was the first version and silverbob broke it
    by wearing a black blazer: a dark garment puts dark pixels on the silhouette without
    any outline being drawn at all, and she was refused three times for her jacket.

    A keyline is a rim that is darker than the thing it encloses. So an edge pixel counts
    only when it is near-black AND the pixel a few steps INSIDE the figure is markedly
    lighter. Black cloth is the same colour as its own interior and scores nothing;
    an inked figure scores everywhere, including around its face and arms.

    The line language is chosen by eye and cannot be trusted by eye, because PixelLab
    treats `outline` as soft guidance - the same setting that drew clubgirl drew a later
    face with a full black keyline (the author: "siyah koyu kontur olmamali").
    """
    px = im.load()
    w, h = im.size
    edge = keyline = 0
    for y in range(1, min(h - 1, bottom if bottom is not None else h)):
        for x in range(1, w - 1):
            if px[x, y][3] < 40:
                continue
            inward = None
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                if px[x + dx, y + dy][3] < 40:
                    ix, iy = x - dx * 3, y - dy * 3
                    if 0 <= ix < w and 0 <= iy < h and px[ix, iy][3] >= 40:
                        inward = px[ix, iy]
                    break
            if inward is None:
                continue
            edge += 1
            r, g, b, _ = px[x, y]
            if max(r, g, b) < 80 and max(inward[:3]) - max(r, g, b) >= 45:
                keyline += 1
    return 100.0 * keyline / max(1, edge)


# Recalibrated on the cast once the measure stopped counting dark clothing: see below.
OUTLINE_MAX = 45.0


def figure_stats(im):
    """The three numbers a new face is accepted or refused on.

    outline   dark share of the silhouette - the line language, which the `outline`
              parameter only suggests (see edge_darkness).
    head      the head's height as a fraction of the body's. The cast runs 0.125-0.152;
              a bigger fraction is a chibi, and no amount of good drawing hides it beside
              a figure that is not one. v3 has no proportions dial, so this is checked
              rather than set.
    colours   how many distinct colours the figure is drawn in - the detail level. The
              cast runs 37-57; below that a customer reads flat beside the others.

    All three were added the day the author put two new faces beside heavyset and said
    they did not match him "vucut oranti, detay seviyesi vs" - and all three turned out
    to be measurable, which is the only reason they are here rather than in a note.

    OUTLINE IS MEASURED TWICE, and the second one is the one that decides. The first is
    the whole drawing; 'visible' stops at the counter line, and the two disagree by a lot
    in both directions - eastasianman is 39% whole and 16% visible, this round's Spaniard
    is 38% whole and 60% visible. The player only ever sees the visible half, so that is
    what the gate reads. The cast the author approved runs 4% to 40% on it, which is the
    band, and it is a coincidence worth noting that the old whole-figure limit of 45 still
    contains them exactly.
    """
    px = im.load()
    w, h = im.size
    xs = [x for x in range(w) if any(px[x, y][3] >= 40 for y in range(h))]
    ys = [y for y in range(h) if any(px[x, y][3] >= 40 for x in range(w))]
    if not xs or not ys:
        return None
    body = ys[-1] - ys[0] + 1
    # THE HEAD IS MEASURED TOP TO CHIN, not across. The first version took the widest row
    # in the top sixth, which is hair, not skull: afrowoman's afro scored 0.187 and read as
    # a bighead beside a cast that is nothing of the kind. Height is the honest proxy - the
    # head runs from the crown down to the NECK, which is the narrowest row between the
    # head's own widest and the shoulders' spread, and every silhouette has one.
    top = ys[0]
    scan = range(top, top + int(body * 0.30))
    widths = {y: sum(1 for x in range(w) if px[x, y][3] >= 40) for y in scan}
    if widths:
        widest_head = max(widths, key=lambda y: widths[y] if y < top + body * 0.14 else -1)
        below = [y for y in scan if y > widest_head]
        neck = min(below, key=lambda y: widths[y]) if below else widest_head
    else:
        neck = top
    head_w = max(1, neck - top)
    colours = len({px[x, y][:3] for x in range(w) for y in range(h) if px[x, y][3] >= 200})
    # NEAR-BLACK CLOTH, as a share of the figure. It gets its own number because it
    # DEFEATS the outline measure rather than failing it: a keyline is "a rim darker than
    # what it encloses", and a black blazer has nothing lighter inside it to be darker
    # than. silverbob came back with flat black lapels, seams and edges and scored 6% -
    # the author saw it at a glance ("takim elbisesi siyah konturle gecmis fakat biz siyah
    # kontur kullanmiyoruz"). So a figure that is largely near-black is refused outright:
    # at this size the house cannot tell the garment from the line, and neither can anyone.
    ink = sum(1 for y in range(h) for x in range(w)
              if px[x, y][3] >= 200 and max(px[x, y][:3]) < 45)
    solid = sum(1 for y in range(h) for x in range(w) if px[x, y][3] >= 200)
    return {'outline': edge_darkness(im, VISIBLE_CUT), 'head': head_w / max(1, body),
            'whole': edge_darkness(im), 'colours': colours, 'body': body,
            'black': 100.0 * ink / max(1, solid)}


# CALIBRATED ON THE CAST THE AUTHOR APPROVED, not on a number I liked: pastelman .120,
# heavyset .147, clubgirl .158, silkwoman .162. The band has to contain all four, because
# every one of them was looked at and kept. silverbob at .183 sits well outside it, which
# is what "vucut orantisi ... heavyset ornegine uygun degil" measures out as.
# Kept for the record, unused as a gate: see judge().
HEAD_BAND = (0.112, 0.166)
COLOURS_MIN = 34             # the cast: 37 to 57
# CALIBRATED ON THE CAST, again, and the first guess was badly wrong: I set 18 and it
# refused clubgirl (54%, black trousers) and heavyset (40%). Near-black CLOTHING is normal
# here and is not the problem. What fails is a figure that is near-black almost EVERYWHERE
# - silverbob's black trouser suit came to 77%, and at that share every internal edge, every
# lapel and seam, is a black line whether or not anybody drew one.
BLACK_MAX = 62.0
# HOW TALL THE DRAWING COMES OUT, which the generator decides for itself: the cast fills
# 197-209 of its 220 canvas, and a roll that fills less is simply a smaller person once it
# is standing at the bar - the author spotted one at 188 ("ispanyol kucuk olmus"). It is
# not a setting, so it is a gate: re-roll until the figure is the cast's size.
BODY_BAND = (194, 214)


def judge(name, im):
    """Print the measurements and say plainly whether the face is in the cast's bands.

    Measured on the RAW DOWNLOAD, which is also what ships. It briefly measured the
    filtered figure instead, back when a keyline was stripped at ship time; the author
    threw that out ("hicbir karakterde siyah kontur olmamali ... dogal kontur olacak") and
    he is right that a filtered edge is not the same thing as a drawing that never had a
    line. So the drawing itself has to pass, and this is the number that says whether it
    does.
    """
    st = figure_stats(im)
    if st is None:
        return False
    # HEAD IS PRINTED, NOT JUDGED - and admitting that is the finding. Two versions were
    # tried and both measured HAIR: across, an afro reads as a bighead (0.187); top to
    # chin, a ponytail does (0.182) and a shaved head reads as a pinhead (0.108). The
    # cast's real proportions are alike; their haircuts are not, and a silhouette cannot
    # tell the two apart. So the number is reported for the eye to use and the gates keep
    # only what they can actually see: the keyline, the colour count, the near-black share.
    ok = (st['outline'] <= OUTLINE_MAX
          and st['colours'] >= COLOURS_MIN
          and st['black'] <= BLACK_MAX
          and BODY_BAND[0] <= st['body'] <= BODY_BAND[1])
    print('  %-20s body %d  outline %.0f%% seen (%.0f%% whole)  colours %d  black %.0f%%'
          '  (hair+head %.3f)  -> %s'
          % (name, st['body'], st['outline'], st['whole'], st['colours'], st['black'],
             st['head'], 'in band' if ok else 'OUT OF BAND, re-roll'))
    return ok


# -- rolling a batch and keeping the measured best (2026-08-20) --------------
# "Fighting a lottery with more coins is not a method" was the argument for stripping the
# keyline off the finished art instead (a filter that lived here for one day and was
# thrown out), and it was wrong in one word: fighting a lottery with more coins and NO
# SCOREBOARD is not a method. Three re-rolls judged by eye lost three times; the
# same three judged by edge_darkness would have been told apart in a second, because the
# spread is enormous - the cast runs 3% to 39% keyline from one identical setting.
#
# So a face is now rolled N times AT ONCE, every roll is measured, and the best one is
# adopted under the plain name. The losers stay in the state file with their numbers, so
# what was rejected and why is on the record rather than in a memory.
def roll(name, n=3, figure=None, tag='r'):
    """Queue n candidates for one figure, as <name>__<tag>1 .. n.

    FIGURE overrides the brief for one batch, which is how a fault gets ISOLATED rather
    than re-rolled at: leopard came back inked six times out of six while the man rolled
    beside her came back clean, so the fault is in her description and the way to find it
    is one batch that varies one clause each. Whatever wins is then written back into
    patron_prompts.py, so the brief still describes what shipped."""
    state = load()
    figure = figure or brief.FIGURE_OPTIONS[name]
    for i in range(1, n + 1):
        key = '%s__%s%d' % (name, tag, i)
        if state.get(key, {}).get('character_id'):
            print('  %-20s already rolled (%s)' % (key, state[key]['character_id'][:8]))
            continue
        text, _ = call('create_character', {
            'description': brief.character_prompt(figure, brief.NEUTRAL_POSE,
                                                  brief.PIVOT_LANGUAGE),
            'name': 'trial %s' % key, 'mode': 'v3', 'size': SIZE, 'view': VIEW,
            'outline': brief.outline_hint(brief.PIVOT_LANGUAGE),
            'detail': 'medium detail',
        })
        import re
        m = re.search(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}',
                      text, re.I)
        if not m:
            print('  %-20s NO ID: %s' % (key, text[:200].replace(chr(10), ' ')))
            continue
        state[key] = {'character_id': m.group(0), 'language': brief.PIVOT_LANGUAGE,
                      'size': SIZE, 'roll_of': name}
        save(state)
        log({'asset': 'patron_trial/' + key, 'event': 'queued', 'roll_of': name,
             'character_id': m.group(0), 'size': SIZE, 'view': VIEW})
        print('  %-20s queued %s' % (key, m.group(0)))


def adopt(key):
    """Promote one candidate to the plain name it was rolled for: its id, its still and
    its measurements become the character the rest of the pipeline animates and ships."""
    import shutil
    state = load()
    cand = state[key]
    name = cand['roll_of']
    old = state.get(name, {}).get('character_id')
    state[name] = {'character_id': cand['character_id'], 'language': cand['language'],
                   'size': cand['size'], 'measured': cand.get('measured'),
                   'png': os.path.relpath(os.path.join(RAW, name + '.png'), HERE),
                   'clips': {}, 'adopted_from': key, 'replaced': old}
    shutil.copyfile(os.path.join(RAW, key + '.png'), os.path.join(RAW, name + '.png'))
    zp = os.path.join(RAW, name + '_anim.zip')
    if os.path.exists(zp):
        os.remove(zp)          # the old body's clips are not this body's clips
    save(state)
    log({'asset': 'patron_trial/' + name, 'event': 'adopted', 'from': key,
         'character_id': cand['character_id'], 'replaced': old,
         'measured': cand.get('measured')})
    print('  %s -> %s  (%s replaces %s)'
          % (key, name, cand['character_id'][:8], (old or '-')[:8]))


def pull(slug):
    """Download a character's whole zip to <slug>_anim.zip - the frames of every clip.

    The one step that used to live outside the repo, in a throwaway script, which meant
    the pipeline could not be re-run from a clean checkout. The endpoint answers HTTP 423
    while any job is still running, so a failure here usually just means "not yet".
    """
    import patron_gen
    state = load()
    cid = state[slug]['character_id']
    text, _ = call('get_character', {'character_id': cid, 'include_preview': False})
    if 'status: completed' not in text:
        print('  %-12s %s' % (slug, (text.strip().splitlines() or ['(silent)'])[0]))
        return False
    url = next((l.split('download:', 1)[1].strip() for l in text.splitlines()
                if l.strip().startswith('download:')), None)
    if not url:
        print('  %-12s completed, no download url' % slug)
        return False
    pending = [l.strip() for l in text.splitlines() if 'pending' in l.lower()]
    try:
        blob = patron_gen.download_zip(cid, url)
    except Exception as e:
        print('  %-12s not ready (%s) %s' % (slug, e, pending[:1]))
        return False
    io.open(os.path.join(RAW, slug + '_anim.zip'), 'wb').write(blob)
    groups = patron_gen.frames_from_zip(blob)
    print('  %-12s %s' % (slug, ', '.join('%s(%d)' % (k, len(v))
                                          for k, v in sorted(groups.items()))))
    return True


def clip_frames(slug, clip):
    """One clip's frames, from wherever they are.

    Shipped clips live under Resources/Patron/<slug>/<clip> and are the ones the game
    plays - those win, because a clip should continue from what the player is looking at.
    A half that has only just been generated is not shipped yet (it has no folder of its
    own; it ships concatenated with its other half), so the download zip is the fallback.
    """
    d = os.path.join(ROOT, 'Assets', 'Resources', 'Patron', slug, clip)
    if os.path.isdir(d):
        names = sorted(n for n in os.listdir(d) if n.endswith('.png'))
        if names:
            return [Image.open(os.path.join(d, n)).convert('RGBA') for n in names]
    zip_path = os.path.join(RAW, slug + '_anim.zip')
    if not os.path.exists(zip_path):
        return None
    import patron_gen
    return patron_gen.frames_from_zip(io.open(zip_path, 'rb').read()).get(clip)


def frame_b64(frame):
    import base64
    buf = io.BytesIO()
    frame.save(buf, format='PNG')
    return base64.b64encode(buf.getvalue()).decode('ascii')


def start_frame(slug, clip, which='held'):
    """One clip's frame, base64, as a starting or ending pose for another.

    'held' is the pose a clip settles on (measured - see peak_frame, and why it is not
    simply the last frame). 'last' is where a clip physically ENDS, which is what a
    continuation has to start from: the walk's held frame is mid-stride, and sitting down
    out of mid-stride is a stumble; half A's held frame is the middle of the action, and
    half B has to begin where A stopped.
    """
    frames = clip_frames(slug, clip)
    if not frames:
        return None
    pick = (frames[-1] if which == 'last'
            else frames[0] if which == 'first'
            else frames[peak_frame(frames)] if len(frames) > 1 else frames[0])
    return frame_b64(pick)


# The server runs at most 20 jobs at once and refuses the 21st outright ("need 1 job
# slots but only 0 available"). Three characters x seven clips is 21, so a whole round
# for three people does not fit in one pass - animate() waits for room rather than
# dropping the overflow on the floor, because a silently missing clip is a customer who
# freezes mid-visit.
JOB_SLOTS_WAIT = 45


def animate(clip, names=None):
    """Queue one clip for the named characters (KEPT by default). One clip at a time, on
    purpose: each is looked at before the next is paid for."""
    spec = CLIPS[clip]
    state = load()
    for name in (names or KEPT):
        cid = state[name]['character_id']
        done = state[name].setdefault('clips', {})
        if clip in done:
            print('  %-10s %-13s already queued (%s)' % (name, clip, done[clip][:8]))
            continue
        args = {'character_id': cid, 'animation_name': clip,
                'directions': spec['directions']}
        if spec.get('template'):
            args.update(mode='template', template_animation_id=spec['template'],
                        ai_freedom=0)
        else:
            args.update(mode='v3', frame_count=spec.get('frames', FRAMES),
                        action_description=CUSTOM[clip])
            # Start from another clip's HELD frame rather than from the standing rotation,
            # so a seated customer's arms do not jump back to their sides every time the
            # clip changes. The frame is read off what has already shipped, which means a
            # re-ship with different art re-anchors these clips too.
            if spec.get('start'):
                src, which = (spec['start'] if isinstance(spec['start'], tuple)
                              else (spec['start'], 'held'))
                seed = start_frame(name, src, which)
                if seed is None:
                    print('  %-10s %-13s needs %s first' % (name, clip, src))
                    continue
                args['custom_start_frame_base64'] = seed
            # INTERPOLATION: given both ends, the tool animates between them - which is how
            # a returning half is guaranteed to arrive at the idle pose instead of near it.
            # This is the whole reason the one-shots are drawn in halves.
            # The end pose is half A's FIRST frame, not the shipped idle - same pose, but
            # on the animation canvas. A 220px character animates on a 220x256 one, and the
            # server refuses a pair of frames that disagree ("End frame dimensions (220x220)
            # must match start frame (220x256)"). A's first frame IS the idle, padded by the
            # server itself, so it is the only version of that pose guaranteed to fit.
            if spec.get('end'):
                src2, which2 = (spec['end'] if isinstance(spec['end'], tuple)
                                else (spec['end'], 'last'))
                target = start_frame(name, src2, which2)
                if target is None:
                    print('  %-10s %-13s needs %s first' % (name, clip, src2))
                    continue
                args['end_frame_base64'] = target
        import re
        text, _ = call('animate_character', args)
        ids = re.findall(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}', text, re.I)
        job = next((i for i in ids if i != cid), None)
        if not job and 'job slots' in text:
            # No room on the server: wait for a slot and ask again, twice, before giving up.
            for _ in range(6):
                time.sleep(JOB_SLOTS_WAIT)
                text, _ = call('animate_character', args)
                ids = re.findall(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}',
                                 text, re.I)
                job = next((i for i in ids if i != cid), None)
                if job:
                    break
        if not job:
            print('  %-10s %-13s NO JOB: %s' % (name, clip, text[:220].replace(chr(10), ' ')))
            continue
        done[clip] = job
        save(state)
        log({'asset': 'patron_trial/' + name, 'event': 'animation queued',
             'character_id': cid, 'clip': clip, 'template': spec.get('template'),
             'start': spec.get('start'), 'directions': spec['directions']})
        print('  %-10s %-13s queued %s (%s)'
              % (name, clip, job, 'interpolated' if spec.get('end')
                 else spec.get('template') or 'v3 custom'))


def peak_frame(frames):
    """Where a turn-and-hold clip should STOP, measured rather than chosen.

    The glance clips were asked to turn 45 degrees and hold, and only half of them do:
    clubgirl holds her look through the last three frames, while heavyset turns and comes
    BACK to the front by the end. A clip that returns cannot be held on its last frame, so
    the rig holds the frame that is furthest from frame 0 instead - the peak of the turn -
    and plays 0..peak forward when the neighbour arrives, peak..0 backward when they go.

    Distance is counted in changed pixels against frame 0, which for a head-only motion is
    exactly the amount of head that has turned. Measured 2026-08-19:
        clubgirl look_right 7, look_left 8 (both hold to the end)
        heavyset look_right 4, look_left  6 (both return, so both are trimmed)
    """
    from PIL import ImageChops
    base = frames[0].convert('RGB')
    best, best_i = -1, 0
    for i, f in enumerate(frames):
        d = ImageChops.difference(base, f.convert('RGB')).load()
        w, h = f.size
        n = sum(1 for y in range(h) for x in range(w) if sum(d[x, y]) > 30)
        if n > best:
            best, best_i = n, i
    return best_i


def peaks():
    """Print the hold frame for every glance clip that has landed."""
    import patron_gen
    for name in KEPT:
        blob = io.open(os.path.join(RAW, name + '_anim.zip'), 'rb').read()
        g = patron_gen.frames_from_zip(blob)
        for clip in ('look_right', 'look_left'):
            if clip in g:
                print('  %-10s %-11s %d kare -> tut %d'
                      % (name, clip, len(g[clip]), peak_frame(g[clip])))


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'poll'
    if cmd == 'queue':
        queue(sys.argv[2] if len(sys.argv) > 2 else None)
    elif cmd == 'roll':
        roll(sys.argv[2], int(sys.argv[3]) if len(sys.argv) > 3 else 3)
    elif cmd == 'adopt':
        adopt(sys.argv[2])
    elif cmd == 'pull':
        for slug in sys.argv[2:]:
            pull(slug)
    elif cmd == 'anim':
        animate(sys.argv[2], sys.argv[3:] or None)
    elif cmd == 'peaks':
        peaks()
    else:
        poll()
