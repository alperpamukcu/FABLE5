# -*- coding: utf-8 -*-
"""THE FROZEN BRIEF for every v4 vessel. Nothing is generated except through build().

GDD 25 §5a's lesson, paid for three times: hand-written briefs reopened solved failures —
liquid pooled back in, labels came back, the pitch went flat. So the words that hold those
gates are fixed here, and the only variables per card are its LOOK sentence and its ratio
(Docs/PLAN_bottle_art_v4.md §7).

Two blocks are at full strength in every prompt and are not negotiable:
  EMPTY     the vessel is drawn with nothing in it — the drink is the game's to draw
  NO LABEL  the body carries no label, no print, no lettering — the label is the pipeline's
            to press, which is what makes the cavity geometry and the label opacity exact
"""

TOOL = 'create_image_pro'
CANVAS = {'width': 96, 'height': 192}          # both divisible by 4 (PixelLab) and by 3 (cellar ÷3)
CELLAR = {'width': 32, 'height': 64}
SEEDS = (23,)                                 # ONE take a card (the author, 2026-09-04: quota)

EMPTY = ('The bottle is COMPLETELY EMPTY: absolutely no liquid inside, no fill level, no '
         'coloured contents, no pool at the bottom; the inside shows only pale clear empty '
         'glass all the way down to the base. ')
# THE MASTER IS OPEN (the author, 2026-08-27: "orijinal boyutta kapak olmayacak"). The hand
# bottle is the one being poured from, so the generator draws it uncapped - a real open
# mouth from this camera, a rim and a dark throat - and the cellar copy gets a small drawn
# cap from the pipeline. No cap seam to find, no mouth to paint.
OPEN = ('The bottle is OPEN with NO CAP, NO CORK and NO STOPPER: the bare neck ends in an open '
        'mouth, and from this slightly-above camera the mouth shows as a thin glass rim '
        'around a small dark opening. ')
# THE LABEL IS THE GENERATOR'S (the author, 2026-09-04: "etiket yazi marka logo her neyi
# varsa" — draw it with its label, its name and its logo). The pipeline no longer presses
# one; the film pass keeps printed pixels opaque so the label stays in front of the drink.
LABEL = ('It has its own brand label on the body with the name "%s" written on it and a '
         'small simple logo, in flat colours. ')
CAMERA = ('Seen straight on from slightly above (about 17 degrees), so the cap top and the '
          'shoulders show as shallow ellipses and the base edge bows gently downward; the '
          'bottle is left-right symmetric. ')
STYLE = ('Clean hi-bit pixel art, flat fills and ramp steps, no dither, no texture noise, '
         'matte with very little shine — at most one faint thin highlight on the left glass '
         'wall, no reflections, no glow — a thin single-colour black outline, drawn as a '
         'single flat layer showing only the bottle front, transparent background. ')
SEALED_NOTE = ('It is a sealed opaque container, drawn closed. ')

# id -> (family, ratio, look, label_ramp, band_ramp, emblem)
#   family     silhouette family (PLAN §7) — drives the label rect and the open-state rule
#   ratio      height / width, the one number per card the generator is told
#   look       the LOOK sentence: glass colour, silhouette, cap — NO label words, ever
#   label_ramp / band_ramp   palette ramps (palette.RAMPS keys) for the pressed label
#   emblem     the 32×32 emblem prompt (no text) for the label's medallion; None = no emblem
CARDS = {
    # ── vodka: tall straight shoulder, narrow neck, screw cap ──────────────────
    'vodka_astra':      ('vodka', 2.5, 'a tall slim clear-glass vodka bottle with straight shoulders', 'Cream', 'ClubBlue', 'a small stylised silver crane bird, flat pixel emblem, no text'),
    'vodka_vor':        ('vodka', 2.5, 'a tall slim clear-glass vodka bottle with a very plain cylindrical body, minimal shoulders', 'Cream', 'Graphite', 'a small stylised apothecary bottle silhouette, flat pixel emblem, no text'),
    'vodka_leonid':     ('vodka', 2.5, 'a tall frosted pale-grey glass vodka bottle with straight shoulders', 'Graphite', 'Cream', 'a small stylised grey goose in flight, flat pixel emblem, no text'),
    'vodka_okhta':      ('vodka', 2.5, 'a tall clear-glass vodka bottle with a faintly blue tint, elegant straight shoulders', 'ClubBlue', 'Cream', 'a small stylised white whale, flat pixel emblem, no text'),
    # ── gin: squat, broad shoulder, cork + capsule ─────────────────────────────
    'gin_boothby':      ('gin', 2.1, 'a squat dark-green glass gin bottle with broad shoulders and a short neck', 'Cream', 'Lime', 'a small stylised heraldic crest with a crown, flat pixel emblem, no text'),
    'gin_juniper_crow': ('gin', 2.1, 'a squat pale-green glass gin bottle with broad shoulders', 'Cream', 'ViceRed', 'a small stylised top hat, flat pixel emblem, no text'),
    'gin_thornwood':    ('gin', 2.1, 'a squat clear-glass gin bottle with a wide round body like a medicine flask', 'Cream', 'Night', 'a small stylised rose flower, flat pixel emblem, no text'),
    'gin_veilcrest':    ('gin', 2.1, 'a squat dark-blue glass gin bottle with broad shoulders', 'Cream', 'Amber', 'a small stylised gibbon face, flat pixel emblem, no text'),
    # ── rum: round shoulder, slightly bellied, cork ─────────────────────────────
    'rum_cane_coral':   ('rum', 2.2, 'a clear-glass rum bottle with soft round shoulders and a slightly bellied body', 'Cream', 'Magenta', 'a small stylised bat with spread wings, flat pixel emblem, no text'),
    'rum_tidewater':    ('rum', 2.2, 'an amber-tinted glass rum bottle with round shoulders', 'Amber', 'ViceRed', 'a small stylised admiral in a tricorn hat, flat pixel emblem, no text'),
    'rum_windward':     ('rum', 2.2, 'a dark near-black glass rum bottle with round shoulders', 'Night', 'ViceRed', 'a small stylised erupting volcano, flat pixel emblem, no text'),
    'rum_reina_del_mar':('rum', 2.2, 'a clear-glass rum bottle with round shoulders', 'Cream', 'Cyan', 'a small stylised palm tree on a beach, flat pixel emblem, no text'),
    # ── whiskey: square/broad body, short neck ─────────────────────────────────
    'bourbon_redline':  ('whiskey', 2.0, 'a square-shouldered clear-glass whiskey bottle with a broad flat-sided body', 'Night', 'ViceRed', 'a small stylised striding man in a top hat, flat pixel emblem, no text'),
    'bourbon_old_harrow':('whiskey', 2.0, 'a square-shouldered clear-glass whiskey bottle with a broad body', 'Night', 'Cream', 'a small stylised hound dog head, flat pixel emblem, no text'),
    'bourbon_ashfall':  ('whiskey', 1.9, 'a heavy squat clear-glass whiskey bottle with a broad body and a short neck', 'Cream', 'ViceRed', 'a small stylised ornate M monogram seal, flat pixel emblem, no text'),
    'bourbon_hollow_oak':('whiskey', 2.0, 'a broad-shouldered clear-glass whiskey bottle with a slightly tapered body', 'Amber', 'Night', 'a small stylised old man face in profile, flat pixel emblem, no text'),
    # ── tequila: long neck, narrow base ─────────────────────────────────────────
    'tequila_sonora':   ('tequila', 2.3, 'a clear-glass tequila bottle with a long neck, gently sloping shoulders', 'Cream', 'Amber', 'a small stylised black crow, flat pixel emblem, no text'),
    'tequila_alta_luna':('tequila', 2.3, 'a clear-glass tequila bottle with a long neck, square shoulders', 'Cream', 'Night', 'a small stylised crescent moon, flat pixel emblem, no text'),
    'tequila_sol_viejo':('tequila', 2.3, 'a clear-glass tequila bottle with a long neck, square shoulders and a wide low body', 'Amber', 'Lime', 'a small stylised agave plant, flat pixel emblem, no text'),
    'tequila_cielo_rojo':('tequila', 2.3, 'a hand-blown clear-glass tequila bottle with a blue-tinted rim, a long neck and a bulbous lower body', 'ClubBlue', 'Cream', 'a small stylised blue sun with rays, flat pixel emblem, no text'),
    # ── singles: slim, high shoulder ────────────────────────────────────────────
    'amaro_notte':      ('liqueur', 2.4, 'a tall slim clear-glass aperitivo bottle with high shoulders', 'ViceRed', 'Cream', 'a small stylised orange slice, flat pixel emblem, no text'),
    'vermouth_velvet':  ('liqueur', 2.4, 'a tall slim clear-glass vermouth bottle with high shoulders', 'Lime', 'Cream', 'a small stylised vine leaf, flat pixel emblem, no text'),
    'liqueur_delia':    ('liqueur', 2.4, 'a slim clear-glass liqueur bottle with a tapered body', 'Amber', 'ViceRed', 'a small stylised sailing ship, flat pixel emblem, no text'),
    'liqueur_kafa':     ('liqueur', 2.4, 'a slim dark-brown glass liqueur bottle with high shoulders', 'Malt', 'Cream', 'a small stylised koala face, flat pixel emblem, no text'),
    # ── beers: sealed brown glass ───────────────────────────────────────────────
    'beer_kestrel':     ('beer', 2.6, 'a long-neck brown glass beer bottle with a gold crown cap', 'Amber', 'ClubBlue', 'a small stylised kestrel bird, flat pixel emblem, no text'),
    'beer_collier':     ('beer', 2.6, 'a long-neck dark brown glass stout bottle with a black crown cap', 'Night', 'Cream', 'a small stylised harp, flat pixel emblem, no text'),
    'beer_marigold':    ('beer', 2.6, 'a long-neck brown glass ale bottle with a red crown cap', 'Amber', 'ViceRed', 'a small stylised brass triangle, flat pixel emblem, no text'),
    # ── cans ────────────────────────────────────────────────────────────────────
    'cola_marlow':      ('can', 2.0, 'a plain red aluminium soft drink can with a silver top and pull tab, matte flat red', 'ViceRed', 'Cream', 'a small stylised white ribbon swoosh, flat pixel emblem, no text'),
    'energy_volt':      ('can', 2.0, 'a plain slim blue and silver aluminium energy drink can with a silver top and pull tab', 'ClubBlue', 'Cream', 'a small stylised ox head, flat pixel emblem, no text'),
    # ── cartons ─────────────────────────────────────────────────────────────────
    'orange_grove':     ('carton', 2.0, 'a plain orange juice carton, a gable-top box with a small white screw cap, flat orange sides', 'Amber', 'Cream', 'a small stylised orange fruit with a leaf, flat pixel emblem, no text'),
    'lemon_fresh':      ('carton', 2.0, 'a plain yellow lemonade carton, a gable-top box with a small white screw cap, flat yellow sides', 'Amber', 'Cream', 'a small stylised lemon with a leaf, flat pixel emblem, no text'),
    'lime_fresh':       ('carton', 2.0, 'a plain green limeade carton, a gable-top box with a small white screw cap, flat green sides', 'Lime', 'Cream', 'a small stylised lime with a leaf, flat pixel emblem, no text'),
    'cranberry_north':  ('carton', 2.0, 'a plain deep red cranberry juice carton, a gable-top box with a small white screw cap, flat red sides', 'ViceRed', 'Cream', 'a small stylised cluster of three red berries, flat pixel emblem, no text'),
    'pineapple_isla':   ('carton', 2.0, 'a plain golden-yellow pineapple juice carton, a gable-top box with a small white screw cap, flat yellow sides', 'Amber', 'Lime', 'a small stylised pineapple, flat pixel emblem, no text'),
    # ── mixer glass bottles ────────────────────────────────────────────────────
    'tonic_quinbury':   ('mixer', 2.4, 'a slim clear-glass tonic water bottle with a fluted body', 'Cream', 'Cyan', 'a small stylised cinchona leaf, flat pixel emblem, no text'),
    'soda_klara':       ('mixer', 2.4, 'a slim clear-glass soda water bottle with a plain body', 'Cream', 'ClubBlue', 'a small stylised rising bubble trio, flat pixel emblem, no text'),
    'ginger_kicker':    ('mixer', 2.4, 'a slim amber-tinted glass ginger beer bottle with a plain body', 'Amber', 'Night', 'a small stylised kicking boot, flat pixel emblem, no text'),
    'syrup_house':      ('mixer', 2.4, 'a slim clear-glass syrup bottle with a plain body', 'Cream', 'Amber', None),
}

BRAND_WORD = {
    'vodka_astra': 'SMIRKOFF', 'vodka_vor': 'ABSOLVE', 'vodka_leonid': 'GANDER', 'vodka_okhta': 'WHALE',
    'gin_boothby': "GARDEN'S", 'gin_juniper_crow': 'LEAFEATER', 'gin_thornwood': "HENDRAKE'S", 'gin_veilcrest': 'GIBBON 48',
    'rum_cane_coral': 'WHITE BAT', 'rum_tidewater': 'ADMIRAL', 'rum_windward': 'KRAKATOA', 'rum_reina_del_mar': 'MALIBOO',
    'bourbon_redline': 'WALKER', 'bourbon_old_harrow': 'SPANIEL', 'bourbon_ashfall': "MASON'S", 'bourbon_hollow_oak': 'WRINKLE',
    'tequila_sonora': 'CUERDO', 'tequila_alta_luna': '1810', 'tequila_sol_viejo': 'JULEP', 'tequila_cielo_rojo': 'AZULEJO',
    'amaro_notte': 'CUMPARI', 'vermouth_velvet': 'VELVET', 'liqueur_delia': 'MARINER', 'liqueur_kafa': 'KOALA',
    'beer_kestrel': 'KRONA', 'beer_collier': 'GOODNESS', 'beer_marigold': 'BRASS',
    'cola_marlow': 'LOCA', 'energy_volt': 'BLUE OX', 'orange_grove': 'GROVE', 'lemon_fresh': 'LEMONADE',
    'lime_fresh': 'LIMEADE', 'cranberry_north': 'NORTH', 'pineapple_isla': 'ISLA',
    'tonic_quinbury': "QUINN'S", 'soda_klara': 'KLARA', 'ginger_kicker': 'KICKER', 'syrup_house': 'HOUSE',
}

SEALED = {'can', 'carton', 'beer'}     # no cavity, no liquid plates — one sprite + derived open
GLASS = {'vodka', 'gin', 'rum', 'whiskey', 'tequila', 'liqueur', 'mixer'}


def build(card_id):
    fam, ratio, look, _, _, _ = CARDS[card_id]
    body = ('%s, about %.1f times as tall as it is wide. ' % (look, ratio))
    label = LABEL % BRAND_WORD.get(card_id, card_id.split('_')[0].title())
    if fam in SEALED:
        return body + SEALED_NOTE + label + CAMERA + STYLE
    return body + EMPTY + OPEN + label + CAMERA + STYLE


def emblem_prompt(card_id):
    e = CARDS[card_id][5]
    if not e:
        return None
    return e + ', centred on a transparent background, clean hi-bit pixel art, flat fills, thin dark outline'


def family(card_id):
    return CARDS[card_id][0]


if __name__ == '__main__':
    import sys
    for cid in (sys.argv[1:] or ['vodka_astra']):
        print('==', cid)
        print(build(cid))
        print('   emblem:', emblem_prompt(cid))
