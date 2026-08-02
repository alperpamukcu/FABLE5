# -*- coding: utf-8 -*-
"""
The soft-drink shelf, generated on PixelLab (the author, 2026-08-03: the drawn
cartons were not good enough - "direkt pixellab'e urettir olculeri her seyi
soyleyerek").

Eight vessels, each described with its proportions as well as its subject, in the
same art language and at the same grain as the bottles already on the shelf.
"""
import contextlib, io, json, os, sys, time
import pixellab

HERE = os.path.dirname(os.path.abspath(__file__))
STATE = os.path.join(HERE, 'vessels_state.json')

# One shared tail so all eight read as one shelf.
TAIL = ("side view, clean pixel art, soft shading with subtle dithering, "
        "no anti-aliasing, thick dark outline all the way round, transparent "
        "background, standing upright and centred, "
        "absolutely no text, no letters, no words, no numbers, no logo, no writing")

CARTON = ("a rectangular prism juice carton made of matte cardboard, tall and narrow, "
          "roughly twice as tall as it is wide, seen from slightly above and to the "
          "left so that the flat front face, one side face and the top face are all "
          "visible, sharp straight edges and square corners, a small plastic screw cap "
          "standing on the top face at the back right, ")

VESSELS = {
 'orange': (144, CARTON + "bright orange carton, a large juicy ORANGE and one orange "
            "slice printed across the middle of the front face on a cream panel"),
 'lemon': (144, CARTON + "bright yellow carton, a large LEMON and one lemon wedge "
           "printed across the middle of the front face on a cream panel"),
 'lime': (144, CARTON + "fresh green carton, a large LIME and one lime wedge printed "
          "across the middle of the front face on a cream panel"),
 'pineapple': (144, CARTON + "golden yellow carton, a large whole PINEAPPLE with its "
               "spiky green crown printed across the middle of the front face on a "
               "cream panel"),
 'cranberry': (144, CARTON + "deep red carton, a cluster of dark red CRANBERRIES with "
               "two green leaves printed across the middle of the front face on a "
               "cream panel"),
 'cola': (164, "a large two-litre clear PET plastic bottle of dark brown cola, wide "
          "round body, a band of horizontal grip ribs around the waist, tapered "
          "shoulder, short neck, bright red plastic screw cap, a plain deep red label "
          "band wrapped around the middle of the bottle, fluted petaloid base"),
 'energy': (144, "a tall slim aluminium ENERGY DRINK CAN, about three times as tall as "
            "it is wide, straight metal walls, tapered shoulder at the top, a bare "
            "silver rolled rim and lid with a pull tab, the body split corner to corner "
            "into a deep blue field and a bare silver field with a small red and gold "
            "disc where they meet"),
 'soda': (156, "a clear glass bottle of sparkling soda water, slim straight body, "
          "gently tapered shoulder, short neck, blue metal screw cap, a plain pale "
          "blue label band around the middle, fine bubbles in the water"),
}


def load():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def save(s):
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(s, indent=1))


def text_of(tool, args, timeout=900):
    buf = io.StringIO()
    with contextlib.redirect_stdout(buf):
        pixellab.call(tool, args, timeout=timeout)
    return buf.getvalue()


def queue():
    st = load()
    for cid, (size, prompt) in VESSELS.items():
        if st.get(cid, {}).get('object_id'):
            print('%-12s already queued -> %s' % (cid, st[cid]['object_id']))
            continue
        out = text_of('create_1_direction_object', {
            'description': '%s, %s' % (prompt, TAIL),
            'size': size, 'view': 'sidescroller'})
        oid = None
        for line in out.splitlines():
            if line.strip().startswith('id:'):
                oid = line.split('id:')[1].strip()
        st.setdefault(cid, {})['object_id'] = oid
        st[cid]['size'] = size
        save(st)
        print('%-12s -> %s' % (cid, oid))
        if oid is None:
            print(out[:600])
        time.sleep(1.0)


if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == 'queue':
        queue()
