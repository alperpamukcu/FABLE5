# -*- coding: utf-8 -*-
"""THE brief for a v3 bottle (GDD 25 §5a) — short, and the label CARRIES ITS NAME.

The author, 2026-08-05: "Etiketler boş olmasın, pixellab yazı da üretebilsin ya da
yazı yazabilsin, eğer böyle bir direktif varsa iptal et." The no-text rule is
cancelled. It existed because the generator used to return mangled glyphs, and the
pipeline stamped the wordmark instead; the author has weighed that and wants the
name generated, so the brand word goes in the brief and the stamping step stands
down unless a take comes back unreadable.

The camera and the canvas are not in the words — they are the TOOL's settings
below, which is why they hold.
"""

TOOL = 'create_map_object'
VIEW = 'high top-down'          # the author's camera, picked 2026-08-04
CANVAS = {'width': 120, 'height': 280}
KNOBS = {'outline': 'single color outline',
         'shading': 'medium shading',
         'detail': 'high detail'}      # letters need the detail budget


def build(spirit, brand, glass='clear', dress=''):
    """An empty, capless bottle whose label reads the brand's own name."""
    return ('an empty %s glass %s bottle, no cap, open neck, nothing inside, '
            '%swith the word "%s" written in large clear letters on the label'
            % (glass, spirit, dress, brand))


def call_args(spirit, brand, glass='clear', dress=''):
    args = {'description': build(spirit, brand, glass, dress), 'view': VIEW}
    args.update(CANVAS)
    args.update(KNOBS)
    return args


# Per bottle: the spirit, the parody name from base_bar.json, the glass colour, and
# one short dress clause where the mark's layout is part of the recognition.
BOTTLES = {
    'vodka_astra': ('vodka', 'SMIRKOFF', 'clear',
                    'a white label with a bold red band across it, '),
}
