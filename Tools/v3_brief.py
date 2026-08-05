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
    # The EMPTY block at full strength (the author, 2026-08-05, third telling:
    # "içerisinde sıvı olmadan boş şişe olarak üreteceksin") — softer wordings kept
    # letting pooled liquid through, and the gate then had to reject the take.
    return ('%s, the bottle is about %.1f times as TALL as it is WIDE, '
            'the bottle is COMPLETELY EMPTY with absolutely no liquid inside - '
            'no fill level, no coloured contents, no pool at the bottom, the '
            'inside shows only pale empty glass all the way down to the base, '
            'with the word "%s" written in large clear letters on the label'
            % (dress, ratio, brand))


def call_args(bottle_id):
    dress, brand, ratio = BOTTLES[bottle_id]
    args = {'description': build(dress, brand, ratio), 'view': VIEW}
    args.update(CANVAS)
    args.update(KNOBS)
    return args


def build2(dress, brand, ratio):
    """The FILL-FIRST brief (the author, 2026-08-05): the game draws the liquid level
    inside these bottles, so the design is asked to serve the level — a large clear
    unobstructed glass body below a COMPACT label, nothing inside, cap on. The brand
    parody stays in the dress clause; what changes is what the bottle is FOR."""
    return ('%s, the bottle is DESIGNED AROUND ITS GLASS: the body below the label '
            'is large plain see-through glass with no decoration on it, the label '
            'sits compact on the UPPER HALF of the body and never covers the lower '
            'half, the bottle is about %.1f times as TALL as it is WIDE, '
            'the bottle is COMPLETELY EMPTY - no liquid, no fill level, no coloured '
            'contents, plain pale empty glass all the way down to the base, '
            'with the word "%s" written in clear letters on the label'
            % (dress, ratio, brand))


def call_args2(bottle_id):
    dress, brand, ratio = BOTTLES[bottle_id]
    args = {'description': build2(dress, brand, ratio), 'view': VIEW}
    args.update(CANVAS)
    args.update(KNOBS)
    return args


# id -> (dress: the real bottle's design, echoed and shifted; brand; ratio).
# One clause per bottle. The cap is part of the dress, because it generates on.
BOTTLES = {
    # ── vodka: Smirnoff / Absolut / Grey Goose / Beluga ─────────────────────────
    # The author's reference photo, measured band by band (2026-08-05): RED cap on
    # top, grey neck, the RED banner across the LABEL'S TOP at ~40% height, white
    # plate below with a small red seal. The cap is red, not silver.
    'vodka_astra': (
        'a WIDE-BODIED clear colourless glass vodka bottle with a shiny RED metal '
        'cap, a large PURE WHITE paper label with one bold SIGNAL RED banner band '
        'across its TOP and a small red circular seal emblem below it, thin '
        'silver-grey accents', 'SMIRKOFF', 2.2),
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
        'a CLEAR COLOURLESS glass rum bottle with a dark label showing a small '
        'standing sea-captain figure, a gold cap', 'ADMIRAL MORGAN', 2.3),
    'rum_windward': (
        'a squat black glass rum bottle with two small glass handles at the neck '
        'and a black label with a sea-monster emblem, a black cap', 'KRAKATOA', 2.0),
    # "gerçek orijinal şişeye benzesin" + low-res complaint (2026-08-05): the mark
    # is the stout dark bottle with the parchment label; asked with more precision.
    # Emissary retired (2026-08-05): the parody is MALIBU - the smooth opaque
    # white bottle with the palm.
    # Round two (2026-08-05): the author wants the REAL Malibu silhouette and
    # nothing like the previous attempt - the domed cylinder with no shoulders.
    'rum_reina_del_mar': (
        'a pure white glossy cylindrical bottle whose rounded DOME top slopes '
        'directly into a very short neck with no shoulders, a round printed '
        'emblem of a green palm tree against an orange sunset on its front, '
        'a flat white cap', 'MALIBOO', 2.4),
    # ── bourbon: Jim Beam / Jack Daniel's / Maker's Mark / Pappy Van Winkle ─────
    # Jim Beam retired (2026-08-05): the parody is JOHNNIE WALKER - the square
    # bottle with the SLANTED label and the striding man.
    'bourbon_redline': (
        'a square clear glass whiskey bottle with a SLANTED rectangular gold-framed '
        'label and a small striding walking man emblem, a black cap',
        'WANDERER', 2.2),
    # "şişenin şekli biraz daha benzetilsin orijinaline" (2026-08-05): the square is
    # the whole identity — flat front, hard bevelled shoulders, fluted short neck.
    'bourbon_old_harrow': (
        'a SQUARE CLEAR COLOURLESS glass whiskey bottle with a flat front, hard bevelled square '
        'shoulders and a short fluted neck, a black label with a thin white frame, '
        'a black cap', "JACK SPANIEL'S", 2.1),
    'bourbon_ashfall': (
        'a squat round CLEAR COLOURLESS glass whiskey bottle with a cream label, its neck '
        'sealed in dripping amber wax', "MASON'S MARK", 2.0),
    # "etiketin üstünde sigara içen adam portresi" (2026-08-05): the real label's
    # identity is the smoking gentleman, so the parody carries its own.
    'bourbon_hollow_oak': (
        'a rounded clear glass whiskey bottle with an aged cream paper label '
        'carrying a small pixel art portrait of an old gentleman smoking a cigar, '
        'a gold foil cap', 'VAN WRINKLE', 2.3),
    # ── tequila: Jose Cuervo / 1800 / Don Julio / Clase Azul ────────────────────
    # "şişe orijinal markanın şişesine daha çok benzesin" (2026-08-05): Cuervo
    # Especial reads as the amber-gold squared bottle with the dark band label.
    'tequila_sonora': (
        'an amber-gold tinted glass tequila bottle with a squared body and rounded '
        'shoulders, a cream label with a wide black band and small red crest, '
        'a black cap', 'JOSE CUERDO', 2.2),
    'tequila_alta_luna': (
        'a trapezoid glass tequila bottle with sloped shoulders and a wide flat '
        'stopper top, a small brown label', '1810', 2.0),
    # "orijinal markanın orijinal şişesine daha çok benzesin" (2026-08-05): Don
    # Julio's shape IS the short round body with the tall thin neck.
    'tequila_sol_viejo': (
        'a SHORT round-bodied clear glass tequila bottle with soft round shoulders '
        'and a TALL THIN NECK, a simple cream label with a small blue agave plant '
        'emblem, a small brown cap', 'DON JULEP', 1.8),
    'tequila_cielo_roto': (
        'a white ceramic tequila bottle with cobalt blue vertical fluting and a '
        'rounded ceramic stopper', 'AZULEJO', 2.6),
    # ── the singles: Campari / Cinzano / Grand Marnier / Kahlua ─────────────────
    # The red was the DRINK (the author, 2026-08-05): the empty bottle is clear.
    'amaro_notte': (
        'a squat CLEAR COLOURLESS glass amaro bottle with conical shoulders and a '
        'white label with a blue band, a white cap', 'CUMPARI', 2.0),
    'vermouth_velvet': (
        'a dark green glass vermouth bottle with a large label of red and blue '
        'halves, a gold cap', 'CANZONE', 2.4),
    'liqueur_delia': (
        'a round-bellied brown glass orange liqueur bottle with a narrow neck, a '
        'cream label with a thin red ribbon emblem, a gold cap', 'GRAND MARINER', 2.2),
    # "tasarımı kötü, kesin değiş" (2026-08-05): rebuilt on Kahlua's real dress —
    # sloped shoulders, the big warm yellow label, the red ribbon wave.
    'liqueur_kafa': (
        'a CLEAR COLOURLESS glass coffee liqueur bottle with smoothly sloping shoulders, '
        'a large warm yellow label with a red wavy ribbon band across its middle, '
        'a red cap', 'KOALA', 2.2),
}


# ── the soft-drink vessels (2026-08-05: "şimdi görsel üretilecekler") ────────────
# kind decides the chain: 'can' and 'carton' are SEALED vessels (BottleArt draws no
# drink into them) and ship as one sprite; 'bottle' takes the full sandwich.
# The empty-bottle law applies to the bottles here too.
MIXERS = {
    'syrup_house': ('bottle',
        'a small CLEAR COLOURLESS glass syrup bottle with a metal pour spout and a '
        'plain white label', 'SYRUP', 2.4),
    'lemon_fresh': ('carton',
        'a small bright yellow juice carton with a lemon illustration on its front',
        'LEMONADE', 1.6),
    'lime_fresh': ('carton',
        'a small bright green juice carton with a lime illustration on its front',
        'LIMEADE', 1.6),
    'orange_grove': ('carton',
        'a small orange juice carton with an orange fruit illustration on its front',
        'ORANGE', 1.6),
    'soda_klara': ('bottle',
        'a CLEAR COLOURLESS glass soda water bottle with fluted sides and a '
        'swing-top wire cap, a small blue label', 'KLARA', 2.6),
    'ginger_kicker': ('bottle',
        'a squat brown glass ginger beer bottle with a wide body and a yellow '
        'label', 'KICKER', 2.0),
    'tonic_quinbury': ('bottle',
        'a CLEAR COLOURLESS glass tonic water bottle with a bright yellow label, '
        'a silver cap', 'QUINNS', 2.5),
    'grenadine_rubis': ('bottle',
        'a slim CLEAR COLOURLESS glass syrup bottle with a deep red label and a '
        'red cap', 'RUBIS', 2.6),
    'pineapple_isla': ('carton',
        'a small juice carton with a pineapple illustration on its front',
        'PINA', 1.6),
    'cranberry_north': ('carton',
        'a small dark red juice carton with a cranberry illustration on its front',
        'CRANBERRY', 1.6),
    'energy_volt': ('can',
        'a slim silver and blue aluminium energy drink can with a bold bull-ox '
        'emblem', 'BLUE OX', 2.1),
    'cola_marlow': ('can',
        'a red aluminium cola drink can with a white wavy stripe across it',
        'LOCA COLA', 1.7),
}
