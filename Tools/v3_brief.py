# -*- coding: utf-8 -*-
"""THE brief for a v3 bottle (GDD 25 §5a) — the full-set edition, 2026-08-05.

The author's rules, verbatim in spirit:
  * lower the pixel density — the 120x280 takes were too fine; the canvas is 80x160
  * every bottle resembles ITS real brand's bottle design (GDD 25 §4: silhouette
    family yes, registered distinctive elements shifted)
  * generated CAPPED, placed in the game; the open state is derived at the seam
  * the camera is identical across every production — it lives in the tool settings
  * bottles are WIDE, not thin and tall: ratios run 1.9-2.6, written as numbers
  * the label carries the parody name — the no-text rule stays cancelled

Beers are NOT here: beer comes from a keg (GDD 21 §10) and the shelf shows kegs.
"""

TOOL = 'create_map_object'
VIEW = 'high top-down'          # the author's camera, picked 2026-08-04
CANVAS = {'width': 80, 'height': 160}
KNOBS = {'outline': 'single color outline',
         'shading': 'medium shading',
         'detail': 'high detail'}      # letters need the detail budget


def build(dress, brand, ratio):
    return ('%s, the bottle is about %.1f times as TALL as it is WIDE, '
            'the bottle is EMPTY - nothing inside, '
            'with the word "%s" written in large clear letters on the label'
            % (dress, ratio, brand))


def call_args(bottle_id):
    dress, brand, ratio = BOTTLES[bottle_id]
    args = {'description': build(dress, brand, ratio), 'view': VIEW}
    args.update(CANVAS)
    args.update(KNOBS)
    return args


# id -> (dress: the real bottle's design, echoed and shifted; brand; ratio).
# One clause per bottle. The cap is part of the dress, because it generates on.
BOTTLES = {
    # ── vodka: Smirnoff / Absolut / Grey Goose / Beluga ─────────────────────────
    'vodka_astra': (
        'a clear glass vodka bottle with a white label and a bold red horizontal '
        'band, a silver metal cap', 'SMIRKOFF', 2.6),
    'vodka_vor': (
        'a clear glass vodka bottle with a short neck and no shoulders, the label '
        'printed directly on the glass in blue, a small silver cap', 'ABSOLVE', 2.2),
    'vodka_leonid': (
        'a frosted pale glass vodka bottle with a grey goose-like bird flying on '
        'its label, a tall silver cap', 'GREY GANDER', 2.6),
    'vodka_okhta': (
        'a dark navy blue glass vodka bottle with a small white label and a tiny '
        'silver fish emblem, a black cap', 'WHITE WHALE', 2.4),
    # ── gin: Gordon's / Beefeater / Hendrick's / Monkey 47 ──────────────────────
    'gin_boothby': (
        'a squat dark green glass gin bottle with a cream yellow label and a small '
        'red crest, a green cap', "GARDEN'S", 2.2),
    'gin_juniper_crown': (
        'a clear glass gin bottle with a white label with bold red frame and a '
        'small guard figure emblem, a red cap', 'LEAFEATER', 2.3),
    'gin_thornwood': (
        'a dark apothecary-style black glass gin bottle with a diamond-shaped '
        'cream label, a black cap', "HENDRAKE'S", 2.0),
    'gin_veilcrest': (
        'a brown pharmacy-style glass gin bottle with a round cream label and a '
        'small monkey-like animal emblem, a cork stopper cap', 'GIBBON 48', 2.2),
    # ── rum: Bacardi / Captain Morgan / Kraken / Diplomatico ────────────────────
    'rum_cane_coral': (
        'a clear glass white rum bottle with a white label and a small black '
        'winged bat-like emblem, a red cap', 'WHITE BAT', 2.4),
    'rum_tidewater': (
        'an amber glass rum bottle with a dark label showing a small standing '
        'sea-captain figure, a gold cap', 'ADMIRAL MORGAN', 2.3),
    'rum_windward': (
        'a squat black glass rum bottle with two small glass handles at the neck '
        'and a black label with a sea-monster emblem, a black cap', 'KRAKATOA', 2.0),
    'rum_reina_del_mar': (
        'a dark round-shouldered glass rum bottle with a cream label and a thin '
        'blue ribbon band, a wooden cap', 'EMISSARY', 2.1),
    # ── bourbon: Jim Beam / Jack Daniel's / Maker's Mark / Pappy Van Winkle ─────
    'bourbon_redline': (
        'a square clear glass bourbon whiskey bottle with a white label and red '
        'accents, a black cap', 'JIM BEAN', 2.2),
    'bourbon_old_harrow': (
        'a square dark glass whiskey bottle with a black label with white frame, '
        'a black cap', "JACK SPANIEL'S", 2.2),
    'bourbon_ashfall': (
        'a squat round clear glass whiskey bottle with a cream label, its neck '
        'sealed in dripping amber wax', "MASON'S MARK", 2.0),
    'bourbon_hollow_oak': (
        'a rounded clear glass whiskey bottle with an aged paper label, a gold '
        'foil cap', 'VAN WRINKLE', 2.3),
    # ── tequila: Jose Cuervo / 1800 / Don Julio / Clase Azul ────────────────────
    'tequila_sonora': (
        'a clear glass tequila bottle with a cream label with green and red '
        'accents, a black cap', 'JOSE CUERDO', 2.2),
    'tequila_alta_luna': (
        'a trapezoid glass tequila bottle with sloped shoulders and a wide flat '
        'stopper top, a small brown label', '1810', 2.0),
    'tequila_sol_viejo': (
        'a squat clear glass tequila bottle with a simple white label with blue '
        'agave emblem, a brown cap', 'DON JULEP', 1.9),
    'tequila_cielo_roto': (
        'a white ceramic tequila bottle with cobalt blue vertical fluting and a '
        'rounded ceramic stopper', 'AZULEJO', 2.6),
    # ── the singles: Campari / Cinzano / Grand Marnier / Kahlua ─────────────────
    'amaro_notte': (
        'a squat red glass amaro bottle with conical shoulders and a white label '
        'with blue band, a white cap', 'CUMPARI', 2.0),
    'vermouth_velvet': (
        'a dark green glass vermouth bottle with a large label of red and blue '
        'halves, a gold cap', 'CANZONE', 2.4),
    'liqueur_delia': (
        'a round-bellied brown glass orange liqueur bottle with a narrow neck, a '
        'cream label with a thin red ribbon emblem, a gold cap', 'GRAND MARINER', 2.2),
    'liqueur_kafa': (
        'a dark brown glass coffee liqueur bottle with a yellow label with red '
        'wave accents, a yellow cap', 'KOALA', 2.3),
}
