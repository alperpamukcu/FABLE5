# -*- coding: utf-8 -*-
"""THE SHELF STYLE (2026-09-27): the 29 glass bottles drawn by ONE hand, at 32x64 and 96x192.

The author's reference (a back bar of two shelves, every bottle plainly by the same artist)
against our set: ours are area averages of 29 separately generated masters, so each keeps its
own generator's light, its own shine, its own label idea. The reference's sameness is a RECIPE
applied to every bottle, and its variety is in the SHAPES:

  * one light, from the upper left: a dark rim column, ONE bright highlight stripe a fifth of
    the way in, a lit band, the body, a shade band, a reflected rim on the right; a square
    bottle is a flat face between two bevels instead
  * the shoulder catches the light (a bright arc wherever the glass faces up)
  * a thick glass foot, two dark rows under the drink
  * a capsule or a cap built the same way on every bottle, shaded with the same bands
  * the label is paper + one darker border + the brand's mark + bars for its lettering

Round two (the author, 2026-09-27: "2.5d olmalı ki raflara tam otursun ... logolar biraz daha
belirgin büyük ... daha çok çeşitli şişe çeşitleri olabilir, tüm şişeler birbirine benzemek
zorunda değil"): every bottle is now a SHAPE of its own (SPEC — square whiskies, apothecary
gins, a round-bellied rum, a pyramid tequila, a Grand-Marnier ball, short mixers), seen from
the game's 17-degree camera: the cap's top is an ellipse, a round bottle's foot bows down by
15% of its width, the label wraps the body, the empty bottle shows its floor as an ellipse.
One shape table draws both sizes, so the cellar copy and the hand bottle are the same drawing
at two resolutions (PLAN v4 §3, "iki boyut, tek kimlik").

The cellar plates keep the room's three files (back / mask / front at 32x64) so they ship by
swapping PNGs. Writes to staging/shelf_style/ only.
"""
import colorsys
import json
import math
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import brief      # noqa: E402
import fontpx     # noqa: E402
import palette    # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
ITEMS = os.path.join(ROOT, 'Assets', 'Resources', 'Items')
OUT = os.path.join(HERE, 'staging', 'shelf_style')

INK = palette.INK
W0, H0 = 32, 64                      # the cellar canvas; the hand canvas is three times it


def rp(name, i):
    return palette.ramp(name, i)


def col(spec):
    return rp(spec[:-1], int(spec[-1]))


# ── the shapes ───────────────────────────────────────────────────────────────
# In CELLAR pixels (the hand bottle is the same numbers x3). Widths are even: the canvas centre
# sits between two pixels, so every row is symmetric about it.
#   ht     rows from the cap's top to the foot's lowest row (the foot's bow included)
#   bw nw  body and neck width
#   neck   neck rows (the cap sits over its top)
#   sh     shoulder rows, and how the shoulder turns: square | round | slope | none
#   body   straight | belly | taper | pyramid | ball | pear
#   square a flat front face between two bevels, a flat foot (a box seen straight on)
#   lip    a glass ring under the cap
# The parody names point at the real bottle each is a nod to.
SPEC = {
    # vodka
    # ROUND FIVE (the author, 2026-09-27: five favourite designs replace five look-alikes, their
    # design fitted to the brand they now dress): Chambord's crowned ball for Smirkoff (its mark
    # is a crown), Galliano's lighthouse cone for Cuerdo, Frangelico's monk for Cumpari (a
    # monastery's herbal liqueur), Hibiki's faceted decanter for Van Wrinkle, Baileys for Koala.
    'vodka_astra':        dict(ht=44, bw=26, nw=6, neck=10, sh=0, shoulder='none', body='ball',
                               belt=(0.46, 'Amber'), label='round'),                                 # after Chambord
    'vodka_vor':          dict(ht=54, bw=16, nw=6, neck=6,  sh=5, shoulder='round', body='straight', lip=True,
                               label='print'),                                                       # Absolut: printed glass
    'vodka_leonid':       dict(ht=62, bw=16, nw=6, neck=9,  sh=14, shoulder='slope', body='taper',
                               label='window'),                                                      # Grey Goose: a clear window
    'vodka_okhta':        dict(ht=60, bw=16, nw=6, neck=7,  sh=3, shoulder='square', body='straight',
                               square=True, label='tall'),                                           # Belvedere: a tall block
    # gin
    'gin_boothby':        dict(ht=50, bw=22, nw=8, neck=7,  sh=7, shoulder='step', body='straight',
                               ribs=(0.34, 0.68), label='seal'),                                     # Tanqueray: shaker, red seal
    'gin_juniper_crown':  dict(ht=60, bw=16, nw=6, neck=12, sh=6, shoulder='slope', body='straight', lip=True,
                               necklabel=('Cream4', 'ViceRed2'), label='arch'),                      # Beefeater
    'gin_thornwood':      dict(ht=46, bw=22, nw=8, neck=5,  sh=5, shoulder='round', body='straight',
                               label='diamond', cork_band='Night'),                                  # Hendrick's
    'gin_veilcrest':      dict(ht=44, bw=20, nw=8, neck=6,  sh=5, shoulder='round', body='straight',
                               label='round', cork_band='Graphite'),                                 # Monkey 47
    # rum
    'rum_cane_coral':     dict(ht=58, bw=16, nw=6, neck=12, sh=3, shoulder='square', body='straight', label='rect'),  # Bacardi
    'rum_tidewater':      dict(ht=58, bw=18, nw=6, neck=11, sh=7, shoulder='round', body='belly', label='oval'),     # Captain Morgan
    'rum_windward':       dict(ht=54, bw=24, nw=8, neck=12, sh=0, shoulder='none', body='ball',
                               handles=True, label='banner'),                                        # Kraken
    'rum_reina_del_mar':  dict(ht=52, bw=18, nw=6, neck=7,  sh=4, shoulder='round', body='straight', label='print'),  # Malibu
    # whiskey
    'bourbon_redline':    dict(ht=54, bw=24, nw=8, neck=9,  sh=4, shoulder='square', body='straight', square=True,
                               label='slant'),                                                       # Johnnie Walker
    'bourbon_old_harrow': dict(ht=58, bw=22, nw=8, neck=13, sh=6, shoulder='slope', body='straight', square=True,
                               flutes=True, label='rect', necklabel=('Night1', 'Cream3')),           # Jack Daniel's
    'bourbon_ashfall':    dict(ht=50, bw=22, nw=8, neck=8,  sh=6, shoulder='round', body='straight', square=True,
                               label='torn'),                                                        # Maker's Mark
    'bourbon_hollow_oak': dict(ht=54, bw=22, nw=8, neck=9,  sh=7, shoulder='round', body='straight', cap=6,
                               facets=True, label='high'),                                           # after Hibiki
    # tequila
    'tequila_sonora':     dict(ht=62, bw=16, nw=4, neck=6,  sh=0, shoulder='none', body='cone', label='tall',
                               label_at=0.78),                                                       # after Galliano
    'tequila_alta_luna':  dict(ht=58, bw=20, nw=6, neck=12, sh=3, shoulder='square', body='pyramid',
                               label='embossed'),                                                    # 1800: raised glass
    'tequila_sol_viejo':  dict(ht=46, bw=22, nw=8, neck=10, sh=3, shoulder='square', body='straight', square=True,
                               label='high'),                                                        # Don Julio
    'tequila_cielo_roto': dict(ht=62, bw=20, nw=6, neck=21, sh=0, shoulder='none', body='pear',
                               label='painted'),                                                     # Clase Azul: painted ceramic
    # singles
    'amaro_notte':        dict(ht=54, bw=18, nw=6, neck=4,  sh=12, shoulder='slope', body='pyramid', cap=8,
                               belt=(0.28, 'Cream'), label='oval'),                                  # after Frangelico
    'vermouth_velvet':    dict(ht=62, bw=16, nw=6, neck=17, sh=4, shoulder='round', body='straight',
                               band='Amber', label='wrap'),                                          # a Bordeaux bottle
    'liqueur_delia':      dict(ht=50, bw=26, nw=6, neck=21, sh=0, shoulder='none', body='ball', cap=6,
                               ribbon='ViceRed', label='rect'),                                      # Grand Marnier
    'liqueur_kafa':       dict(ht=56, bw=18, nw=6, neck=12, sh=10, shoulder='round', body='taper', label='wrap'),  # after Baileys
    # mixers
    'tonic_quinbury':     dict(ht=40, bw=12, nw=6, neck=8,  sh=6, shoulder='slope', body='straight', label='wrap'),  # a tonic
    'soda_klara':         dict(ht=44, bw=14, nw=6, neck=12, sh=4, shoulder='round', body='straight',
                               neck_shape='codd', label='embossed'),                                 # Codd: embossed glass
    'ginger_kicker':      dict(ht=34, bw=16, nw=8, neck=6,  sh=4, shoulder='round', body='straight', label='shield'),  # stubby
    'syrup_house':        dict(ht=50, bw=14, nw=6, neck=8,  sh=3, shoulder='square', body='straight', square=True,
                               label='rect'),                                                        # Monin
    # Rubis redrawn (the author, round seven: "Rubis şişeleri bozuk, tekrardan üret"): the cruet's
    # tag hung past the glass and its pear read as a skittle; now a syrup bottle of the Rose's kind,
    # round-shouldered, a lip under the cap, an arch label with a pomegranate
    'grenadine_rubis':    dict(ht=52, bw=16, nw=6, neck=12, sh=6, shoulder='round', body='straight', lip=True,
                               label='arch'),
    # the sodas as large PET bottles (the author: "yaygın markaların büyük boy şişelerinin
    # tasarımları sırıtmayabilir rafta"): our own designs, a litre each
    'cola_marlow':        dict(ht=58, bw=16, nw=6, neck=6,  sh=9, shoulder='slope', body='contour', label='wrap',
                               neckring=True, petaloid=True, grip=(0.72, 0.95)),                    # a contour litre
    'energy_volt':        dict(ht=54, bw=16, nw=6, neck=5,  sh=5, shoulder='round', body='waist', label='wrap',
                               neckring=True, petaloid=True),                                        # a sports litre
}

# ── NEW BOTTLE TYPES: candidates from real bottles, not cards in the game ──────
EXTRA = {
    'x_bitters':    dict(ht=32, bw=10, nw=4, neck=6,  sh=4, shoulder='round', body='straight', label='oversize'),  # Angostura
    'x_maraschino': dict(ht=58, bw=16, nw=6, neck=16, sh=6, shoulder='slope', body='straight', straw=0.55,
                         label='high'),                                                              # Luxardo
    'x_hazelnut':   dict(ht=54, bw=18, nw=6, neck=4,  sh=12, shoulder='slope', body='pyramid', cap=8,
                         belt=(0.28, 'Cream'), label='oval'),                                        # Frangelico: the monk
    'x_galliano':   dict(ht=62, bw=16, nw=4, neck=6,  sh=0, shoulder='none', body='cone', label='tall'),  # Galliano
    'x_chambord':   dict(ht=44, bw=26, nw=6, neck=10, sh=0, shoulder='none', body='ball',
                         belt=(0.46, 'Amber'), label='round'),                                       # Chambord
    'x_jager':      dict(ht=56, bw=20, nw=6, neck=12, sh=5, shoulder='slope', body='straight', square=True,
                         label='round'),                                                             # Jagermeister
    'x_amaretto':   dict(ht=50, bw=22, nw=8, neck=6,  sh=4, shoulder='round', body='straight', square=True,
                         label='oval'),                                                              # Disaronno
    'x_cointreau':  dict(ht=52, bw=20, nw=6, neck=10, sh=4, shoulder='square', body='straight', square=True,
                         ribbon='ViceRed', label='rect'),                                            # Cointreau
    'x_hibiki':     dict(ht=54, bw=22, nw=8, neck=9,  sh=7, shoulder='round', body='straight', cap=6,
                         facets=True, label='high'),                                                 # Hibiki: 24 facets
    'x_irishcream': dict(ht=56, bw=18, nw=6, neck=12, sh=10, shoulder='round', body='taper', label='wrap'),  # Baileys
    'x_aperitivo':  dict(ht=60, bw=14, nw=6, neck=14, sh=8, shoulder='slope', body='straight', label='arch'),  # Aperol
    'x_sake':       dict(ht=40, bw=18, nw=4, neck=10, sh=0, shoulder='none', body='pear', label='painted'),  # a tokkuri
}
SPEC.update(EXTRA)

GLASS_OF = {
    'vodka_astra': 'clear', 'vodka_vor': 'clear', 'vodka_leonid': 'frost', 'vodka_okhta': 'bluetint',
    'gin_boothby': 'green', 'gin_juniper_crown': 'palegreen', 'gin_thornwood': 'smoke', 'gin_veilcrest': 'blue',
    'rum_cane_coral': 'clear', 'rum_tidewater': 'amber', 'rum_windward': 'black', 'rum_reina_del_mar': 'milk',
    'bourbon_redline': 'clear', 'bourbon_old_harrow': 'clear', 'bourbon_ashfall': 'clear', 'bourbon_hollow_oak': 'clear',
    'tequila_sonora': 'clear', 'tequila_alta_luna': 'clear', 'tequila_sol_viejo': 'clear', 'tequila_cielo_roto': 'ceramic',
    'amaro_notte': 'clear', 'vermouth_velvet': 'bottlegreen', 'liqueur_delia': 'clear', 'liqueur_kafa': 'brown',
    'tonic_quinbury': 'emerald', 'soda_klara': 'aqua', 'ginger_kicker': 'amber', 'syrup_house': 'clear',
    'grenadine_rubis': 'clear', 'cola_marlow': 'pet', 'energy_volt': 'pet',
    'x_bitters': 'clear', 'x_maraschino': 'green', 'x_hazelnut': 'brown', 'x_galliano': 'clear',
    'x_chambord': 'clear', 'x_jager': 'bottlegreen', 'x_amaretto': 'clear', 'x_cointreau': 'amber',
    'x_hibiki': 'clear', 'x_irishcream': 'smoke', 'x_aperitivo': 'clear', 'x_sake': 'ceramic',
}
# closure: (kind, ramp)  kind: screw | capsule | wax | cork | crown | stopper | pourer | codd | crowncap | block | open
CLOSURE = {
    'vodka_astra': ('crowncap', 'Amber'), 'vodka_vor': ('screw', 'Graphite'), 'vodka_leonid': ('screw', 'Graphite'),
    'vodka_okhta': ('screw', 'ClubBlue'),
    'gin_boothby': ('capsule', 'ViceRed'), 'gin_juniper_crown': ('capsule', 'Graphite'), 'gin_thornwood': ('cork', 'Night'),
    'gin_veilcrest': ('cork', 'Malt'),
    'rum_cane_coral': ('capsule', 'Graphite'), 'rum_tidewater': ('capsule', 'ViceRed'), 'rum_windward': ('capsule', 'Night'),
    'rum_reina_del_mar': ('screw', 'Night'),
    'bourbon_redline': ('capsule', 'Night'), 'bourbon_old_harrow': ('capsule', 'Night'), 'bourbon_ashfall': ('wax', 'ViceRed'),
    'bourbon_hollow_oak': ('stopper', 'Amber'),
    'tequila_sonora': ('screw', 'Amber'), 'tequila_alta_luna': ('cork', 'Malt'), 'tequila_sol_viejo': ('cork', 'Amber'),
    'tequila_cielo_roto': ('stopper', 'Amber'),
    'amaro_notte': ('stopper', 'Malt'), 'vermouth_velvet': ('screw', 'Amber'), 'liqueur_delia': ('wax', 'ViceRed'),
    'liqueur_kafa': ('screw', 'Night'),
    'tonic_quinbury': ('screw', 'Cyan'), 'soda_klara': ('codd', 'Cyan'), 'ginger_kicker': ('crown', 'Amber'),
    'syrup_house': ('pourer', 'Graphite'), 'grenadine_rubis': ('screw', 'ViceRed'),
    'cola_marlow': ('screw', 'ViceRed'), 'energy_volt': ('screw', 'Night'),
    'x_bitters': ('screw', 'Amber'), 'x_maraschino': ('capsule', 'ViceRed'), 'x_hazelnut': ('stopper', 'Malt'),
    'x_galliano': ('screw', 'Amber'), 'x_chambord': ('crowncap', 'Amber'), 'x_jager': ('capsule', 'Amber'),
    'x_amaretto': ('block', 'Amber'), 'x_cointreau': ('capsule', 'Brick'), 'x_hibiki': ('stopper', 'Amber'),
    'x_irishcream': ('screw', 'Night'), 'x_aperitivo': ('screw', 'Amber'), 'x_sake': ('open', 'Cream'),
}


# A glass kind: the EMPTY interior seen through it (back plate, opaque, three steps from the
# walls in) and the film over everything inside (front plate): per band, a colour and an alpha.
def _glass(back, rim, hi, lite, mid, shade, rrim, foot):
    return {'back': back, 'rim': rim, 'hi': hi, 'lite': lite, 'mid': mid, 'shade': shade,
            'rrim': rrim, 'foot': foot}


GLASS = {
    'clear': _glass(back=(rp('Graphite', 3), rp('Graphite', 4), rp('Graphite', 4)),
                    rim=(rp('Graphite', 3), 255), hi=(rp('Cream', 4), 235), lite=(rp('Cream', 4), 40),
                    mid=(rp('Graphite', 4), 18), shade=(rp('Night', 1), 95), rrim=(rp('Graphite', 4), 255),
                    foot=(rp('Graphite', 2), rp('Graphite', 3), rp('Graphite', 4))),
    'frost': _glass(back=(rp('Graphite', 2), rp('Graphite', 3), rp('Graphite', 3)),
                    rim=(rp('Graphite', 3), 255), hi=(rp('Cream', 4), 235), lite=(rp('Cream', 3), 80),
                    mid=(rp('Cream', 3), 60), shade=(rp('Graphite', 1), 110), rrim=(rp('Graphite', 4), 255),
                    foot=(rp('Graphite', 2), rp('Graphite', 3), rp('Graphite', 4))),
    'bluetint': _glass(back=(rp('Graphite', 3), rp('Graphite', 4), rp('Graphite', 4)),
                       rim=(rp('ClubBlue', 2), 255), hi=(rp('Cream', 4), 235), lite=(rp('ClubBlue', 4), 45),
                       mid=(rp('ClubBlue', 3), 30), shade=(rp('ClubBlue', 0), 100), rrim=(rp('ClubBlue', 3), 255),
                       foot=(rp('ClubBlue', 1), rp('ClubBlue', 2), rp('ClubBlue', 3))),
    'green': _glass(back=(rp('Lime', 0), rp('Lime', 0), rp('Lime', 1)),
                    rim=(rp('Lime', 1), 255), hi=(rp('Lime', 4), 240), lite=(rp('Lime', 3), 110),
                    mid=(rp('Lime', 2), 120), shade=(rp('Lime', 0), 160), rrim=(rp('Lime', 2), 255),
                    foot=(rp('Lime', 0), rp('Lime', 1), rp('Lime', 2))),
    'palegreen': _glass(back=(rp('Lime', 0), rp('Lime', 1), rp('Lime', 1)),
                        rim=(rp('Lime', 2), 255), hi=(rp('Lime', 4), 240), lite=(rp('Lime', 4), 60),
                        mid=(rp('Lime', 3), 50), shade=(rp('Lime', 0), 120), rrim=(rp('Lime', 3), 255),
                        foot=(rp('Lime', 1), rp('Lime', 2), rp('Lime', 3))),
    'blue': _glass(back=(rp('ClubBlue', 0), rp('ClubBlue', 0), rp('ClubBlue', 1)),
                   rim=(rp('ClubBlue', 1), 255), hi=(rp('ClubBlue', 4), 240), lite=(rp('ClubBlue', 3), 110),
                   mid=(rp('ClubBlue', 2), 130), shade=(rp('ClubBlue', 0), 170), rrim=(rp('ClubBlue', 2), 255),
                   foot=(rp('ClubBlue', 0), rp('ClubBlue', 1), rp('ClubBlue', 2))),
    'amber': _glass(back=(rp('Malt', 0), rp('Malt', 0), rp('Malt', 1)),
                    rim=(rp('Malt', 1), 255), hi=(rp('Amber', 4), 240), lite=(rp('Malt', 3), 90),
                    mid=(rp('Malt', 2), 100), shade=(rp('Malt', 0), 150), rrim=(rp('Malt', 2), 255),
                    foot=(rp('Malt', 0), rp('Malt', 1), rp('Malt', 2))),
    'brown': _glass(back=(rp('Malt', 0), rp('Brick', 0), rp('Malt', 1)),
                    rim=(rp('Malt', 0), 255), hi=(rp('Malt', 4), 235), lite=(rp('Malt', 2), 110),
                    mid=(rp('Malt', 1), 140), shade=(rp('Night', 1), 170), rrim=(rp('Malt', 1), 255),
                    foot=(rp('Malt', 0), rp('Malt', 0), rp('Malt', 1))),
    'black': _glass(back=(rp('Night', 0), rp('Night', 1), rp('Night', 1)),
                    rim=(rp('Night', 1), 255), hi=(rp('Graphite', 4), 235), lite=(rp('Night', 3), 120),
                    mid=(rp('Night', 2), 160), shade=(rp('Night', 0), 190), rrim=(rp('Night', 3), 255),
                    foot=(rp('Night', 0), rp('Night', 1), rp('Night', 2))),
    # Hendrick's jar: a dark red-brown, lighter than the rum's black and redder than the Kahlua's
    'smoke': _glass(back=(rp('Brick', 0), rp('Brick', 0), rp('Brick', 1)),
                    rim=(rp('Brick', 1), 255), hi=(rp('Amber', 4), 230), lite=(rp('Brick', 3), 90),
                    mid=(rp('Brick', 2), 110), shade=(rp('Night', 1), 160), rrim=(rp('Brick', 2), 255),
                    foot=(rp('Brick', 0), rp('Brick', 1), rp('Brick', 2))),
    # milk glass (Malibu): white and nearly opaque; the drink shows as a faint tint through it
    'milk': _glass(back=(rp('Cream', 3), rp('Cream', 4), rp('Cream', 4)),
                   rim=(rp('Cream', 2), 255), hi=(rp('Cream', 4), 255), lite=(rp('Cream', 4), 205),
                   mid=(rp('Cream', 4), 185), shade=(rp('Cream', 2), 200), rrim=(rp('Cream', 3), 255),
                   foot=(rp('Cream', 2), rp('Cream', 3), rp('Cream', 4))),
    # a wine bottle's deep green (vermouth, Jagermeister)
    'bottlegreen': _glass(back=(rp('Lime', 0), rp('Night', 1), rp('Lime', 0)),
                          rim=(rp('Lime', 0), 255), hi=(rp('Lime', 3), 220), lite=(rp('Lime', 2), 90),
                          mid=(rp('Lime', 1), 130), shade=(rp('Night', 1), 170), rrim=(rp('Lime', 1), 255),
                          foot=(rp('Night', 1), rp('Lime', 0), rp('Lime', 1))),
    # a tonic's emerald: cooler and lighter than the gin's green
    'emerald': _glass(back=(rp('Cyan', 0), rp('Lime', 0), rp('Cyan', 1)),
                      rim=(rp('Cyan', 1), 255), hi=(rp('Cyan', 4), 235), lite=(rp('Lime', 3), 80),
                      mid=(rp('Cyan', 2), 100), shade=(rp('Cyan', 0), 150), rrim=(rp('Cyan', 2), 255),
                      foot=(rp('Cyan', 0), rp('Cyan', 1), rp('Cyan', 2))),
    # painted ceramic (Clase Azul, a sake flask): white, nearly opaque, the drink a ghost behind it
    'ceramic': _glass(back=(rp('Cream', 3), rp('Cream', 4), rp('Cream', 4)),
                      rim=(rp('Cream', 2), 255), hi=(rp('Cream', 4), 255), lite=(rp('Cream', 4), 225),
                      mid=(rp('Cream', 4), 210), shade=(rp('Cream', 3), 225), rrim=(rp('Cream', 3), 255),
                      foot=(rp('Cream', 2), rp('Cream', 3), rp('Cream', 3))),
    # PET: thin clear plastic, a touch blue, bright where it bends
    'pet': _glass(back=(rp('Graphite', 3), rp('Graphite', 4), rp('Graphite', 4)),
                  rim=(rp('ClubBlue', 3), 255), hi=(rp('Cream', 4), 245), lite=(rp('ClubBlue', 4), 36),
                  mid=(rp('ClubBlue', 4), 30), shade=(rp('ClubBlue', 1), 80), rrim=(rp('ClubBlue', 4), 255),
                  foot=(rp('ClubBlue', 2), rp('ClubBlue', 3), rp('ClubBlue', 4))),
    # a Codd bottle's aqua
    'aqua': _glass(back=(rp('Cyan', 0), rp('Cyan', 1), rp('Cyan', 1)),
                   rim=(rp('Cyan', 2), 255), hi=(rp('Cyan', 4), 240), lite=(rp('Cyan', 4), 60),
                   mid=(rp('Cyan', 3), 50), shade=(rp('Cyan', 0), 120), rrim=(rp('Cyan', 3), 255),
                   foot=(rp('Cyan', 1), rp('Cyan', 2), rp('Cyan', 3))),
}

# the shipped liquid tones (UITheme.LiquidColors), by card, for the preview composite only
LIQUID = {
    'vodka': (0xAB, 0xD7, 0xF4), 'gin': (0xA4, 0xDE, 0xCE), 'rum': (0xAE, 0x5F, 0x16),
    'bourbon': (0xCF, 0x7F, 0x1D), 'tequila': (0xE0, 0xEE, 0x8E), 'amaro': (0xC9, 0x38, 0x2C),
    'vermouth': (0x83, 0x27, 0x46), 'liqueur_delia': (0xFF, 0xB2, 0x48), 'liqueur_kafa': (0x5C, 0x2E, 0x0E),
    'grenadine': (0xB0, 0x14, 0x2E), 'ginger': (0xE3, 0xC0, 0x66), 'soda': (0x99, 0xCE, 0xF4),
    'syrup': (0xF9, 0xE7, 0xA2), 'tonic': (0x85, 0xC5, 0xF7),
    # the candidates' drinks, for the preview only
    'cola_marlow': (0x93, 0x41, 0x1B), 'energy_volt': (0xCF, 0xE5, 0x3A),
    'x_bitters': (0x6E, 0x1B, 0x32), 'x_maraschino': (0xE4, 0xEA, 0xF2), 'x_hazelnut': (0xC9, 0x8F, 0x2B),
    'x_galliano': (0xF2, 0xD2, 0x3A), 'x_chambord': (0x5C, 0x1B, 0x45), 'x_jager': (0x3A, 0x24, 0x10),
    'x_amaretto': (0xB5, 0x65, 0x1D), 'x_cointreau': (0xE8, 0xEE, 0xF2), 'x_hibiki': (0xD9, 0x9A, 0x3A),
    'x_irishcream': (0xD9, 0xC0, 0xA0), 'x_aperitivo': (0xF2, 0x6B, 0x1D), 'x_sake': (0xEE, 0xF2, 0xE6),
}


def liquid_of(cid):
    return LIQUID.get(cid) or LIQUID[cid.split('_')[0]]


def vivid(c):
    """UITheme.Vivid: the room pushes every drink the same step up in chroma."""
    h, s, v = colorsys.rgb_to_hsv(*(x / 255.0 for x in c))
    s = min(1.0, s * 1.35 + 0.03); v = min(1.0, v * 1.03)
    return tuple(int(round(x * 255)) for x in colorsys.hsv_to_rgb(h, s, v))


def lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


# ── the labels: one hand, one language ───────────────────────────────────────
# Paper, a darker border with its corners cut, a bar above, the mark, bars below for the
# lettering. The marks are drawn here in one hand, bold (two-pixel strokes where they are
# letters) so they read at the room's size, and keep the motif the shipped label carried.
#   k = the label's ink   a = its accent   w = cream highlight   . = paper
ICONS = {
    'crown':   ['a..a..a', 'aa.a.aa', 'aaaaaaa', 'akakaka', 'aaaaaaa', 'kkkkkkk'],
    'A':       ['..kkk..', '.kk.kk.', 'kk...kk', 'kkkkkkk', 'kk...kk', 'kk...kk'],
    'goose':   ['...kk..', '..kkkaa', '..kk...', '..kk...', '.kkkk..', 'kkkkkkk', '.kkkkk.'],
    'whale':   ['......kk', '.kkkk.k.', 'kkkkkkk.', 'kwkkkkk.', 'kkkkkk..', '.kkkk...'],
    'G':       ['.kkkk.', 'kk..kk', 'kk....', 'kk.kkk', 'kk..kk', '.kkkk.'],
    'leaf':    ['..k..', '.kak.', 'kakak', 'kakak', 'kakak', '.kkk.', '..k..', '..k..'],
    'rose':    ['.aaaa.', 'aakkaa', 'akaaka', 'aakkaa', '.aaaa.', '..k...', '.kkk..', '..k...'],
    'monkey':  ['..aaa..', '.aaaaa.', 'aawwwaa', 'awkwkwa', '.wwwww.', '.wkkkw.', '..www..'],
    'bat':     ['k..k.k..k', 'kk.kkk.kk', 'kkkkkkkkk', 'kk.kkk.kk', 'k...k...k'],
    'anchor':  ['...k...', '..k.k..', '...k...', '.kkkkk.', '...k...', 'k..k..k', '.kkkkk.'],
    'volcano': ['...a.a...', '....a....', '...aaa...', '...kak...', '..kkkkk..', '.kkkkkkk.', 'kkkkkkkkk'],
    'palm':    ['.aa.aa.', 'aaaaaaa', 'a..k..a', '...k...', '...k...', '..kk...', '.kkkkk.'],
    'W':       ['kk....kk', 'kk....kk', 'kk.kk.kk', 'kkkkkkkk', 'kkk..kkk', 'kk....kk'],
    'S':       ['.kkkkk', 'kk....', 'kk....', '.kkkk.', '....kk', '....kk', 'kkkkk.'],
    'M':       ['kk...kk', 'kkk.kkk', 'kkkkkkk', 'kk.k.kk', 'kk...kk', 'kk...kk'],
    'acorn':   ['...k...', '.kkkkk.', 'kkkkkkk', '.aaaaa.', '.aaaaa.', '..aaa..', '...a...'],
    'crow':    ['....kk..', '...kkkaa', '..kkkk..', '.kkkkk..', 'kkkkk...', '..k.k...'],
    'moon':    ['..kkk.', '.kk...', 'kk....', 'kk....', 'kk....', '.kk...', '..kkk.'],
    'agave':   ['k...k...k', '.k..k..k.', '..k.k.k..', 'k.kkkkk.k', '.kkkkkkk.', '..kkkkk..'],
    'sun':     ['a..a..a', '.a.a.a.', '..aaa..', 'aaakaaa', '..aaa..', '.a.a.a.', 'a..a..a'],
    'slice':   ['..kkk..', '.kaaak.', 'kaakaak', 'kkkkkkk', 'kaakaak', '.kaaak.', '..kkk..'],
    'V':       ['kk..kk', 'kk..kk', 'kk..kk', '.kkkk.', '.kkkk.', '..kk..'],
    'ship':    ['...k....', '...kk...', '...kkk..', '...kkkk.', '...k....', 'kkkkkkkk', '.kkkkkk.'],
    'koala':   ['aa....aa', 'aaaaaaaa', '.akaaka.', '.aakkaa.', '.aakkaa.', '..aaaa..'],
    'Q':       ['.kkkk.', 'kk..kk', 'kk..kk', 'kk..kk', 'kk.kkk', '.kkkk.', '....kk'],
    'K':       ['kk..kk', 'kk.kk.', 'kkkk..', 'kkkk..', 'kk.kk.', 'kk..kk'],
    'boot':    ['.kkk...', '.kkk...', '.kkk...', '.kkk...', '.kkkkk.', 'kkkkkkk', 'kkkkkkk'],
    'house':   ['...k...', '..kkk..', '.kkkkk.', 'kkkkkkk', '.kkakk.', '.kkakk.', '.kkakk.'],
    'drop':    ['..a..', '..a..', '.aaa.', 'aaaaa', 'awaaa', 'aaaaa', '.aaa.'],
    'pomegranate': ['.k.k.', '..k..', '.aaa.', 'aawaa', 'aaaaa', 'aaaaa', '.aaa.'],
    'cherry':  ['...kk..', '..k..k.', '.k....k', 'aa...aa', 'awa.awa', 'aaa.aaa'],
    'nut':     ['.kkk.', 'kkkkk', 'aaaaa', 'awaaa', 'aaaaa', '.aaa.'],
    'stag':    ['k.k.k.k', '.kkkkk.', '...k...', '..kkk..', '.kkwkk.', '..kkk..', '...k...'],
    'star':    ['...a...', '..aaa..', 'aaaaaaa', '.aaaaa.', '.aa.aa.'],
    'C':       ['.kkkk', 'kk...', 'kk...', 'kk...', 'kk...', '.kkkk'],
    'B':       ['kkkk.', 'kk.kk', 'kkkk.', 'kk.kk', 'kk.kk', 'kkkk.'],
    'dot':     ['.aaa.', 'aaaaa', 'aaaaa', 'aaaaa', '.aaa.'],
    'none':    ['.'],
    'ox':      ['k.......k', 'kk.aaa.kk', '.kaaaaak.', '..kkkkk..', '..kwkwk..', '...kkk...', '....k....'],
}

# card -> (mark, paper, border, ink, accent, bar)
LABELS = {
    'vodka_astra':        ('crown',   'Cream4',    'Amber2',    'ClubBlue1', 'ViceRed2',  'ClubBlue2'),
    'vodka_vor':          ('A',       'Cream4',    'Cream2',    'ClubBlue1', 'ClubBlue1', 'ClubBlue2'),
    'vodka_leonid':       ('goose',   'Cream4',    'Graphite3', 'Graphite1', 'Amber3',    'ClubBlue2'),
    'vodka_okhta':        ('whale',   'ClubBlue4', 'ClubBlue1', 'ClubBlue0', 'Cream4',    'ClubBlue1'),
    'gin_boothby':        ('G',       'Cream3',    'Lime0',     'Lime0',     'Lime0',     'Lime1'),
    'gin_juniper_crown':  ('leaf',    'Cream4',    'Cream2',    'Lime0',     'Lime3',     'ViceRed2'),
    'gin_thornwood':      ('rose',    'Cream4',    'Night1',    'Night1',    'ViceRed2',  'Night1'),
    'gin_veilcrest':      ('monkey',  'Amber4',    'Amber2',    'Night1',    'ViceRed2',  'ClubBlue1'),
    'rum_cane_coral':     ('bat',     'Cream4',    'Cream2',    'Night1',    'Night1',    'Magenta2'),
    'rum_tidewater':      ('anchor',  'Cream4',    'Cream2',    'ViceRed1',  'ViceRed1',  'ViceRed2'),
    'rum_windward':       ('volcano', 'Cream3',    'Cream1',    'Night1',    'ViceRed3',  'ViceRed2'),
    'rum_reina_del_mar':  ('palm',    'Cream4',    'Cream2',    'Malt1',     'Lime2',     'Cyan2'),
    'bourbon_redline':    ('W',       'ViceRed2',  'Night1',    'Cream4',    'Cream4',    'Night1'),
    'bourbon_old_harrow': ('S',       'Night1',    'Cream2',    'Cream4',    'Cream4',    'Cream3'),
    'bourbon_ashfall':    ('M',       'Cream4',    'Cream2',    'ViceRed1',  'ViceRed1',  'ViceRed2'),
    'bourbon_hollow_oak': ('acorn',   'Cream3',    'Malt1',     'Malt0',     'Amber2',    'Malt0'),
    'tequila_sonora':     ('crow',    'Cream4',    'Cream2',    'Night1',    'Amber3',    'Amber2'),
    'tequila_alta_luna':  ('moon',    'Cream4',    'Cream2',    'Night1',    'Night1',    'Night1'),
    'tequila_sol_viejo':  ('agave',   'Amber4',    'Amber2',    'Lime1',     'Lime1',     'Lime2'),
    'tequila_cielo_roto': ('sun',     'Cream4',    'ClubBlue2', 'ClubBlue1', 'ClubBlue3', 'ClubBlue2'),
    'amaro_notte':        ('slice',   'Cream4',    'Malt1',     'ViceRed1',  'Amber3',    'Malt1'),
    'vermouth_velvet':    ('V',       'Night1',    'Amber2',    'Amber4',    'Amber4',    'Amber2'),
    'liqueur_delia':      ('ship',    'Cream4',    'Cream2',    'ClubBlue1', 'ClubBlue1', 'ViceRed2'),
    'liqueur_kafa':       ('koala',   'Cream4',    'Cream2',    'Night1',    'Graphite3', 'Malt1'),
    'tonic_quinbury':     ('Q',       'Cream4',    'Cream2',    'Cyan1',     'Cyan1',     'Cyan2'),
    'soda_klara':         ('K',       'ClubBlue3', 'ClubBlue1', 'Cream4',    'Cream4',    'Cream4'),
    'ginger_kicker':      ('boot',    'Amber4',    'Amber2',    'Night1',    'Night1',    'Night1'),
    'syrup_house':        ('house',   'Cream4',    'Cream2',    'Amber1',    'Amber3',    'Amber2'),
    'grenadine_rubis':    ('pomegranate', 'Cream4', 'ViceRed1', 'ViceRed1',  'ViceRed2',  'ViceRed2'),
    'cola_marlow':        ('none',    'ViceRed2',  'ViceRed0',  'Cream4',    'Cream4',    'Cream4'),
    'energy_volt':        ('ox',      'ClubBlue2', 'ClubBlue0', 'ViceRed2',  'Amber3',    'Graphite4'),
    'x_bitters':          ('A',       'Cream4',    'Cream2',    'ViceRed1',  'ViceRed1',  'Night1'),
    'x_maraschino':       ('cherry',  'Cream4',    'ViceRed1',  'Lime0',     'ViceRed2',  'ViceRed2'),
    'x_hazelnut':         ('nut',     'Cream4',    'Malt1',     'Malt0',     'Amber2',    'Malt1'),
    'x_galliano':         ('star',    'Amber4',    'Amber2',    'Night1',    'ViceRed2',  'Night1'),
    'x_chambord':         ('crown',   'Amber4',    'Amber2',    'Night1',    'Magenta1',  'Night1'),
    'x_jager':            ('stag',    'Amber3',    'Night1',    'Night1',    'Night1',    'Lime0'),
    'x_amaretto':         ('A',       'Cream4',    'Amber2',    'Malt0',     'Malt0',     'Amber2'),
    'x_cointreau':        ('C',       'Cream4',    'ViceRed1',  'ViceRed1',  'ViceRed1',  'Amber2'),
    'x_hibiki':           ('sun',     'Cream4',    'Cream2',    'Night1',    'Amber3',    'Night1'),
    'x_irishcream':       ('B',       'Cream4',    'Malt1',     'Malt0',     'Malt0',     'Malt1'),
    'x_aperitivo':        ('slice',   'Amber3',    'ViceRed1',  'ViceRed1',  'Cream4',    'ViceRed1'),
    'x_sake':             ('dot',     'Cream4',    'ClubBlue2', 'ClubBlue1', 'ViceRed2',  'ClubBlue2'),
}
SLANTED = {'bourbon_redline'}          # the walker's label climbs to the right
STYLE_WORD = {'vodka': 'VODKA', 'gin': 'GIN', 'rum': 'RUM', 'bourbon': 'WHISKEY', 'tequila': 'TEQUILA',
              'amaro': 'AMARO', 'vermouth': 'VERMOUTH', 'liqueur': 'LIQUEUR', 'tonic': 'TONIC',
              'soda': 'SODA', 'ginger': 'GINGER', 'syrup': 'SYRUP', 'grenadine': 'GRENADINE'}


def scale3x(grid):
    """AdvMAME3x on a character grid: the mark three times bigger with its diagonals kept
    smooth, so the hand bottle's label is the cellar's mark at three times the resolution
    rather than a mosaic of it."""
    h, w = len(grid), len(grid[0])

    def g(x, y):
        return grid[y][x] if 0 <= x < w and 0 <= y < h else '.'
    out = [[None] * (w * 3) for _ in range(h * 3)]
    for y in range(h):
        for x in range(w):
            A, B, C = g(x - 1, y - 1), g(x, y - 1), g(x + 1, y - 1)
            D, E, F = g(x - 1, y), g(x, y), g(x + 1, y)
            G, Hh, I = g(x - 1, y + 1), g(x, y + 1), g(x + 1, y + 1)
            e = [E] * 9
            if B != Hh and D != F:
                e[0] = D if D == B else E
                e[1] = B if (D == B and E != C) or (B == F and E != A) else E
                e[2] = F if B == F else E
                e[3] = D if (D == B and E != G) or (D == Hh and E != A) else E
                e[5] = F if (B == F and E != I) or (Hh == F and E != C) else E
                e[6] = D if D == Hh else E
                e[7] = Hh if (D == Hh and E != I) or (Hh == F and E != G) else E
                e[8] = F if Hh == F else E
            for k in range(9):
                out[y * 3 + k // 3][x * 3 + k % 3] = e[k]
    return [''.join(r) for r in out]


# ── the silhouette ───────────────────────────────────────────────────────────
def _shoulder(style, u):
    if style == 'square':
        return math.sqrt(u)
    if style == 'round':
        return math.sqrt(max(0.0, 1 - (1 - u) ** 2))
    if style == 'step':
        # the shaker's two tiers: a step out, a ledge, a second step to the body
        if u < 0.4:
            return 0.55 * math.sqrt(u / 0.4)
        if u < 0.6:
            return 0.55
        return 0.55 + 0.45 * math.sqrt((u - 0.6) / 0.4)
    return 3 * u * u - 2 * u * u * u                       # slope: an S from neck to body


def _body(style, v):
    if style == 'belly':
        return 0.9 + 0.1 * math.sin(math.pi * v)
    if style == 'taper':
        return 1.0 - 0.1 * v
    if style == 'pyramid':
        return 0.72 + 0.28 * v
    if style == 'ball':
        return math.sqrt(max(0.0, 1 - ((v - 0.52) / 0.56) ** 2))
    if style == 'contour':
        return 1.0 - 0.10 * math.sin(math.pi * min(1.0, max(0.0, (v - 0.05) / 0.5)))
    if style == 'waist':
        return 1.0 - 0.12 * math.exp(-((v - 0.55) / 0.16) ** 2)
    if style == 'pear':
        if v <= 0.72:
            return 0.55 + 0.45 * math.sin(v / 0.72 * math.pi / 2)
        return 1 - 0.25 * ((v - 0.72) / 0.28) ** 2
    return 1.0


def half_width(sp, r):
    """Half the bottle's width (cellar px) at r cellar rows under the cap's top."""
    ht, bw, nw = sp['ht'], sp['bw'], sp['nw']
    neck, sh = sp['neck'], sp['sh']
    square = sp.get('square', False)
    body_end = ht if square else ht - bow_rows(sp)
    if r < neck:
        if sp.get('neck_shape') == 'codd':
            # a Codd neck: the lip, the chamber the marble rattles in, the pinch that holds it
            k = r / float(neck)
            if k < 0.15:
                return nw / 2.0 + 1
            if k < 0.55:
                return nw / 2.0 + 2
            if k < 0.72:
                return nw / 2.0 - 1
        return nw / 2.0
    if r < neck + sh:
        b0 = bw / 2.0 * _body(sp['body'], 0.0)
        return nw / 2.0 + (b0 - nw / 2.0) * _shoulder(sp['shoulder'], (r - neck + 0.5) / sh)
    if r < body_end:
        v = (r - neck - sh) / float(max(1, body_end - neck - sh))
        if sp['body'] == 'cone':
            return nw / 2.0 + (bw / 2.0 - nw / 2.0) * v
        hw = max(nw / 2.0, bw / 2.0 * _body(sp['body'], v))
        for f in sp.get('ribs', ()):
            if abs(r - rib_row(sp, f)) < 0.75:
                hw += 1.0                                  # the shaker's joints stand proud
        return hw
    # the foot of a round bottle: the near half of an ellipse, 15% of the width deep
    b_end = bw / 2.0 * _body(sp['body'], 1.0)
    k = (r - body_end) / float(bow_rows(sp))
    return b_end * math.sqrt(max(0.0, 1 - k * k))


def rib_row(sp, f):
    body_end = sp['ht'] if sp.get('square') else sp['ht'] - bow_rows(sp)
    return sp['neck'] + sp['sh'] + f * (body_end - sp['neck'] - sp['sh'])


def bow_rows(sp):
    if sp.get('square'):
        return 0
    b_end = sp['bw'] * _body(sp['body'], 1.0)
    return max(1, int(round(0.15 * b_end)))


class Shape:
    """The bottle rasterised at scale S: its row spans, its parts, and each column's foot."""

    def __init__(self, cid, S=1, sp=None):
        self.cid, self.S = cid, S
        sp = self.sp = sp or SPEC[cid]
        self.W, self.H = W0 * S, H0 * S
        self.cx2 = self.W                                  # the centre, doubled (between two pixels)
        self.top = self.H - 1 - int(round(sp['ht'] * S))
        self.bot = self.H - 2
        self.spans = {}
        for y in range(self.top, self.bot + 1):
            r = (y - self.top + 0.5) / S
            hw = half_width(sp, r) * S
            w = 2 * int(round(hw))
            if sp.get('square') and y == self.bot:
                w -= 2                                     # a box's foot: its corners turned
            if w >= 2:
                self.spans[y] = ((self.W - w) // 2, (self.W + w) // 2 - 1)
        self.top = min(self.spans)
        self.neck_end = self.top + int(round(sp['neck'] * S)) - 1
        self.shoulder_end = self.top + int(round((sp['neck'] + sp['sh']) * S)) - 1
        self.colbot = {}
        for y, (x0, x1) in self.spans.items():
            for x in range(x0, x1 + 1):
                self.colbot[x] = max(self.colbot.get(x, y), y)

    def inside(self, x, y):
        s = self.spans.get(y)
        return bool(s) and s[0] <= x <= s[1]

    def width(self, y):
        s = self.spans.get(y)
        return s[1] - s[0] + 1 if s else 0


# ── the one hand ─────────────────────────────────────────────────────────────
def bands(n, square=False, S=1):
    """The light across one row of n pixels, left to right."""
    if n <= 0:
        return []
    if n == 1:
        return ['hi']
    if n == 2:
        return ['hi', 'shade']
    if n == 3:
        return ['rim', 'hi', 'rrim']
    edge = max(1, S // 2 + (1 if S > 1 else 0))           # the rim is one pixel small, two big
    hiw = max(1, int(round(n * 0.07)))                      # the highlight widens with the glass
    out = []
    if square:
        # a flat face between two bevels: lit bevel on the left, the face, a dark bevel right
        bev = max(1, int(round(n * 0.12)))
        for i in range(n):
            if i < edge:
                b = 'rim'
            elif i < edge + bev:
                b = 'hi' if i == edge else 'hi2'
            elif i >= n - edge:
                b = 'rrim'
            elif i >= n - edge - bev:
                b = 'shade'
            else:
                b = 'lite' if i < n * 0.35 else 'mid'
            out.append(b)
        return out
    hi0 = max(edge, int(round(0.2 * (n - 1))))
    for i in range(n):
        u = i / float(n - 1)
        if i < edge:
            b = 'rim'
        elif i >= n - edge:
            b = 'rrim'
        elif hi0 <= i < hi0 + hiw:
            b = 'hi'
        elif i == hi0 + hiw and n >= 18:
            b = 'hi2'
        elif S >= 3 and (i == edge or i == n - 1 - edge):
            b = 'wall'                                      # the glass's own thickness
        elif S >= 3 and n >= 30 and abs(u - 0.86) < 0.5 / n * 2:
            b = 'refl'                                      # the room's light off the far side
        elif u < 0.44:
            b = 'lite'
        elif u < 0.70:
            b = 'mid'
        else:
            b = 'shade'
        out.append(b)
    return out


OPAQUE_STEP = {'rim': 2, 'hi': 4, 'hi2': 3, 'lite': 3, 'mid': 3, 'shade': 2, 'rrim': 1, 'wall': 2, 'refl': 3}


def shade_opaque(ramp, band, lift=0):
    # the Night ramp stops at Night1: Night0 is the outline's ink, and a cap shaded into it fuses with the ring
    return rp(ramp, max(1 if ramp == 'Night' else 0, min(4, OPAQUE_STEP[band] + lift)))


def cap_rows(cid, sp=None):
    kind = CLOSURE[cid][0]
    sp = sp or SPEC[cid]
    if 'cap' in sp:
        return sp['cap']
    neck = sp['neck']
    if kind in ('codd', 'open'):
        return 0                                           # sealed from inside, or no closure at all
    if kind in ('crowncap', 'block'):
        return 5
    if kind == 'crown':
        return 3
    if kind == 'screw':
        return min(4, max(3, neck // 2))
    if kind == 'wax':
        return max(4, int(round(neck * 0.5)))
    if kind == 'cork':
        return 4
    if kind == 'pourer':
        return 7
    if kind == 'stopper':
        return 9
    return max(4, int(round(neck * 0.45)))


def draw(cid, S=1, open_mouth=False, sp=None):
    """(back, mask, front, shape). open_mouth draws the hand bottle: no closure, the rim and the
    dark throat seen from 17 degrees. sp: the card's shape scaled for the hand (hand_spec)."""
    sh = Shape(cid, S, sp)
    sp = sh.sp
    g = GLASS[GLASS_OF[cid]]
    kind, cramp = CLOSURE[cid]
    square = sp.get('square', False)
    Wd, Ht = sh.W, sh.H
    back = Image.new('RGBA', (Wd, Ht), (0, 0, 0, 0)); bp = back.load()
    mask = Image.new('RGBA', (Wd, Ht), (0, 0, 0, 0)); mp = mask.load()
    front = Image.new('RGBA', (Wd, Ht), (0, 0, 0, 0)); fp = front.load()
    foot_t = 2 * S
    top = sh.top
    nominal_top = Ht - 1 - int(round(sp['ht'] * S))
    crow = 0 if (open_mouth or kind in ('codd', 'open')) else int(round(cap_rows(cid, sp) * S))
    # the neck's bands, collars and rings stand where the CAPPED bottle puts them, open or not
    crow_cap = 0 if kind in ('codd', 'open') else int(round(cap_rows(cid, sp) * S))

    def clear(x, y):
        fp[x, y] = (0, 0, 0, 0); bp[x, y] = (0, 0, 0, 0); mp[x, y] = (0, 0, 0, 0)

    # 1. the glass
    for y, (x0, x1) in sh.spans.items():
        n = x1 - x0 + 1
        bl = bands(n, square, S)
        for i, x in enumerate(range(x0, x1 + 1)):
            b = bl[i]
            if y > sh.colbot[x] - foot_t:
                d, m, l = g['foot']
                c = l if b in ('hi', 'hi2') else (d if b in ('rim', 'rrim', 'shade') or y == sh.colbot[x] else m)
                fp[x, y] = c + (255,)
                continue
            if b in ('rim', 'rrim'):
                fp[x, y] = g[b][0] + (255,)
                continue
            k = abs(i / float(max(1, n - 1)) - 0.5) * 2
            bc = g['back'][2] if k < 0.35 else (g['back'][1] if k < 0.75 else g['back'][0])
            bp[x, y] = bc + (255,)
            mp[x, y] = (255, 255, 255, 255)
            if b == 'wall':
                colr, a = g['rim'][0], 150
            elif b == 'refl':
                colr, a = g['hi'][0], 110
            else:
                colr, a = g['hi'] if b == 'hi2' else g[b]
            if b == 'hi2':
                a = 150
            if y > sh.neck_end:
                # THE SHOULDER CATCHES THE LIGHT: glass that faces up is lit. A square
                # bottle's shoulder is a flat top seen from above: the whole band is lit.
                up = any(not sh.inside(x, y - d) for d in range(1, max(1, S - 1) + 1))
                if up or (square and y <= sh.shoulder_end):
                    colr, a = g['hi'][0], (215 if x * 2 < sh.cx2 else 130)
            fp[x, y] = colr + (a,)

    # 2. the floor: an empty round bottle shows its base as an ellipse (back plate only)
    if not square:
        cb = {}
        for x in range(Wd):
            ys = [y for y in range(Ht) if mp[x, y][3]]
            if ys:
                cb[x] = ys[-1]
        if cb:
            edge = min(cb.values())
            for x, yb in cb.items():
                ytop = edge - (yb - edge)
                for y in range(ytop, yb + 1):
                    if bp[x, y][3]:
                        r_, i_ = ramp_of(bp[x, y][:3])
                        bp[x, y] = rp(r_, min(4, i_ + 1)) + (255,)

    # 3. what the glass itself is made with
    if sp.get('flutes'):
        # Jack's fluted shoulder: ribs of glass running down it, lit and shaded in turn
        for y in range(sh.neck_end + 1, sh.shoulder_end + 1):
            s = sh.spans.get(y)
            if not s:
                continue
            for x in range(s[0] + S, s[1] - S + 1):
                if mp[x, y][3]:
                    # counted from the centre, not the row's edge, so a flute runs straight down
                    # the slope instead of stepping sideways into a checker
                    fp[x, y] = (g['hi'][0] + (190,)) if ((x * 2 - sh.cx2) // (2 * S)) % 2 == 0 else (g['shade'][0] + (170,))
    for f in sp.get('ribs', ()):
        # the shaker's joints: a lit ridge and its shadow under it
        yc = nominal_top + int(round(rib_row(sp, f) * S))
        for y, a_col in ((yc, g['hi']), (yc + S, g['shade'])):
            for yy in range(y, y + S):
                s = sh.spans.get(yy)
                if not s:
                    continue
                for x in range(s[0] + S, s[1] - S + 1):
                    if mp[x, yy][3]:
                        fp[x, yy] = a_col[0] + (200,)
    if sp.get('band'):
        yb0 = top + crow_cap + max(1, (sh.neck_end - top - crow_cap) // 2) - S
        for y in range(yb0, yb0 + 2 * S):
            s = sh.spans.get(y)
            if not s:
                continue
            bl = bands(s[1] - s[0] + 1, False, S)
            for i, x in enumerate(range(s[0], s[1] + 1)):
                fp[x, y] = shade_opaque(sp['band'], bl[i], 1 if y == yb0 else 0) + (255,)
    if sp.get('necklabel'):
        # a paper collar on the neck with one printed line
        paper_c, bar_c = (col(c) for c in sp['necklabel'])
        pr_, pi_ = ramp_of(paper_c)
        free = sh.neck_end - (top + crow_cap)
        yl0 = top + crow_cap + max(S, free // 3)
        for y in range(yl0, yl0 + 3 * S):
            s = sh.spans.get(y)
            if not s:
                continue
            bl = bands(s[1] - s[0] + 1, False, S)
            for i, x in enumerate(range(s[0], s[1] + 1)):
                if y >= yl0 + S and y < yl0 + 2 * S and s[0] < x < s[1]:
                    c = bar_c
                else:
                    c = rp(pr_, max(1 if pr_ == 'Night' else 0,
                                    min(4, pi_ + (0 if bl[i] in ('hi', 'lite', 'hi2') else -1 if bl[i] in ('mid',) else -2))))
                fp[x, y] = c + (255,)
    if sp.get('ribbon'):
        # Grand Marnier's cordon rouge: a ribbon from the neck down across the body
        s0 = sh.spans[sh.neck_end]
        ax, ay = s0[1] + 0.5, sh.neck_end - 2 * S
        yb = sh.neck_end + int((sh.bot - sh.neck_end) * 0.55)
        sb = sh.spans[yb]
        bx, by = sb[0] + 1.5 * S, yb
        L = math.hypot(bx - ax, by - ay)
        for y in range(int(ay) - S, int(by) + S + 1):
            s = sh.spans.get(y)
            if not s or y < top + crow:
                continue
            for x in range(s[0], s[1] + 1):
                t = ((x + 0.5 - ax) * (bx - ax) + (y + 0.5 - ay) * (by - ay)) / (L * L)
                if not (0 <= t <= 1):
                    continue
                px_, py_ = ax + t * (bx - ax), ay + t * (by - ay)
                d = math.hypot(x + 0.5 - px_, y + 0.5 - py_)
                if d <= 1.1 * S:
                    fp[x, y] = rp(sp['ribbon'], 3 if (y + 0.5) < py_ else 2) + (255,)
    if sp.get('handles'):
        # the Kraken's two loops at the foot of the neck
        yb = sh.neck_end
        nx0, nx1 = sh.spans[yb]
        lw_, lh_ = 4 * S, 5 * S
        for side in (-1, 1):
            x_near = nx0 - 1 if side < 0 else nx1 + 1
            for yy in range(yb - lh_ + 1, yb + 1):
                for k in range(lw_):
                    x = x_near - k if side < 0 else x_near + k
                    ry, rx = yy - (yb - lh_ + 1), k
                    ring = rx >= lw_ - S or ry < S or ry >= lh_ - S
                    corner = (rx >= lw_ - S) and (ry < S or ry >= lh_ - S)
                    if ring and not corner and 0 <= x < Wd:
                        fp[x, yy] = (g['hi'][0] if ry < S else g['rrim'][0] if ry >= lh_ - S else g['rim'][0]) + (255,)

    body_lo = sh.shoulder_end + 1
    body_hi = max(sh.colbot.values()) - foot_t
    if sp.get('facets'):
        # Hibiki's twenty-four facets: the body cut into narrow flat faces, each edge a lit line
        for y in range(body_lo, body_hi + 1):
            s = sh.spans.get(y)
            if not s:
                continue
            for x in range(s[0] + S, s[1] - S + 1):
                if not mp[x, y][3]:
                    continue
                k = ((x * 2 - sh.cx2) // (2 * S)) % 3
                if k == 0:
                    fp[x, y] = g['hi'][0] + (150,)
                elif k == 2:
                    fp[x, y] = g['shade'][0] + (120,)
    if sp.get('straw'):
        # Luxardo's straw: the lower body woven over, a lit rim along its top
        y0 = body_hi - int((body_hi - body_lo) * sp['straw'])
        for y in range(y0, max(sh.colbot.values()) + 1):
            s = sh.spans.get(y)
            if not s:
                continue
            bl = bands(s[1] - s[0] + 1, False, S)
            for i, x in enumerate(range(s[0], s[1] + 1)):
                if y > sh.colbot[x]:
                    continue
                weave = ((x // S) + (y // S)) % 4
                step = 4 if y == y0 else (2 if weave == 0 else 3)
                step += {'rim': -1, 'rrim': -1, 'shade': -1, 'hi': 1}.get(bl[i], 0)
                fp[x, y] = rp('Malt', max(0, min(4, step))) + (255,)
                bp[x, y] = (0, 0, 0, 0); mp[x, y] = (0, 0, 0, 0)
    if sp.get('belt'):
        # a rope round the waist (Frangelico) or a gold band (Chambord)
        f, ramp = sp['belt']
        yb = body_lo + int((body_hi - body_lo) * f)
        for y in range(yb, yb + 2 * S):
            s = sh.spans.get(y)
            if not s:
                continue
            bl = bands(s[1] - s[0] + 1, False, S)
            for i, x in enumerate(range(s[0], s[1] + 1)):
                twist = 1 if ramp == 'Cream' and ((x // S) + (y // S)) % 2 == 0 else 0
                # opaque in front, so the cavity behind stays whole: a gap in the mask would stop
                # BottleArt.Upright's walk to the neck and cut the volume table in two
                fp[x, y] = shade_opaque(ramp, bl[i], twist + (1 if y == yb else 0) - 1) + (255,)
        if ramp == 'Cream':
            # the knot's two ends hanging down
            kx = int(sh.W / 2.0) + 2 * S
            for d in range(1, 4 * S):
                for x in (kx, kx + S):
                    y = yb + 2 * S - 1 + d
                    if sh.inside(x, y) and d < (4 * S if x == kx else 3 * S):
                        fp[x, y] = rp('Cream', 3 if d % 2 else 2) + (255,)
                        bp[x, y] = (0, 0, 0, 0); mp[x, y] = (0, 0, 0, 0)

    # 4. the closure, or the open mouth
    for y in range(top, top + crow):
        s = sh.spans.get(y)
        if s:
            for x in range(s[0], s[1] + 1):
                clear(x, y)
    if open_mouth:
        knob = int(round(stopper_knob_rows(cid, sp) * S))
        if knob:
            # the stopper's knob leaves with it: the open neck ends where the stopper sat
            new_top = max(top, min(top + knob, sh.neck_end - 2 * S))
            for y in range(top, new_top):
                for x in range(Wd):
                    clear(x, y)
            sh.top = new_top
        draw_finish(sh, cid, g, fp, bp, mp, crow_cap, sp)
    elif kind in ('codd', 'open'):
        draw_mouth(sh, g, fp, bp, mp)
    else:
        draw_closure(sh, cid, kind, cramp, crow, fp, bp, mp)
    if sp.get('neck_shape') == 'codd':
        # the marble in its chamber
        neck_px = int(round(sp['neck'] * S))
        cy = nominal_top + neck_px * 0.36
        cx = sh.W / 2.0
        rad = (sp['nw'] / 2.0 - 0.5) * S                   # smaller than the chamber: glass shows round it
        for y in range(int(cy - rad) - 1, int(cy + rad) + 2):
            for x in range(int(cx - rad) - 1, int(cx + rad) + 2):
                dx, dy = x + 0.5 - cx, y + 0.5 - cy
                if dx * dx + dy * dy <= rad * rad and sh.inside(x, y):
                    lit = dx + dy < -rad * 0.5
                    fp[x, y] = rp('ClubBlue', 4 if lit else 3 if dx + dy < rad * 0.4 else 2) + (255,)
                    bp[x, y] = (0, 0, 0, 0); mp[x, y] = (0, 0, 0, 0)

    if sp.get('neckring') and not open_mouth:
        # PET's support ring: a flange a pixel wider than the neck, just under the closure (the open hand
        # bottle draws it in draw_finish, under the tamper band the cap left)
        yr = top + crow_cap + S
        s = sh.spans.get(yr)
        if s:
            for y in range(yr, yr + S):
                for x in range(s[0] - S, s[1] + S + 1):
                    fp[x, y] = rp('ClubBlue', 4 if x * 2 < sh.cx2 else 3) + (255,)
                    if not (s[0] <= x <= s[1]):
                        bp[x, y] = (0, 0, 0, 0); mp[x, y] = (0, 0, 0, 0)   # the cavity runs on under it
    if sp.get('grip'):
        # moulded grip ribs round the lower body: lit rings every other art row
        f0, f1 = sp['grip']
        ya = body_lo + int((body_hi - body_lo) * f0)
        yb = body_lo + int((body_hi - body_lo) * f1)
        for y in range(ya, yb):
            if ((y - ya) // S) % 2:
                continue
            s = sh.spans.get(y)
            if not s:
                continue
            for x in range(s[0] + S, s[1] - S + 1):
                if mp[x, y][3]:
                    fp[x, y] = g['hi'][0] + (120,)
    if sp.get('petaloid'):
        # a PET foot's petals: the bow notched into feet, dark between them
        yb = max(sh.colbot.values())
        s = sh.spans.get(yb - S)
        if s:
            n_ = s[1] - s[0] + 1
            for k in range(1, 4):
                x = s[0] + (n_ * k) // 4
                for y in range(yb - S, yb + 1):
                    for dx in range(S):
                        if sh.inside(x + dx, y):
                            fp[x + dx, y] = rp('Night', 1) + (255,)

    # 5. the label: the cellar's paper-border-mark, or at the hand's size a composition (hand_label)
    import hand_label
    if S >= 3 and hand_label.has(cid):
        hand_label.press_round(cid, sh, fp, bp, mp, g)
    else:
        press_label(cid, sh, fp, bp, mp)
        if cid == 'cola_marlow':
            cola_wave(sh, fp)

    # 5b. THE GAME'S CONTRACT (measured 2026-09-27 against ItemArt / BottleArt / the grip):
    #  * ItemArt.FootFilled carries the mask down each column through every front pixel that is
    #    not dark (luma >= 40) and turns it to drink at alpha 96; the pixel straight under each
    #    column's lowest mask texel is made dark, so the drawn foot and walls stay glass
    #  * the bottle is picked up on the front plate's own alpha (threshold 0.1): no film thinner
    #    than 30/255, or the clear glass cannot be grabbed
    for x in range(Wd):
        ys = [y for y in range(Ht) if mp[x, y][3]]
        if not ys:
            continue
        y = ys[-1] + 1
        if y < Ht and fp[x, y][3] and 0.299 * fp[x, y][0] + 0.587 * fp[x, y][1] + 0.114 * fp[x, y][2] >= 40:
            fp[x, y] = rp('Night', 1) + (255,)
    #  * at the cellar's size the front is ALSO a standalone icon (the gauge rows, basket chips, the
    #    recipe list) with no back and no drink behind it: its film keeps the old plates' ~77 there
    floor = 30 if S > 1 else 72
    for y in range(Ht):
        for x in range(Wd):
            if 0 < fp[x, y][3] < floor:
                fp[x, y] = fp[x, y][:3] + (floor,)

    # 6. the ring: one pixel of ink round everything
    ring = [(x, y) for y in range(Ht) for x in range(Wd) if not fp[x, y][3] and any(
        0 <= x + dx < Wd and 0 <= y + dy < Ht and fp[x + dx, y + dy][3]
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))]
    for x, y in ring:
        fp[x, y] = INK + (255,)
    return back, mask, front, sh


def draw_closure(sh, cid, kind, cramp, crow, fp, bp, mp):
    """The cap over the neck's top rows. Its top is an ellipse 30% of its width tall (the
    camera's pitch): the far half narrows the silhouette, the near half bows into the side."""
    S, top = sh.S, sh.top
    s0 = sh.spans[top]
    nw = s0[1] - s0[0] + 1
    xc = sh.W / 2.0
    if kind == 'stopper':
        draw_stopper(sh, cramp, crow, nw, fp)
        return
    if kind == 'pourer':
        draw_pourer(sh, crow, nw, fp)
        return
    if kind == 'crowncap':
        # Chambord's crown: a band and three points
        for k, y in enumerate(range(top, top + crow)):
            w = nw + 4 * S
            x0 = int(xc - w / 2)
            bl = bands(w, False, S)
            for i, x in enumerate(range(x0, x0 + w)):
                if k < 2 * S:
                    u = (i // S) % ((w // S) // 2 if w // S >= 4 else 2)
                    tip = (i // S) in (0, (w // S) // 2, w // S - 1) or (i // S) in ((w // S) // 2 - 1,)
                    if not tip:
                        continue
                fp[x, y] = shade_opaque(cramp, bl[i], 1 if k < 2 * S else (-1 if y == top + crow - 1 else 0)) + (255,)
        return
    if kind == 'block':
        # Disaronno's block: a square gold cap, its flat top lit
        w = nw + 6 * S
        x0 = int(xc - w / 2)
        bl = bands(w, True, S)
        for k, y in enumerate(range(top, top + crow)):
            for i, x in enumerate(range(x0, x0 + w)):
                if k < S:
                    c = rp(cramp, 4 if i < w * 0.6 else 3)
                else:
                    c = shade_opaque(cramp, bl[i], -1 if y == top + crow - 1 else 0)
                fp[x, y] = c + (255,)
        return
    cw = nw + (2 * S if kind in ('screw', 'wax', 'cork', 'crown') else 0)
    eh = max(1.0, 0.15 * cw)                                # half the top ellipse's height
    lip_w = cw + (2 * S if kind == 'capsule' else 0)        # a capsule's crimped lip overhangs
    band = SPEC[cid].get('cork_band', 'Cream' if GLASS_OF[cid] != 'black' else 'Graphite')
    for y in range(top, top + crow):
        ry = y - top + 0.5
        dy = (ry - eh) / eh
        is_lip = kind == 'capsule' and ry < 2 * eh + S
        w = lip_w if is_lip else cw
        if kind == 'cork' and ry > 2 * S + eh:
            w = nw                                          # the neck band under the stopper
        if dy < 0:
            w = 2 * int(round(w / 2.0 * math.sqrt(max(0.0, 1 - dy * dy))))
        if w < 2:
            continue
        x0 = int(xc - w / 2); x1 = x0 + w - 1
        bl = bands(w, False, S)
        for i, x in enumerate(range(x0, x1 + 1)):
            dx = (x + 0.5 - xc) / (w / 2.0)
            face = dx * dx + dy * dy <= 1.0 and ry <= 2 * eh
            ramp = cramp
            lift = 0
            if kind == 'cork' and ry > 2 * S + eh:
                ramp = band
            if face:
                c = rp(ramp, 4 if (x + 0.5) < xc + w * 0.2 else 3)   # the lit top, a shade off on the right
            else:
                if y == top + crow - 1:
                    lift = -1                              # the lower edge, in shadow
                    if kind == 'crown' and ((x - x0) // S) % 2 == 1:
                        continue                           # a crown cap's teeth
                if kind == 'screw' and ((x - x0) // S) % 2 == 1 and bl[i] != 'hi':
                    lift -= 1                              # knurling
                c = shade_opaque(ramp, bl[i], lift)
            fp[x, y] = c + (255,)
            bp[x, y] = (0, 0, 0, 0); mp[x, y] = (0, 0, 0, 0)
    if kind == 'wax':
        # the drips: runs under the seal, fixed by the card's name so they never move
        yb = top + crow - 1
        s = (int(xc - cw / 2), int(xc - cw / 2) + cw - 1)
        seed = sum(ord(ch) for ch in cid)
        bl = bands(cw, False, S)
        for j, x in enumerate(range(s[0], s[1] + 1)):
            run = (((seed >> ((j // S) % 5)) + (j // S) * 7) % 4) * S
            for y in range(yb + 1, yb + run):
                if sh.inside(x, y):
                    fp[x, y] = shade_opaque(cramp, bl[j], -1 if y >= yb + run - S else 0) + (255,)
                    bp[x, y] = (0, 0, 0, 0); mp[x, y] = (0, 0, 0, 0)
    if SPEC[cid].get('lip'):
        # the glass lip under a screw cap: a ring one step wider than the neck
        y0 = top + crow
        for y in range(y0, y0 + S):
            s = sh.spans.get(y)
            if not s:
                continue
            for x in range(s[0] - S, s[1] + S + 1):
                fp[x, y] = (GLASS[GLASS_OF[cid]]['rrim'][0] if x * 2 < sh.cx2 else GLASS[GLASS_OF[cid]]['rim'][0]) + (255,)
                bp[x, y] = (0, 0, 0, 0); mp[x, y] = (0, 0, 0, 0)


def draw_stopper(sh, ramp, crow, nw, fp):
    """Clase Azul's stopper: a collar on the neck and a round knob on it, twice the neck wide."""
    S, top = sh.S, sh.top
    xc = sh.W / 2.0
    collar = 2 * S
    yb = top + crow - 1
    for y in range(yb - collar + 1, yb + 1):
        w = nw + 2 * S
        x0 = int(xc - w / 2)
        bl = bands(w, False, S)
        for i, x in enumerate(range(x0, x0 + w)):
            fp[x, y] = shade_opaque(ramp, bl[i], -1 if y == yb else 0) + (255,)
    D = nw + 6 * S
    kh = crow - collar
    cy = top + kh / 2.0
    for y in range(top, top + kh):
        dy = (y + 0.5 - cy) / (kh / 2.0)
        w = 2 * int(round(D / 2.0 * math.sqrt(max(0.0, 1 - dy * dy))))
        if w < 2:
            continue
        x0 = int(xc - w / 2)
        bl = bands(w, False, S)
        for i, x in enumerate(range(x0, x0 + w)):
            lift = 1 if dy < -0.45 else (-1 if dy > 0.55 else 0)
            fp[x, y] = shade_opaque(ramp, bl[i], lift) + (255,)


def draw_pourer(sh, crow, nw, fp):
    """A syrup bottle's pourer: a steel collar and a thin spout leaning left."""
    S, top = sh.S, sh.top
    xc = sh.W / 2.0
    collar = 2 * S
    yb = top + crow - 1
    for y in range(yb - collar + 1, yb + 1):
        w = nw + 2 * S
        x0 = int(xc - w / 2)
        bl = bands(w, False, S)
        for i, x in enumerate(range(x0, x0 + w)):
            fp[x, y] = shade_opaque('Graphite', bl[i], 1 if y == yb - collar + 1 else -1 if y == yb else 0) + (255,)
    sw = 2 * S
    for k, y in enumerate(range(yb - collar, top - 1, -1)):
        x0 = int(xc - sw / 2) - (k // (2 * S)) * S
        for j in range(sw):
            fp[x0 + j, y] = rp('Graphite', 4 if j < S else 2) + (255,)


def draw_mouth(sh, g, fp, bp, mp):
    """The hand bottle is poured from, so it has no cap: the neck ends in a rim seen from
    above — a light glass ring round a dark throat, the near half bowing down."""
    S, top = sh.S, sh.top
    s0 = sh.spans[top]
    nw = s0[1] - s0[0] + 1
    eh = max(1.0, 0.15 * nw)
    xc = sh.W / 2.0
    for y in range(top, top + int(math.ceil(2 * eh)) + 1):
        ry = y - top + 0.5
        dy = (ry - eh) / eh
        s = sh.spans.get(y)
        if not s:
            continue
        for x in range(s[0], s[1] + 1):
            dx = (x + 0.5 - xc) / (nw / 2.0)
            d_out = dx * dx + dy * dy
            inner_rx = max(0.4, nw / 2.0 - S) / (nw / 2.0)
            inner_ry = max(0.4, eh - 1.0) / eh
            d_in = (dx / inner_rx) ** 2 + (dy / inner_ry) ** 2
            if dy < 0 and d_out > 1.0:
                fp[x, y] = (0, 0, 0, 0)                    # outside the far rim: nothing
                bp[x, y] = (0, 0, 0, 0); mp[x, y] = (0, 0, 0, 0)
                continue
            if d_in <= 1.0:
                # the throat: the far inside wall a step lighter than the dark going down
                fp[x, y] = rp('Night', 2 if dy < -0.2 else 1) + (255,)
            elif d_out <= 1.0:
                # the rim: its far edge and its left catch the light, its near edge is glass
                lit = dy < 0 or x < xc - nw * 0.2
                fp[x, y] = (g['hi'][0] if lit else g['rim'][0]) + (255,)
            else:
                continue
            bp[x, y] = (0, 0, 0, 0); mp[x, y] = (0, 0, 0, 0)


# ── THE OPEN HAND BOTTLE'S NECK TOP (round seven b) ──────────────────────────────
# The author, 2026-09-28: "büyük şişelerin ağız ve boyun kısmında problem var ... şişenin kendisi renginde
# veya tasarımında şeklinde devam etmiyor ağız kısmı". draw_mouth ended every open neck the same way: a
# ring in the glass's HIGHLIGHT (cream-white on clear glass, gold on smoke) round a black hole, on a
# plain cut pipe — so the top of every hand bottle read as one white tube end, and everything its cap
# had carried (a red foil, Maker's wax, a gold collar, a PET tamper band) was gone.
# A bottle opened behind a bar keeps its finish. Its lip is the neck's own glass: one ramp — the glass's
# face ramp (the ramp its empty inside is drawn in) — measured once off the neck, a step lighter where the
# lip faces up, a step darker under its edge, the neck's rim colours at its two walls and the neck's own
# highlight column running up through it; the bore goes from a far wall a step darker into the glass's
# dark (an opaque bottle's into Night). A foil capsule stays as a sleeve torn at the lip; wax stays, cut
# round the lip, with its drips; a screw finish shows threads, the cap's tamper ring and the glass bead
# under it; PET its threads, the tamper band and the support ring; a stopper takes its knob with it, so
# the neck ends where the stopper sat, on the collar it sat on.
# Three review passes (2026-09-28): opaque edge colours read as a grey knob, a cap still on; sampling the
# neck column by column carried its vertical bands through the ring and snapped greys to Cream and teals
# to Lime; one ramp, measured once, does neither.
def glass_edge(g):
    """The glass seen edge-on: (dark, light, highlight) — its rim, its reflected rim, its highlight."""
    return g['rim'][0], g['rrim'][0], g['hi'][0]


LIPPED = ('cork', 'stopper', 'crowncap', 'crown', 'wax', 'capsule', 'open', 'block', 'codd')
OPAQUE_GLASS = ('milk', 'ceramic')


def _lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


def _seen_rgb(fp, bp, x, y):
    """What the room shows at (x, y): the front laid over the back (unsnapped)."""
    f, b = fp[x, y], bp[x, y]
    if not f[3]:
        return tuple(b[:3]) if b[3] else None
    if f[3] >= 255 or not b[3]:
        return tuple(f[:3])
    a = f[3] / 255.0
    return tuple(f[i] * a + b[i] * (1 - a) for i in range(3))


def _step(c, k):
    r, i = ramp_of(c)
    return rp(r, max(1 if r == 'Night' else 0, min(4, i + k)))


def stopper_knob_rows(cid, sp=None):
    """Cellar rows of a stopper's knob (it leaves with the stopper); its collar (2) stays as the seat."""
    kind = CLOSURE[cid][0]
    if kind not in ('stopper', 'crowncap'):
        return 0
    return max(0, cap_rows(cid, sp) - 2)


def draw_finish(sh, cid, g, fp, bp, mp, crow_cap, sp=None):
    S, top = sh.S, sh.top
    kind, cramp = CLOSURE[cid]
    sp = sp or SPEC[cid]
    s0 = sh.spans[top]
    nw = s0[1] - s0[0] + 1
    xc = sh.W / 2.0
    hi = g['hi'][0]
    lipped = kind in LIPPED
    proud = 2 if lipped else 0                      # a bead two pixels proud of the neck; a screw neck none
    lipw = nw + 2 * proud
    eh = max(1.0, 0.15 * lipw)                      # half the top ellipse's height (17-degree camera)
    wall = max(S, int(round(0.18 * nw))) if lipped else S
    rin = max(1.5, nw / 2.0 - wall)
    rin_y = eh * rin / (lipw / 2.0)
    y_top = top + int(math.ceil(2 * eh))            # under the top ellipse
    F = S if lipped else 0                          # the bead's side
    y_end = y_top + F
    x0 = int(round(xc - lipw / 2.0))

    # the rows that are still neck (a sleeve or a ring never climbs onto the shoulder)
    last_neck = top
    while sh.spans.get(last_neck + 1) and sh.width(last_neck + 1) <= nw + 1:
        last_neck += 1

    # the glass's face ramp, and the neck's face in it, measured once a row under the lip
    face = ramp_of(g['back'][2])[0]
    lo = 1 if face == 'Night' else 0
    ys = min(last_neck, y_end + 1)
    hi_cols = set()
    acc, n_ = [0.0, 0.0, 0.0], 0
    for x in range(s0[0] + S, s0[1] - S + 1):
        f = fp[x, ys]
        if f[3] >= 200 and tuple(f[:3]) == hi:
            hi_cols.add(x)
            continue
        c = _seen_rgb(fp, bp, x, ys)
        if c:
            for k in range(3):
                acc[k] += c[k]
            n_ += 1
    avg = tuple(a / max(1, n_) for a in acc)
    base_i = min(range(lo, 5), key=lambda i: sum((avg[k] - rp(face, i)[k]) ** 2 for k in range(3)))
    base = rp(face, base_i)
    lit = rp(face, min(4, base_i + 1))
    under = rp(face, max(lo, base_i - 1))
    wall_l, wall_r = g['rim'][0], g['rrim'][0]
    if face in ('Cream',) and GLASS_OF.get(cid) in OPAQUE_GLASS:
        far, deep = rp('Cream', 2), rp('Night', 1)   # an opaque bottle is dark inside
    else:
        far = rp(face, max(lo, base_i - 1))
        deep = rp(face, lo)
        if _lum(deep) >= _lum(far) - 6:
            far = base if _lum(base) > _lum(deep) + 6 else lit
        if _lum(deep) >= _lum(far) - 6:
            deep = rp('Night', 1)
    if not hi_cols:
        nb = bands(nw, False, S)
        hi_cols = {s0[0] + i for i, b in enumerate(nb) if b == 'hi'}

    # 1. the lip: a closed ring round the bore, and a bead's side
    for y in range(top, y_end):
        ry = y - top + 0.5
        dy = (ry - eh) / eh
        for x in range(x0, x0 + lipw):
            dx = (x + 0.5 - xc) / (lipw / 2.0)
            on_top = dx * dx + dy * dy <= 1.0
            if dy < 0 and not on_top:
                fp[x, y] = (0, 0, 0, 0); bp[x, y] = (0, 0, 0, 0); mp[x, y] = (0, 0, 0, 0)
                continue                             # beyond the far rim: air
            near_curve = eh + eh * math.sqrt(max(0.0, 1 - dx * dx))
            if ry > near_curve + F:
                continue                             # under the lip: the neck's own glass
            edge_l, edge_r = x < x0 + 2, x > x0 + lipw - 3
            if ((x + 0.5 - xc) / rin) ** 2 + ((ry - eh) / rin_y) ** 2 <= 1.0:
                t = (ry - (eh - rin_y)) / (2 * rin_y)
                c = far if t < 0.34 else deep
                fp[x, y] = c + (255,); bp[x, y] = (0, 0, 0, 0); mp[x, y] = (0, 0, 0, 0)
                continue
            if on_top:
                if x in hi_cols:
                    c = hi
                elif edge_l:
                    c = lit if dy < 0 else wall_l
                elif edge_r:
                    c = wall_r
                else:
                    c = lit if dy < 0.35 else base
                fp[x, y] = c + (255,); bp[x, y] = (0, 0, 0, 0); mp[x, y] = (0, 0, 0, 0)
                continue
            # the bead's side: the face, its walls, the highlight through it; a step darker underneath
            c = hi if x in hi_cols else (wall_l if edge_l else wall_r if edge_r else base)
            if ry > near_curve + F - 1:
                c = under if not (edge_l or edge_r) else c
            fp[x, y] = c + (255,)                    # the cavity runs on behind it (mask kept)

    ns = lambda y: sh.spans.get(y) if y <= last_neck else None
    seed = sum(ord(ch) for ch in cid) * 7919

    def hsh(k):
        return ((seed + k * 2654435761) >> 7) & 0xffff

    def sleeve(y0, y1, ramp, torn=False, cut=False):
        """A band round the neck in a closure's material, a pixel proud each side; returns the row under it."""
        last = y0
        for y in range(y0, y1):
            s = ns(y)
            if not s:
                continue
            a, b_ = s[0] - 1, s[1] + 1
            bl = bands(b_ - a + 1, False, S)
            for i, x in enumerate(range(a, b_ + 1)):
                blk = (x - a) // S
                if torn and y < y0 + (S if hsh(blk) % 3 == 0 else (1 if hsh(blk) % 3 == 1 else 0)):
                    continue                         # the foil's torn top, whole blocks at a time
                if cut and y == y0 and hsh(blk + 17) % 4 == 0 and 0 < i < b_ - a:
                    continue                         # the wax cut round the lip, a little uneven
                lift = 1 if y == y0 else (-1 if y == y1 - 1 else 0)
                fp[x, y] = shade_opaque(ramp, bl[i], lift) + (255,)
            last = y + 1
        return last

    base_row = top + crow_cap                      # where the capped copy's closure ended
    ring_end = y_end
    if kind == 'capsule':
        end = sleeve(y_end, min(last_neck + 1, max(y_end + 2 * S, base_row + S)), cramp, torn=True)
        ring_end = end
        if end - y_end > 6 * S:
            ym = y_end + (end - y_end) * 2 // 3
            s = ns(ym)
            if s:
                for x in range(s[0] - 1, s[1] + 2):
                    fp[x, ym] = shade_opaque(cramp, 'shade') + (255,)     # the capsule's crimp
    elif kind == 'wax':
        end = sleeve(y_end, min(last_neck + 1, max(y_end + 2 * S, base_row)), cramp, cut=True)
        s = ns(end - 1)
        if s:
            a, b_ = s[0] - 1, s[1] + 1
            bl = bands(b_ - a + 1, False, S)
            x = a + 1 + hsh(3) % S
            k = 0
            while x < b_ - 1:
                w = min((S - 1) + hsh(10 + k) % 3, b_ - x)
                n = S + (hsh(20 + k) % 4) * S
                for d in range(n):
                    y = end + d
                    tip = d == n - 1
                    for xx in range(x + (1 if tip else 0), x + w - (1 if tip and w > 2 else 0)):
                        c = shade_opaque(cramp, bl[min(len(bl) - 1, xx - a)], -1 if tip else 0)
                        fp[xx, y] = c + (255,)
                x += w + S + (hsh(30 + k) % 3) * S
                k += 1
    elif kind == 'screw':
        pet = bool(sp.get('neckring'))
        ring_rows = S if pet else S + 1
        band0 = base_row if pet else base_row - ring_rows
        # the band (and, on PET, the support ring under it) must both fit on the neck, however short
        band0 = max(y_top + S + 1, min(band0, last_neck - (2 * S if pet else ring_rows) + 1))
        # the threads: lines of the neck's own highlight on its clear glass, between the lip and the band
        stepr = S if band0 - y_top < 3 * S else 2 * S
        yy = y_top + 1
        while yy < band0 - 1:
            s = ns(yy)
            if s:
                for x in range(s[0] + 1, s[1]):
                    fp[x, yy] = hi + (200,)
                    if yy + 1 < band0 - 1:
                        fp[x, yy + 1] = under + (max(fp[x, yy + 1][3], 170),)
            yy += stepr
        ring_end = sleeve(band0, band0 + ring_rows, cramp)
        if pet:
            # the support ring, under the tamper band: a clear flange S proud each side, the cavity behind
            yr = min(band0 + ring_rows, last_neck)
            s = sh.spans.get(yr)
            if s:
                for y in range(yr, yr + S):
                    for x in range(s[0] - S, s[1] + S + 1):
                        fp[x, y] = rp('ClubBlue', 4 if x * 2 < sh.cx2 else 3) + (255,)
                ring_end = yr + S
    elif kind == 'crown':
        # a crown finish: the lip, then a second, smaller bead under it
        yb = y_end + S
        s = ns(yb)
        if s:
            for y in range(yb, yb + S):
                for x in range(s[0] - 1, s[1] + 2):
                    fp[x, y] = (lit if y == yb else (under if y == yb + S - 1 else base)) + (255,)
    elif kind in ('stopper', 'crowncap'):
        # the collar the stopper sat on, right under the lip (the knob went with the stopper)
        sleeve(y_end, y_end + (2 * S if kind == 'crowncap' else S), cramp)
    # the glass bead a screw or capsule neck carries under its cap stays when the cap comes off
    if sp.get('lip') and kind in ('screw', 'capsule'):
        yl = min(max(ring_end, y_end + S), last_neck - S + 1)
        s = ns(yl)
        if s and yl >= ring_end:
            for y in range(yl, yl + S):
                for x in range(s[0] - 1, s[1] + 2):
                    fp[x, y] = (hi if x in hi_cols else (lit if y == yl else (under if y == yl + S - 1 else base))) + (255,)
    return y_end


def ramp_of(c):
    for name, hexes in palette.RAMPS.items():
        for i, h in enumerate(hexes):
            if palette.hex_rgb(h) == tuple(c):
                return name, i
    return ramp_of(palette.nearest(c))


def press_label(cid, sh, fp, bp=None, mp=None):
    """The label, in the shape its brand wears. Most are paper with a darker border (rect,
    slant, oval, round, diamond, shield, arch, banner, torn, tall, high, wrap, two, seal,
    oversize, tag); some are no paper at all: printed on the glass (print), raised out of it
    (embossed), a clear window in frosted glass (window), paint on ceramic (painted). Big paper
    labels wrap the body and bow toward the middle as the camera sees a ring round a cylinder;
    medallions and tags stay flat."""
    S = sh.S
    sp = sh.sp
    g = GLASS[GLASS_OF[cid]]
    shape = sp.get('label', 'rect')
    mark, paper, border, ink, accent, bar = LABELS[cid]
    art = ICONS[mark] if S == 1 else scale3x(ICONS[mark]) if S == 3 else ICONS[mark]
    ih, iw = len(art), len(art[0])
    paper, border, ink, accent, bar = (col(c) for c in (paper, border, ink, accent, bar))
    cream = rp('Cream', 4)
    xc = sh.W / 2.0
    square = sp.get('square', False)
    y_lo = sh.shoulder_end + 2 * S
    y_hi = max(sh.colbot.values()) - 4 * S
    if sp.get('straw'):
        y_hi = y_hi - int((y_hi - y_lo) * sp['straw'])
    if y_hi - y_lo < ih + 4 * S:
        y_lo = sh.neck_end + 2 * S
    nb = y_hi - y_lo + 1
    bw_at = lambda y: sh.width(y)
    bwmax = max(sh.width(y) for y in range(y_lo, y_hi + 1))
    bord = 1 if S == 1 else 2

    def put_mark(mx0, my0, cols=None, dy_of=None):
        for r, line in enumerate(art):
            for c, ch in enumerate(line):
                if ch == '.':
                    continue
                x, y = mx0 + c, my0 + r
                if dy_of:
                    y += dy_of(x)
                fp[x, y] = ((cols or {}).get(ch) or (ink if ch == 'k' else accent if ch == 'a' else cream)) + (255,)

    def bar_line(y, x0, x1, dy_of, colr=None):
        for x in range(x0, x1 + 1):
            for t in range(bord):
                fp[x, y + t + dy_of(x)] = (colr or bar) + (255,)

    def text_line(y, x0_, x1_, dy_of, word, colr=None):
        """At the hand's size a line of lettering is the word itself (the project's 3x5 face,
        bent along the label a whole letter at a time); where it will not fit, and always in
        the cellar, it stays the bar that stands for it."""
        if S >= 3 and word and fontpx.width(word) <= x1_ - x0_ + 1:
            glyphs = fontpx.render(word, colr or bar).load()
            gw = fontpx.width(word)
            gx0 = (sh.W - gw) // 2
            stp = fontpx.GW + fontpx.GAP
            for gy in range(fontpx.GH):
                for gx in range(gw):
                    if glyphs[gx, gy][3]:
                        o = dy_of(gx0 + (gx // stp) * stp + fontpx.GW // 2)
                        fp[gx0 + gx, y - 1 + gy + o] = (colr or bar) + (255,)
            return
        bar_line(y, x0_, x1_, dy_of, colr)

    def bow_of(lx0, bw):
        if square or shape in ('slant',):
            return (lambda x: -int(round((x + 0.5 - xc) * 0.2))) if shape == 'slant' else (lambda x: 0)
        half = bw / 2.0
        bow = 0.2 * bw
        ue = (lx0 + 0.5 - xc) / half
        base = int(round(bow * math.sqrt(max(0.0, 1 - ue * ue))))
        return lambda x: int(round(bow * math.sqrt(max(0.0, 1 - ((x + 0.5 - xc) / half) ** 2)))) - base

    def paint(mask, dy_of, fill=None, edge=None):
        """Paper over the mask, its border one pixel of the border colour."""
        for (x, y) in mask:
            e = any((x + dx, y + dy) not in mask for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            fp[x, y + dy_of(x)] = ((edge or border) if e else (fill or paper)) + (255,)

    def rect_mask(x0, y0, w, h, cut=True):
        m = set()
        for y in range(y0, y0 + h):
            for x in range(x0, x0 + w):
                if cut and (x - x0 < S or x0 + w - 1 - x < S) and (y - y0 < S or y0 + h - 1 - y < S):
                    continue
                m.add((x, y))
        return m

    def place(h, where=0.55):
        where = sp.get('label_at', where)                  # a shape that is widest low (a cone) says so
        centre = y_lo + int(round((nb - 1) * where))
        return max(y_lo, min(y_hi - h + 1, centre - h // 2))

    def body_w(y0, h):
        return min(bw_at(y) for y in range(y0, y0 + h))

    flat = lambda x: 0

    # ── no paper ──
    if shape in ('print', 'embossed', 'painted'):
        h = ih + (6 if shape != 'painted' else 8) * S
        y0 = place(h)
        mx0 = (sh.W - iw) // 2
        my0 = y0 + (2 if shape == 'painted' else 1) * S
        if shape == 'embossed':
            # raised glass: the mark in the glass's own light, its shadow under it
            for r, line in enumerate(art):
                for c, ch in enumerate(line):
                    if ch == '.':
                        continue
                    x, y = mx0 + c, my0 + r
                    fp[x + S, y + S] = g['shade'][0] + (190,)
            for r, line in enumerate(art):
                for c, ch in enumerate(line):
                    if ch != '.':
                        fp[mx0 + c, my0 + r] = g['hi'][0] + (235,)
            return
        if shape == 'painted':
            # a painted band round the ceramic: two lines and a zigzag between, the mark in it
            ya, yb = y0, y0 + h - 1
            s_ = sh.spans
            for y, kind_ in ((ya, 'line'), (ya + S, 'zig'), (yb - S, 'zig'), (yb, 'line')):
                s = s_.get(y)
                if not s:
                    continue
                for x in range(s[0] + S, s[1] - S + 1):
                    if kind_ == 'line' or ((x // S) % 2 == (1 if y == ya + S else 0)):
                        fp[x, y] = bar + (255,)
            put_mark(mx0, my0 + S)
            return
        put_mark(mx0, my0)
        bw = body_w(y0, h)
        lx0 = (sh.W - int(bw * 0.7)) // 2
        bar_line(my0 + ih + S, lx0 + S, sh.W - 1 - lx0 - S, flat, ink)
        return

    if shape == 'window':
        # Grey Goose: a clear window in the frosted glass, the birds flying across it
        w = max(iw + 4 * S, int(round(bwmax * 0.62))) & ~1
        h = ih + 6 * S
        y0 = place(h)
        x0 = (sh.W - w) // 2
        for (x, y) in rect_mask(x0, y0, w, h):
            edge = x in (x0, x0 + w - 1) or y in (y0, y0 + h - 1)
            if edge:
                fp[x, y] = g['hi'][0] + (200,)
            elif mp is not None and mp[x, y][3]:
                fp[x, y] = g['mid'][0] + (12,)          # clear: the drink shows plainly here
        put_mark((sh.W - iw) // 2, y0 + 3 * S)
        return

    if shape in ('round', 'diamond'):
        R = (max(iw, ih) + (6 if shape == 'round' else 8) * S) / 2.0
        cy = y_lo + (nb - 1) * 0.55
        cy = max(y_lo + R, min(y_hi - R, cy))
        m = set()
        for y in range(int(cy - R) - 1, int(cy + R) + 2):
            for x in range(int(xc - R) - 1, int(xc + R) + 2):
                dx, dy = x + 0.5 - xc, y + 0.5 - cy
                d = math.hypot(dx, dy) if shape == 'round' else abs(dx) + abs(dy) * 1.15
                if d <= R and sh.inside(x, y):
                    m.add((x, y))
        paint(m, flat)
        put_mark((sh.W - iw) // 2, int(round(cy - ih / 2.0)))
        return

    if shape == 'tag':
        # a card hung from the neck on a string, resting on the shoulder
        w, h = iw + 4 * S, ih + 4 * S
        x0 = int(xc) + S
        y0 = sh.shoulder_end + 1 * S
        m = rect_mask(x0, y0, w, h)
        yn = sh.neck_end - 2 * S
        s = sh.spans.get(yn)
        if s:
            # the string from the neck's right side to the tag's corner
            ax, ay = s[1], yn
            bx, by = x0 + S, y0
            n_ = max(abs(bx - ax), abs(by - ay), 1)
            for k in range(n_ + 1):
                x = int(round(ax + (bx - ax) * k / float(n_)))
                y = int(round(ay + (by - ay) * k / float(n_)))
                fp[x, y] = rp('Cream', 3) + (255,)
        paint(m, flat)
        put_mark(x0 + 2 * S, y0 + 2 * S)
        return

    # ── paper of every other shape ──
    word_rows = 0
    if shape in ('tall',):
        h = min(int(nb * 0.86), ih + 16 * S)
        w = max(iw + 4 * S, int(round(bwmax * 0.58))) & ~1
        y0 = place(h, 0.5)
    elif shape == 'high':
        h = ih + 6 * S
        w = max(iw + 4 * S, int(round(bwmax * 0.7))) & ~1
        y0 = y_lo
    elif shape == 'wrap':
        h = ih + 6 * S
        y0 = place(h, 0.5)
        w = body_w(y0, h)
    elif shape == 'banner':
        h = ih + 4 * S
        y0 = place(h, 0.5)
        w = body_w(y0, h)
    elif shape == 'oversize':
        y0 = max(sh.neck_end - S, y_lo - 6 * S)
        h = (max(sh.colbot.values()) - 4 * S) - y0 + 1
        w = bwmax
    elif shape == 'oval':
        h = ih + 6 * S
        w = max(iw + 8 * S, int(round(bwmax * 0.84))) & ~1
        y0 = place(h)
    elif shape == 'shield':
        h = ih + 9 * S
        w = max(iw + 6 * S, int(round(bwmax * 0.66))) & ~1
        y0 = place(h)
    elif shape == 'arch':
        h = ih + 11 * S
        w = max(iw + 4 * S, int(round(bwmax * 0.74))) & ~1
        y0 = place(h)
    else:                                                  # rect, slant, torn, two, seal
        h = ih + (10 if shape in ('rect', 'slant', 'torn', 'two') else 7) * S
        w = max(iw + 4 * S, int(round(bwmax * 0.76))) & ~1
        y0 = place(h, 0.62 if shape in ('two', 'seal') else 0.55)
    h = max(ih + 4 * S, min(h, y_hi - y0 + 1))
    bw = body_w(y0, min(h, max(1, y_hi - y0 + 1)))
    if shape not in ('oversize',):
        w = min(w, bw if shape in ('wrap', 'banner') else bw - 2 * S)
    w -= w % 2
    x0 = (sh.W - w) // 2
    dy_of = bow_of(x0, bw) if shape not in ('oval', 'shield', 'arch') else flat

    if shape == 'oval':
        m = set()
        for y in range(y0, y0 + h):
            for x in range(x0, x0 + w):
                dx = (x + 0.5 - xc) / (w / 2.0); dy = (y + 0.5 - (y0 + h / 2.0)) / (h / 2.0)
                if dx * dx + dy * dy <= 1:
                    m.add((x, y))
    elif shape == 'shield':
        m = set()
        for y in range(y0, y0 + h):
            r = (y - y0) / float(h)
            if r < 0.55:
                hw_ = w / 2.0
            else:
                hw_ = w / 2.0 * (1 - (r - 0.55) / 0.45) + S
            for x in range(x0, x0 + w):
                if abs(x + 0.5 - xc) <= hw_ and not ((x - x0 < S or x0 + w - 1 - x < S) and y == y0):
                    m.add((x, y))
    elif shape == 'arch':
        m = set()
        ha = w / 2.0 * 0.7
        for y in range(y0, y0 + h):
            for x in range(x0, x0 + w):
                if y - y0 < ha:
                    k = (ha - (y - y0 + 0.5)) / ha
                    if abs(x + 0.5 - xc) > w / 2.0 * math.sqrt(max(0.0, 1 - k * k)):
                        continue
                m.add((x, y))
    elif shape == 'banner':
        m = set()
        d = 2 * S
        ym = y0 + h / 2.0
        for y in range(y0, y0 + h):
            for x in range(x0, x0 + w):
                e = min(x - x0, x0 + w - 1 - x)
                if e < d and abs(y + 0.5 - ym) < (d - e):
                    continue                               # the swallowtail notch
                m.add((x, y))
    elif shape == 'torn':
        m = set()
        for y in range(y0, y0 + h):
            for x in range(x0, x0 + w):
                jag = (1 if ((x // S) * 5 + 3) % 7 < 3 else 0) * S
                if y < y0 + jag:
                    continue
                if (x - x0 < S or x0 + w - 1 - x < S) and y0 + h - 1 - y < S:
                    continue
                m.add((x, y))
    else:
        m = rect_mask(x0, y0, w, h, cut=shape not in ('wrap', 'oversize'))
    paint(m, dy_of)

    # the contents: mark, then lines of lettering as bars (words at the hand's size)
    top_air = {'arch': 5, 'shield': 2, 'oversize': 3, 'high': 2, 'wrap': 2, 'banner': 2, 'oval': 3}.get(shape, 2) * S
    mx0 = (sh.W - iw) // 2
    my0 = y0 + top_air
    if shape in ('rect', 'slant', 'torn', 'two', 'tall', 'oversize'):
        # a line of lettering over the mark
        text_line(y0 + 2 * S, x0 + 2 * S, x0 + w - 1 - 2 * S, dy_of, brief.BRAND_WORD.get(cid), ink)
        my0 = y0 + 4 * S
    if shape == 'seal':
        my0 = y0 + 2 * S - ih - S                         # the seal carries the mark; the paper only lettering
    else:
        put_mark(mx0, my0, dy_of=(dy_of if shape == 'slant' else (lambda x: dy_of(sh.W // 2))))
    yy = my0 + ih + S
    lines = {'tall': 4, 'oversize': 3, 'rect': 2, 'slant': 2, 'torn': 2, 'two': 2, 'arch': 2,
             'shield': 1, 'oval': 1, 'seal': 2, 'high': 1, 'wrap': 1, 'banner': 0}.get(shape, 1)
    for k in range(lines):
        inset = (2 + k) * S
        if yy + bord > y0 + h - 2 * S:
            break
        if shape == 'shield':
            inset = 3 * S
        if k == 0:
            text_line(yy, x0 + inset, x0 + w - 1 - inset, dy_of, STYLE_WORD.get(cid.split('_')[0]))
        else:
            bar_line(yy, x0 + inset, x0 + w - 1 - inset, dy_of)
        yy += 2 * S
    if shape == 'two':
        # a small second label up on the shoulder
        w2 = int(round(bwmax * 0.46)) & ~1
        y2 = sh.shoulder_end - S
        x2 = (sh.W - w2) // 2
        m2 = {(x, y) for (x, y) in rect_mask(x2, y2, w2, 4 * S) if sh.inside(x, y)}
        paint(m2, flat)
        bar_line(y2 + int(1.5 * S), x2 + 2 * S, x2 + w2 - 1 - 2 * S, flat)
    if shape == 'seal':
        # a wax-red seal pressed over the label's top edge, the mark in it
        R = (max(iw, ih) + 4 * S) / 2.0
        cy = y0 - R * 0.35
        for y in range(int(cy - R) - 1, int(cy + R) + 2):
            for x in range(int(xc - R) - 1, int(xc + R) + 2):
                d = math.hypot(x + 0.5 - xc, y + 0.5 - cy)
                if d <= R and sh.inside(x, y):
                    fp[x, y] = rp('ViceRed', 1 if d > R - 1 else (3 if (x + 0.5 - xc) + (y + 0.5 - cy) < -R * 0.4 else 2)) + (255,)
        put_mark((sh.W - iw) // 2, int(round(cy - ih / 2.0)), cols={'k': cream, 'a': cream})


def cola_wave(sh, fp):
    """Loca's white ribbon: a wave across its red wrap, rising to the right."""
    S = sh.S
    red = rp('ViceRed', 2)
    rows = [y for y in sh.spans if any(fp[x, y][:3] == red and fp[x, y][3] == 255
                                       for x in range(sh.spans[y][0], sh.spans[y][1] + 1))]
    if not rows:
        return
    y0, y1 = min(rows), max(rows)
    mid = (y0 + y1) / 2.0 + 2 * S
    for y in range(y0, y1 + 1):
        s = sh.spans[y]
        for x in range(s[0], s[1] + 1):
            u = (x - s[0]) / float(max(1, s[1] - s[0]))
            wy = mid - 1.8 * S * math.sin(u * math.pi * 1.4)
            if wy <= y < wy + 2 * S and fp[x, y][:3] == red:
                fp[x, y] = rp('Cream', 4 if y < wy + S else 3) + (255,)
    if S >= 3:
        # the hand bottle has room for the name, in cream over the ribbon
        g = fontpx.render('LOCA', rp('Cream', 4), 2).load()
        gw = fontpx.width('LOCA', 2)
        gx0 = (sh.W - gw) // 2
        gy0 = int(y0 + 2 * S)
        for gy in range(fontpx.GH * 2):
            for gx in range(gw):
                if g[gx, gy][3] and fp[gx0 + gx, gy0 + gy][:3] == red:
                    fp[gx0 + gx, gy0 + gy] = rp('Cream', 4) + (255,)


# ── the preview composite (what the room draws) ─────────────────────────────
SURFACE_SQUASH = 0.24                                     # GlassArt.SurfaceSquash


def game_shoulder(widths, rows):
    """BottleArt.Upright's full line, in image rows (y down): the first row from the top at least
    88% of the median width 55-90% down; below 60% of the cavity the walk up to the neck
    (a quarter wider than the neck, stepping over belt gaps)."""
    top, bot = rows[0], rows[-1]
    hgt = bot - top + 1
    lower = sorted(widths.get(y, 0) for y in range(top, bot + 1)
                   if 0.55 <= (y - top) / float(max(1, hgt)) <= 0.90 and widths.get(y, 0) > 0)
    body = lower[len(lower) // 2] if lower else 1
    sh = next((y for y in range(top, bot + 1) if widths.get(y, 0) >= 0.88 * body), top)
    if (bot - sh + 1) / float(hgt) < 0.6:
        tops = sorted(widths[y] for y in range(top, bot + 1) if (y - top) < hgt * 0.3 and widths.get(y, 0) > 0)
        neck = tops[len(tops) // 2] if tops else None     # the median: the rim's sliver is not the neck
        if neck:
            allow = max(2, int(round(hgt * 0.08)))       # a short waist (a rope, a pinch) is stepped over
            last, narrow = sh, 0
            for y in range(sh - 1, top - 1, -1):
                if widths.get(y, 0) >= neck * 1.2:
                    last, narrow = y, 0
                else:
                    narrow += 1
                    if narrow > allow:
                        break
            sh = last
    return sh


def composite(back, mask, front, colour, fill):
    """What the room draws: back, the drink, front. The drink is DiegeticStage's: a quad of
    ChromeArt.LiquidBody from the cavity's foot up to the level (full = the shoulder, as a
    volume), a face oval as wide as the cavity at the level and 24% as tall, lighter, and a
    faint foot oval over the base — all cut by the cavity mask."""
    Wd, Ht = back.size
    out = back.copy()
    mp = mask.load()
    rows = [y for y in range(Ht) if any(mp[x, y][3] for x in range(Wd))]
    liq = Image.new('RGBA', (Wd, Ht), (0, 0, 0, 0)); lp = liq.load()
    if rows and fill > 0:
        xs_all = [x for x in range(Wd) if any(mp[x, y][3] for y in rows)]
        cx0, cx1 = min(xs_all), max(xs_all)
        cw_ = cx1 - cx0 + 1
        widths = {y: sum(1 for x in range(Wd) if mp[x, y][3]) for y in rows}
        wmax = max(widths.values())
        shoulder = game_shoulder(widths, rows)
        vol_full = sum(widths[y] for y in rows if y >= shoulder)
        want = vol_full * fill
        acc, line = 0, rows[-1]
        for y in reversed(rows):
            if acc >= want:
                break
            acc += widths[y]; line = y
        tone = vivid(colour)
        foot_y = rows[-1] + 1                               # the cavity's lower edge
        for y in range(line, rows[-1] + 1):
            for x in range(cx0, cx1 + 1):
                if not mp[x, y][3]:
                    continue
                u = (x - cx0 + 0.5) / cw_
                wall = 1 - (abs(u - 0.5) * 2) ** 2.2 * 0.32
                streak = 1.06 if abs(u - 0.32) < 0.08 else 1.0
                depth = 0.90 + 0.10 * (foot_y - y) / float(max(1, foot_y - line))
                k = min(1.0, wall * streak * depth)
                lp[x, y] = tuple(min(255, int(tone[i] * k)) for i in range(3)) + (255,)

        def oval(cx, cy, w, h, c, a):
            for y in range(int(cy - h / 2) - 1, int(cy + h / 2) + 2):
                for x in range(int(cx - w / 2) - 1, int(cx + w / 2) + 2):
                    if not (0 <= x < Wd and 0 <= y < Ht) or not mp[x, y][3]:
                        continue
                    dx = (x + 0.5 - cx) / (w / 2.0); dy = (y + 0.5 - cy) / (h / 2.0)
                    if dx * dx + dy * dy <= 1:
                        base = lp[x, y] if lp[x, y][3] else (0, 0, 0, 0)
                        if a >= 255 or not base[3]:
                            lp[x, y] = c + (255,) if a >= 255 else c + (a,)
                        else:
                            t = a / 255.0
                            lp[x, y] = tuple(int(base[i] * (1 - t) + c[i] * t) for i in range(3)) + (255,)
        # the foot: the base seen through the drink, faint
        fw = widths.get(rows[-1] - 2 * max(1, Ht // 64 // 1), wmax)
        rise = fw * SURFACE_SQUASH * 0.5
        if rise * 2 < (foot_y - line):
            oval((cx0 + cx1 + 1) / 2.0, foot_y - rise, fw, rise * 2,
                 tuple(int(tone[i] * 0.88) for i in range(3)), 140)
        # the face: the drink's top, an oval centred on the level
        faw = widths[line]
        oval((cx0 + cx1 + 1) / 2.0, line, faw, faw * SURFACE_SQUASH,
             tuple(min(255, int(tone[i] * 1.18 + 18)) for i in range(3)), 255)
    out.alpha_composite(liq)
    out.alpha_composite(front)
    return out


def load(cid, part, big=False):
    name = 'v4_%s_%s%s.png' % (cid, part, '' if big else '_c')
    return Image.open(os.path.join(ITEMS, name)).convert('RGBA')


def shipped_composite(cid, fill, big=False):
    b, m, f = load(cid, 'back', big), load(cid, 'mask', big), load(cid, 'front', big)
    return composite(b, m, f, liquid_of(cid), fill)


# THE OLD ART STAYS (the author, 2026-09-27: "Rubis eski stile dönsün"): drawn for the record,
# never staged, never shipped.
KEEP_OLD = set()                           # 2026-09-27 later: Rubis back in the new style after all


# cards that were sealed (a can) and are glass now (a PET litre): they have no plates in Items yet
NEW_GLASS = {'cola_marlow', 'energy_volt'}


def glass_ids():
    return sorted(c for c in SPEC if c not in KEEP_OLD and (c in NEW_GLASS
                  or os.path.exists(os.path.join(ITEMS, 'v4_%s_front_c.png' % c))))


# ── THE HAND BOTTLE AT ITS OWN SIZE (round seven) ─────────────────────────────
# The author, 2026-09-27: "şişelerin boyutu neyse öyle kalsın, sıkıştırma şişelerin görselini".
# Drawn as the cellar numbers x3, a short bottle came to the bench a third smaller than the plate
# it replaced (the ginger beer 104 rows where the old one stood 172). The hand keeps the height
# the shipped plate had (measured off HEAD, rows of the 192 canvas) and the cellar keeps its own
# proportions: the shape is scaled whole, never squeezed, and narrowed only where it would run
# off the 96 columns.
HAND_H = {
    'amaro_notte': 174, 'bourbon_ashfall': 175, 'bourbon_hollow_oak': 184, 'bourbon_old_harrow': 184,
    'bourbon_redline': 171, 'gin_boothby': 173, 'gin_juniper_crown': 172, 'gin_thornwood': 176,
    'gin_veilcrest': 173, 'ginger_kicker': 172, 'grenadine_rubis': 175, 'liqueur_delia': 168,
    'liqueur_kafa': 170, 'rum_cane_coral': 175, 'rum_reina_del_mar': 175, 'rum_tidewater': 172,
    'rum_windward': 179, 'soda_klara': 172, 'syrup_house': 175, 'tequila_alta_luna': 173,
    'tequila_cielo_roto': 178, 'tequila_sol_viejo': 164, 'tequila_sonora': 180, 'tonic_quinbury': 175,
    'vermouth_velvet': 178, 'vodka_astra': 171, 'vodka_leonid': 178, 'vodka_okhta': 175, 'vodka_vor': 170,
    'cola_marlow': 178, 'energy_volt': 176,
}
HAND_MAX_W = 90                            # the widest a hand bottle may stand, its ring included
KEEP_CLOSED = {'syrup_house'}              # its pourer IS where it pours from (the author, round seven)


def hand_spec(cid, k):
    sp = dict(SPEC[cid])
    for key in ('ht', 'bw', 'nw', 'neck', 'sh', 'cap'):
        if key in sp:
            sp[key] = sp[key] * k
    return sp


def draw_hand(cid):
    """The 96x192 hand bottle: the cellar's shape scaled to HAND_H, narrowed to HAND_MAX_W."""
    sp0 = SPEC[cid]
    k = min(HAND_H.get(cid, 172), 186) / (sp0['ht'] * 3.0)
    k = min(k, (HAND_MAX_W - 2) / (sp0['bw'] * 3.0))     # never drawn past the canvas to be measured
    for _ in range(6):
        out = draw(cid, S=3, open_mouth=cid not in KEEP_CLOSED, sp=hand_spec(cid, k))
        bb = out[2].getbbox()
        wide = bb[2] - bb[0]
        if wide <= HAND_MAX_W:
            return out
        k *= (HAND_MAX_W - 1) / float(wide)
    return out


def off_palette(*ims):
    n = 0
    for im in ims:
        px = im.load()
        for y in range(im.height):
            for x in range(im.width):
                if px[x, y][3] and palette.nearest(px[x, y][:3]) != px[x, y][:3]:
                    n += 1
    return n


def run(big_ids=()):
    os.makedirs(OUT, exist_ok=True)
    made = {}
    import shelf_box
    for cid in glass_ids():
        box = shelf_box.has(cid)
        back, mask, front, sh = shelf_box.draw(cid) if box else draw(cid)
        d = os.path.join(OUT, cid)
        os.makedirs(d, exist_ok=True)
        back.save(os.path.join(d, 'v4_%s_back_c.png' % cid))
        mask.save(os.path.join(d, 'v4_%s_mask_c.png' % cid))
        front.save(os.path.join(d, 'v4_%s_front_c.png' % cid))
        made[cid] = {'off_palette': off_palette(back, front), 'height': sh.bot - sh.top + 1}
        if cid in big_ids:
            b3, m3, f3, _ = shelf_box.draw_hand(cid) if box else draw_hand(cid)
            b3.save(os.path.join(d, 'v4_%s_back.png' % cid))
            m3.save(os.path.join(d, 'v4_%s_mask.png' % cid))
            f3.save(os.path.join(d, 'v4_%s_front.png' % cid))
            made[cid]['big_off_palette'] = off_palette(b3, f3)
    with open(os.path.join(OUT, 'made.json'), 'w') as f:
        json.dump(made, f, indent=1)
    return made


if __name__ == '__main__':
    m = run(big_ids=tuple(sys.argv[1:]))
    print(len(m), 'drawn;', sum(v['off_palette'] + v.get('big_off_palette', 0) for v in m.values()), 'off-palette pixels')
