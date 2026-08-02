# -*- coding: utf-8 -*-
"""
The juice cartons, generated again (the author, 2026-08-03).

Two things were wrong with the first set and neither could be repaired in the sprite:
the screw cap landed wherever the generator felt like putting it - one hard right, one
dead centre - and the author wants them all on the LEFT; and the cartons had no
cap-off state for the pour stage. A carton's roof is flat and the cap is painted onto
it rather than standing proud of it, so no amount of measuring finds the cap reliably
across five different drawings. Asking for it in the right place is the honest fix.

Each carton is asked for twice: shut, and open with the spout showing.
"""
import contextlib, io, json, os, sys, time
import pixellab

HERE = os.path.dirname(os.path.abspath(__file__))
STATE = os.path.join(HERE, 'cartons2_state.json')

TAIL = ("side view, clean pixel art, soft shading with subtle dithering, "
        "no anti-aliasing, thick dark outline all the way round, transparent "
        "background, standing upright and centred, "
        "absolutely no text, no letters, no words, no numbers, no logo, no writing")

BODY = ("a NARROW TALL rectangular prism juice carton made of matte cardboard, twice as "
        "tall as it is wide, seen from slightly above and to the left so the flat front "
        "face, one narrow side face and the flat top face are visible, sharp straight "
        "edges and square corners, ")

SHUT = ("a small round plastic screw cap standing on the LEFT-HAND side of the top face, "
        "clearly over on the left, not in the middle and not on the right, ")

OPEN = ("the screw cap has been taken OFF and is gone: on the LEFT-HAND side of the top "
        "face there is an open round spout, a short raised ring of plastic with a dark "
        "hole in the middle of it, clearly over on the left, ")

FRUIT = {
    'orange': "bright orange carton, a large ORANGE and one orange slice printed across "
              "the middle of the front face on a cream panel",
    'lemon': "bright yellow carton, a large LEMON and one lemon wedge printed across the "
             "middle of the front face on a cream panel",
    'lime': "fresh green carton, a large LIME and one lime wedge printed across the "
            "middle of the front face on a cream panel",
    'pineapple': "warm golden yellow carton, a large whole PINEAPPLE with its spiky green "
                 "crown printed across the middle of the front face on a cream panel",
    'cranberry': "deep red carton, a cluster of dark red CRANBERRIES with two green leaves "
                 "printed across the middle of the front face on a cream panel",
}


def load():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def save(s):
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(s, indent=1))


def queue():
    st = load()
    for style, fruit in FRUIT.items():
        for state, lid in (('shut', SHUT), ('open', OPEN)):
            key = '%s_%s' % (style, state)
            if st.get(key):
                print('%-18s already queued -> %s' % (key, st[key]))
                continue
            buf = io.StringIO()
            with contextlib.redirect_stdout(buf):
                pixellab.call('create_1_direction_object', {
                    'description': BODY + lid + fruit + ', ' + TAIL,
                    'size': 144, 'view': 'sidescroller'}, timeout=900)
            out = buf.getvalue()
            oid = None
            for line in out.splitlines():
                if line.strip().startswith('id:'):
                    oid = line.split('id:')[1].strip()
            st[key] = oid
            save(st)
            print('%-18s -> %s' % (key, oid))
            if oid is None:
                print(out[:400])
            time.sleep(1.0)


if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == 'queue':
        queue()
