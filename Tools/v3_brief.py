# -*- coding: utf-8 -*-
"""THE brief for a v3 bottle (GDD 25 §5a) — short, and it says only four things.

The author, 2026-08-05: "Çok zor bir şey istemiyorum… sadece şu an belirlediğimiz
pixelde belirlediğimiz perspektifte alkol isminde boş bir şişe. Kapaksız üretip
kapağı sonradan ekleyeceğiz." So the brief asks for the vessel and nothing else:
the earlier version argued with the generator over labels, dress and geometry and
got worse takes for it. Cap, label and dress are the pipeline's job, later.

The camera and the canvas are not in the words — they are the TOOL's settings
below, which is why they hold.
"""

TOOL = 'create_map_object'
VIEW = 'high top-down'          # the author's camera, picked 2026-08-04
CANVAS = {'width': 120, 'height': 280}
KNOBS = {'outline': 'single color outline',
         'shading': 'medium shading',
         'detail': 'medium detail'}


def build(spirit, glass='clear'):
    """An empty, capless, unlabelled bottle. Four clauses, no more."""
    return ("an empty %s glass %s bottle, no cap, open neck, "
            "no label, nothing inside" % (glass, spirit))


def call_args(spirit, glass='clear'):
    args = {'description': build(spirit, glass), 'view': VIEW}
    args.update(CANVAS)
    args.update(KNOBS)
    return args


# Per bottle: the spirit word and the glass colour. That is the whole variable part.
BOTTLES = {
    'vodka_astra': ('vodka', 'clear'),
}
