# -*- coding: utf-8 -*-
"""The 2026-08-25 Miami batch: a new till, and room art to stand it in.

The author's brief, in four parts, of which THREE land here:

  "Kasa gorselinin yenisini uret perpektif ve aciyi dikkate al tezgahi referans al
   2.5d 30 derecelik bir aci, tema vice, miami bar kasasi."
  "Pixellab ile oda atmosferine ve sekline uyumlu eklenebilecek gorseller uret.
   Tezgah odanin kendisi arkaplan duvarlar vs. icin bunlari oyuna eklemeden once
   onizleme ile goster bana."

(The fourth part is the OPEN BAR sign, which is UI chrome and is struck by hand in
open_sign_gen.py - the generator cannot write letters, see the memory
pixellab-mcp-constraints. The shelf packing is code, in DiegeticStage.)

NOTHING SHIPS FROM HERE. Everything lands in Tools/AssetPipeline/staging/vice_room/
and `report` builds preview.html beside it. The author picks; only then does a take
get copied into Assets/. That is the proof gate this project has paid for once already
(memory bottle-art-v3-respec) and the author asked for it again by name in this brief.

THE THREE THINGS THIS BATCH HAS TO GET RIGHT, each one a rule already paid for:

  * THE ANGLE IS WRITTEN, NOT HOPED FOR. "2.5D seen from slightly above" is the clause
    that bought the v3 bottles their roundness; without it the generator returns a flat
    cut-out. Here it is sharper still - the author named 30 degrees AND named the
    counter as the reference - so every till prompt says which face recedes and which
    way, and the counter plate itself rides along as a labelled reference so the model
    can SEE the eye height it has to match.
  * NO LIGHT IS BAKED IN. The room is lit by URP 2D lights; a highlight painted into a
    sprite is a second sun. Every prompt says flat matte local colour, no specular, no
    reflection, no cast shadow, no glow, and form shaded only by stepping along a named
    ramp. (art-direction-rules, 2026-08-18.)
  * NATIVE RESOLUTION, NO RESAMPLE. Every plate is asked for at exactly the size the
    stage wants. An area-downscale averages four painted pixels into one, and that is
    precisely why the shipped room reads blurry beside a hand-pixelled bottle.

Commands:  balance | queue [key...] | fetch | post [key...] | report | inventory | status
State:     Tools/vice_room_state.json      Raw: Tools/vice_room_raw/
Staged:    Tools/AssetPipeline/staging/vice_room/
Log:       Tools/AssetPipeline/generation_log.jsonl
"""
import base64, io, json, os, re, sys, time

import numpy as np
from PIL import Image, ImageDraw

import pixellab
import scene_nb_post as nb

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
STATE = os.path.join(HERE, 'vice_room_state.json')
RAW = os.path.join(HERE, 'vice_room_raw')
STAGE = os.path.join(HERE, 'AssetPipeline', 'staging', 'vice_room')
LOG = os.path.join(HERE, 'AssetPipeline', 'generation_log.jsonl')
BACKGROUNDS = os.path.join(ROOT, 'Assets', 'Art', 'Backgrounds')
PROPS = os.path.join(ROOT, 'Assets', 'Art', 'Props')
FIXTURES = os.path.join(ROOT, 'Assets', 'Resources', 'Fixtures')

# -- the stage's own numbers (DiegeticStage.cs), so nothing here is a guess ------
REF_W, REF_H = 640, 360           # DiegeticStage.Reference
COUNTER_REST_Y = 120              # CounterRestY
COUNTER_INSET = 2                 # CounterSurfaceInset
COUNTER_W, COUNTER_H = 638, 241   # the installed counter.png, to the pixel
REGISTER_X = 604                  # RegisterX
REGISTER_BASE_Y = COUNTER_REST_Y - 12   # RegisterBaseY
REGISTER_W = 57                   # the till's fixed footprint, in stage units

# The counter's cellar, in the counter art's OWN rows and columns. A new counter plate
# that does not carry these is not a drop-in: DiegeticStage measures its shelves off
# them, and a stale table stands a bottle on a post. `report` draws them onto every
# counter take so that is answered by LOOKING and not by hoping.
CELLAR_BAY_CENTRE = [120, 319, 517]
CELLAR_BAY_WIDTH = 175
CELLAR_FOOT_ROWS = [143, 233]     # the plank surfaces a bottle stands on
CELLAR_CEIL_ROWS = [65, 150]      # the opening's top, then the upper board's underside

# -- the production law, in the prompt itself -----------------------------------
CRISP = ('true pixel art at native resolution, every pixel placed deliberately, hard '
         'crisp pixel edges, clean 1-pixel outlines in each material\'s own darkest '
         'tone and never pure black, flat shading with ordered 2x2 dithering for every '
         'gradient, strictly limited palette, no anti-aliasing, no blur, no soft '
         'gradients, no painterly brushwork, no photo texture, no text, no letters, '
         'no numbers, no signage, no logos, no people')

# Written into every prompt because the room is lit in Unity. See the module header.
UNLIT = ('flat matte local colour, even flat lighting, no specular highlights, no '
         'reflections, no cast shadows, no rim light, no glow, no bloom, no lens flare, '
         'form shaded only by stepping along the named colour ramps')

# THE ANGLE, and it is the same sentence in all three till takes on purpose. The author
# gave ONE instruction - 2.5D, thirty degrees, the counter is the reference - so the
# takes may differ in what the machine IS and must not differ in where it is seen from.
# The direction is not a detail either: the till stands at stage x 604, the right-hand
# end of a room drawn in one-point perspective with its vanishing point dead centre, so
# its top face recedes up and to the LEFT. A take that recedes right is a machine
# standing in a different room.
# THE ANGLE HAS TWO HALVES AND THE FIRST CUT ONLY NAMED ONE (2026-08-25, the author:
# "Kasa gorselleri cok donuk biraz daha karsiya bakmali").
#
# "2.5D from above at about 30 degrees" says how high the camera is. It says NOTHING about
# how far the object is TURNED, and the generator filled that silence with the default a
# pixel-art model reaches for: full isometric, front corner nearest, BOTH side walls in
# view. Every one of the twelve takes came back that way - correct about the elevation,
# wrong about the rotation, and the two together read as a machine sitting at 45 degrees
# to a counter that runs straight across the screen.
#
# So the two are now written separately, and the wrong answer is named and forbidden:
# elevation stays where it was, rotation comes almost all the way back to square. What
# has to survive is the TOP FACE - that is the whole reason the brief said 2.5D - so it
# is asked for as a shallow band above the front panel rather than as a receding plane.
TILL_ANGLE = (
    'seen from the FRONT and almost square to the camera, turned only very slightly: the '
    'front panel faces the viewer directly and fills nearly the whole width of the '
    'machine, only a NARROW sliver of its left side wall is visible and the right side is '
    'completely hidden. The camera is ABOVE it at about 30 degrees, so the flat TOP '
    'surface reads as a shallow band across the top of the machine and the keypad plate '
    'as a slanted plane tilted towards the viewer. NOT an isometric view, NOT a 45 degree '
    'three-quarter or corner-on view, and NOT a flat cut-out with no top surface - every '
    'horizontal edge runs nearly level, parallel to the counter it stands on')

TILL_SHELL = (
    'pixel art, ONE isolated 1980s Miami bar cash register on a plain transparent '
    'background, no counter under it, ' + TILL_ANGLE + ', filling the frame, about 1.1 '
    'times as WIDE as it is TALL: a rectangular body, a raised display head at the back, '
    'a grid of round keys on the sloped keypad, a drawer with a pull across the front, '
    'blank key caps and a blank dark display window, ')

ROOM_SHELL = (
    'pixel art, EMPTY interior of a small Miami cocktail bar, seen straight on in '
    'one-point perspective with the vanishing point dead centre, no furniture, no '
    'bottles, no counter, no people, a wide empty floor filling the lower half, a tall '
    'steel-framed industrial window on the LEFT wall running floor to ceiling in '
    'perspective with flat chroma green #00FF00 glass panes, a flat ceiling with a slim '
    'pale cornice where wall meets ceiling and a matching skirting at the floor, the '
    'back wall square to the camera and the right wall running away in perspective, ')

# Every plant in the room is drawn the same way; only the leaf and the vessel differ.
# Kept as one constant so a fifth plant cannot quietly arrive in a different style.
PLANT_SHELL = (' on a plain transparent background, seen straight on at eye level, '
               'nothing behind it, no floor, no shadow, ')

# EIGHT compartments across the front is the bar's own furniture, and the CELLAR behind
# the roller is the machine: three bays on two planks. Both are described, because both
# are measured in code.
# TAKE TWO OF THE COUNTER (2026-08-25). The first pair failed in two ways worth writing
# down, because both are about the FRAME rather than about taste:
#   * the model CENTRED the object in the 244-row frame and drew a thin slab, so the crop
#     to 241 rows ate the slab and left a band of nothing at the foot. The fix is to say
#     the counter touches the top row and the bottom row - "full width" was said and
#     obeyed, "full height" was not said at all.
#   * the bays came back TRANSPARENT on the sunset take. Asking for a near-black interior
#     on a transparent-background call invites the model to read "dark and empty" as
#     "background", and the alpha cut took the whole inside of the unit with it. So the
#     interior is now described as a solid opaque back wall you can SEE, twice.
# The band proportions are the shipped counter's own rows, as fractions, because the
# cellar table in DiegeticStage is measured against them to the pixel.
COUNTER_SHELL = (
    'pixel art, ONE isolated bar counter seen straight on at eye level, FILLING THE WHOLE '
    'IMAGE from the very top row to the very bottom row and from the left edge to the '
    'right edge with no empty margin anywhere, on a plain transparent background, NO wall '
    'and NO room behind it, nothing standing on it, no bottles and no glasses. Layout '
    'from the top down: the TOP QUARTER of the image is one solid thick slab with a '
    'dead-straight level front edge; under it a deep open shelf unit of exactly THREE '
    'tall bays divided by four square vertical posts; a horizontal shelf plank crosses '
    'all three bays dead level at 58 percent of the way down the image and an identical '
    'plank at 95 percent down forms the base, both planks drawn with visible depth so '
    'their top surfaces read as a plank seen from slightly above; the inside of each bay '
    'is a SOLID OPAQUE dark back wall that is clearly drawn and never transparent and '
    'never empty background, ')

# -- the labelled references ----------------------------------------------------
# create_image_pro takes up to FOUR, and the LABEL is the lever: each entry's "usage"
# tells the model what to take from that picture. Palette as a swatch beats any amount
# of adjective; the counter goes in as the ANGLE the till has to match, which is the
# author's own instruction ("tezgahi referans al") in the one form a model cannot misread.
PALETTE_SWATCH = os.path.join(HERE, 'palette_miami.png')
STYLE_REF = os.path.join(ROOT, 'Assets', 'Resources', 'Items', 'v3_bourbon_redline_flat.png')
COUNTER_REF = os.path.join(BACKGROUNDS, 'counter.png')
ROOM_REF = os.path.join(BACKGROUNDS, 'club_room.png')
TILL_REF = os.path.join(PROPS, 'register2.png')

STYLE_USAGE = ('pixel art rendering style: hard 1px edges, large flat colour runs, '
               'ordered dithering, no blur, no baked lighting')
PALETTE_USAGE = 'colour palette: use ONLY these colours, Miami sunset and neon tones'


def b64file(path):
    with io.open(path, 'rb') as f:
        return base64.b64encode(f.read()).decode('ascii')


# WHEN THE PLATE IS THE WRONG PLATE (2026-08-25). palette_miami.png is the Miami subset
# of the 55 and it deliberately leaves Lime out - the swatch was cut for sunset and neon,
# and the drab ramps were dropped because they made the room read dead. That is right for
# a wall and wrong for a plant: the palm was asked for in three named greens and all four
# candidates came back magenta and cyan, because the plate outvotes the prose every time.
#
# The answer is NOT to widen the shared plate, which would quietly re-tint every scene
# call after it. It is to hand THIS asset a different colour reference - and the best one
# is not a synthetic swatch at all, it is the plant already standing in the room. Whatever
# green fx_monstera is, the new palm has to be the same green, or the two sit side by side
# in the same room disagreeing about what a leaf looks like.
_LEAF_REF = (os.path.join(FIXTURES, 'fx_monstera.png'),
             'leaf colour ONLY: use exactly this plant\'s greens for the foliage and this '
             'pot\'s earth tones for the vessel. It is the plant already standing in this '
             'room and the two must agree. Do NOT copy its shape')

PALETTE_OVERRIDE = {
    'fx_plant_fiddle': _LEAF_REF,
    'fx_plant_snake': _LEAF_REF,
    'fx_plant_pothos': _LEAF_REF,
    'fx_plant_agave': _LEAF_REF,
    'fx_palm_pot': (os.path.join(FIXTURES, 'fx_monstera.png'),
                    'leaf colour ONLY: use exactly this plant\'s greens for the fronds and '
                    'this pot\'s earth tones for the pot. It is the plant already standing '
                    'in this room and the two must agree. Do NOT copy its shape'),
}


def refs(family, key=None):
    over = PALETTE_OVERRIDE.get(key)
    if over:
        out = [{'base64': b64file(over[0]), 'usage': over[1]}]
    else:
        out = [{'base64': b64file(PALETTE_SWATCH), 'usage': PALETTE_USAGE}]
    if family == 'till':
        out.append({'base64': b64file(COUNTER_REF), 'usage': (
            'CAMERA ANGLE AND EYE HEIGHT ONLY: this is the counter the machine stands on. '
            'Match the height the viewer looks down from and the way this counter\'s top '
            'slab recedes. Do NOT copy its colours, its shape or its shelves')})
        out.append({'base64': b64file(TILL_REF), 'usage': (
            'subject and footprint ONLY: what the object is, and roughly how wide it is '
            'against how tall. Do NOT copy its flat straight-on angle and do NOT copy '
            'its colours')})
    elif family == 'room':
        out.append({'base64': b64file(ROOM_REF), 'usage': (
            'composition ONLY: the camera angle, the one-point perspective, where the '
            'window sits and where the floor meets the walls. Do NOT copy its colours, '
            'its soft blurry shading or its muted grey-mauve tone')})
        out.append({'base64': b64file(COUNTER_REF), 'usage': (
            'colour temperature and material feel of the bar this room belongs to, so '
            'the two read as one place. Do NOT copy its shape')})
    elif family == 'counter':
        out.append({'base64': b64file(COUNTER_REF), 'usage': (
            'GEOMETRY, EXACTLY: keep the same slab thickness, the same three bays, the '
            'same four vertical posts at the same widths, and the same two horizontal '
            'shelf planks at the same heights. Do NOT copy its colours or its shading')})
    else:                                     # dressing
        out.append({'base64': b64file(ROOM_REF), 'usage': (
            'the room this piece has to hang in: its wall colour, its light and its '
            'scale. Do NOT copy its shapes and do NOT draw the room')})
    out.append({'base64': b64file(STYLE_REF), 'usage': STYLE_USAGE})
    return json.dumps(out)


# -- the batch ------------------------------------------------------------------
# The till is drawn at 112x100 - TWICE the 57x50 footprint it is rendered into. That is
# not a doubling on screen: DiegeticStage already says the till is "drawn at a hi-bit
# density into a fixed footprint", so a 2x plate puts finer pixels into the same slot.
# It also gives the model room to draw a keypad that reads at all.
TILL_SIZE = (112, 100)
COUNTER_FRAME = (640, 244)        # the plate is 638x241; slack for the crop to find

ASSETS = {
    # -- the till, three machines, one angle ------------------------------------
    'till_brass': dict(tool='create_image_pro', seed=52101, family='till', post='prop',
        label='Pirinc deco kasa', where='tezgahin sag ucu',
        note='Art-deco pirinc govde, krem tuslar, magenta emaye panolar. Odanin kendi '
             'altin rampasinda duruyor - tezgahin mermer ve pirinc hattiyla ayni malzeme '
             'ailesi.',
        args=dict(width=TILL_SIZE[0], height=TILL_SIZE[1], no_background=True,
                  description=(TILL_SHELL +
            'an ornate art-deco brass machine: polished brass body #C9822B shaded '
            '#8F5A1E and #4A2E14 with #E8A33D edges, a stepped deco crown on the display '
            'head, deep magenta enamel side panels #8F2464 with a thin brass reveal, '
            'round cream key caps #F2E8D5 with #9C8F80 shadow in a neat grid, a dark plum '
            'display window #241830 in a brass frame, a brass drawer pull across the '
            'front, ' + UNLIT + ', ' + CRISP))),
    'till_neon': dict(tool='create_image_pro', seed=52102, family='till', post='prop',
        label='Neon elektronik kasa', where='tezgahin sag ucu',
        note='1985 kasasi: krem-beyaz plastik govde, krom hat, magenta tuslar, turkuaz '
             'ekran. Odadaki neon tabelalarla ayni dili konusuyor - en "vice" olani.',
        args=dict(width=TILL_SIZE[0], height=TILL_SIZE[1], no_background=True,
                  description=(TILL_SHELL +
            'a mid-eighties electronic point-of-sale machine: warm cream plastic body '
            '#F2E8D5 shaded #C9BCA8 and #9C8F80, a chrome trim line along the top of the '
            'front panel, hot magenta #E84DA6 and #C23283 square key caps with three '
            'larger keys down the right, a wide teal #123B45 display window with a cyan '
            '#3BC8BE bezel, a thin cyan stripe under the drawer pull, ' + UNLIT + ', '
            + CRISP))),
    'till_marble': dict(tool='create_image_pro', seed=52103, family='till', post='prop',
        label='Mermer & erik kasa', where='tezgahin sag ucu',
        note='Tezgahin kendi malzemesinden: erik-siyahi govde, krem mermer ust yuzey, '
             'amber tuslar. Uzerinde durdugu tablanin devami gibi okunuyor.',
        args=dict(width=TILL_SIZE[0], height=TILL_SIZE[1], no_background=True,
                  description=(TILL_SHELL +
            'a heavy machine built from the counter\'s own materials: near-black plum '
            'body #1A1023 shaded #241830 and #362447, its flat top surface a slab of '
            'cream marble #C9BCA8 with sparse thin #9C8F80 veins and an #F2E8D5 nose, '
            'amber #E8A33D round key caps with #8F5A1E shadow, a magenta #E84DA6 hairline '
            'along the front panel\'s bottom edge, a brushed brass drawer pull, '
            + UNLIT + ', ' + CRISP))),

    # -- the room ---------------------------------------------------------------
    'room_dusk': dict(tool='create_image_pro', seed=52201, family='room', post='room',
        label='Alacakaranlik odasi', where='arkaplan plakasi (club_room)',
        note='Ayni kutu, gun batiminin son yarim saati: krem siva magentaya donuyor, sag '
             'duvar erik, parke sicak. Gunduz sahnesi icin.',
        args=dict(width=REF_W, height=REF_H, no_background=False,
                  description=(ROOM_SHELL +
            'warm cream plaster walls #F2E8D5 shaded #C9BCA8 washed with hot magenta '
            '#E84DA6 and #FF7DC6 on the window side and amber #E8A33D deeper in, the '
            'right wall deep plum #362447, a chest-high dado band of deep petrol green '
            '#123B45 running around the room with a thin brass trim line #C9822B along '
            'its top, warm oak parquet #8F5A1E with #4A2E14 seams laid in clean straight '
            'perspective lines, ' + UNLIT + ', ' + CRISP))),
    'room_night': dict(tool='create_image_pro', seed=52202, family='room', post='room',
        label='Neon gece odasi', where='arkaplan plakasi (club_room)',
        note='Karanlik kutu: mor-laciverte duvarlar, camdan turkuaz, sokaktan magenta. '
             'Oyunun cogu gece geciyor - bu, o gecenin kendi plakasi.',
        args=dict(width=REF_W, height=REF_H, no_background=False,
                  description=(ROOM_SHELL +
            'deep purple-blue walls #241830 with #362447 panel seams and a magenta '
            '#8F2464 band at chest height, the right wall #1A1023, a cyan #26918F edge '
            'along the window frame and the cornice, near-black parquet #0D0813 with '
            '#3A2410 seams and long ordered-dithered magenta #8F2464 and cyan #1B5F66 '
            'runs down the boards, ' + UNLIT + ', ' + CRISP))),

    # -- the counter ------------------------------------------------------------
    'counter_vice': dict(tool='create_image_pro', seed=52301, family='counter',
        post='counter', label='Vice tezgah', where='counter.png',
        note='Oyundaki tezgahin ayni geometride yeniden cekimi: uc goz, dort dikme, iki '
             'raf. Mavi dograma yerine petrol yesili, tablada mermer, magenta burun. '
             'GEOMETRI TUTMAZSA oyuna giremez - rapordaki cizgiler tam bunun icin.',
        args=dict(width=COUNTER_FRAME[0], height=COUNTER_FRAME[1], no_background=True,
                  description=(COUNTER_SHELL +
            'cream marble slab top #C9BCA8 with sparse thin #9C8F80 veins and a hot '
            'magenta #E84DA6 hairline along its front nose, deep petrol green posts and '
            'planks #123B45 with #1B5F66 faces and #26918F top edges, the bay interiors '
            'near-black #0D0813, a thin brass reveal line #C9822B where the slab meets '
            'the posts, ' + UNLIT + ', ' + CRISP))),
    'counter_sunset': dict(tool='create_image_pro', seed=52302, family='counter',
        post='counter', label='Gun batimi tezgahi', where='counter.png',
        note='Ayni geometri, sicak malzeme: ceviz dikmeler, altin damarli koyu tabla, '
             'amber raf kenarlari. Alacakaranlik odasinin yanina konmak icin.',
        args=dict(width=COUNTER_FRAME[0], height=COUNTER_FRAME[1], no_background=True,
                  description=(COUNTER_SHELL +
            'near-black plum marble slab top #1A1023 with thin gold #C9822B veins and an '
            '#E8A33D nose, warm walnut posts and planks #4A2E14 with #6B4416 faces and '
            '#C9822B top edges, the bay interiors near-black #0D0813, ' + UNLIT + ', '
            + CRISP))),

    'counter_chrome': dict(tool='create_image_pro', seed=52303, family='counter',
        post='counter', label='Krom & lacivert tezgah', where='counter.png',
        note='Ucuncu cekim, secenek olsun diye: bu ailede cagri basina TEK aday donuyor '
             '(640x244 pro tavaninin ustunde), yani her take bir tur demek. Laciverte '
             'dograma, krom kenarlar, cyan raf hatlari - odanin gece plakasina en yakini.',
        args=dict(width=COUNTER_FRAME[0], height=COUNTER_FRAME[1], no_background=True,
                  description=(COUNTER_SHELL +
            'pale cream marble slab top #F2E8D5 shaded #C9BCA8 with a cyan #3BC8BE '
            'hairline along its front nose, deep club blue posts and planks #1F2E66 with '
            '#2E4699 faces and pale chrome #9C8F80 top edges, the bay interiors '
            'near-black #0D0813, ' + UNLIT + ', ' + CRISP))),

    # -- things the room could take ---------------------------------------------
    'fx_neon_flamingo': dict(tool='create_image_pro', seed=52401, family='dressing',
        post='prop', label='Neon flamingo', where='wall_right yuvasi (neon martini yerine)',
        note='Duvar tabelasi. Odada zaten neon martini var; bu onun ustune bir kademe - '
             'ayni yuva, ayni olcu, tek bacakli flamingo.',
        args=dict(width=64, height=64, no_background=True, description=(
            'pixel art, ONE isolated neon sign of a standing flamingo on a plain '
            'transparent background, drawn as bent glass tubing: a continuous hot magenta '
            '#E84DA6 and #FF7DC6 tube outline of a flamingo standing on one leg, a small '
            'cyan #3BC8BE tube arc under its feet, a dark #241830 mounting bar and '
            'brackets behind the tubes, the tube drawn as unlit coloured glass and not as '
            'light, nothing behind it, no wall, ' + UNLIT + ', ' + CRISP))),
    'fx_palm_pot': dict(tool='create_image_pro', seed=52402, family='dressing',
        post='prop', label='Saksi palmiye', where='plant_left / plant_right yuvasi',
        note='Zeminde duran uzun bir areca palmiye. Fern ve monstera ile ayni yuva '
             'ailesi ama boyu tezgahin ustune ciktigi icin koseyi dolduruyor. '
             'DIKKAT: prompt yaprak icin YESIL istedi (#16331B/#2A5926/#479938), gelen '
             'dordu de magenta-turkuaz. Palet plakasi metni yendi - Lime rampasi o '
             'plakada yok, cunku plaka "Miami tonlari" icin cikarilmisti. Neon palmiye '
             'odaya yakisiyor olabilir ama ISTENEN bu degildi: yesil isteniyorsa palet '
             'referansina Lime eklenip yeniden cekilmeli, uzerine boyanmamali.',
        args=dict(width=56, height=96, no_background=True, description=(
            'pixel art, ONE isolated tall potted areca palm standing on a plain '
            'transparent background, seen straight on at eye level, about 1.7 times as '
            'TALL as it is WIDE, filling the frame from top to bottom: slim arching '
            'fronds in deep green #16331B and #2A5926 with #479938 leaf faces, thin '
            'stems, standing in a terracotta pot #7E3130 with a #9C4740 rim band and a '
            '#38161A base, nothing behind it, no floor, no shadow, ' + UNLIT + ', '
            + CRISP))),
    # -- more plants (2026-08-25, the author: "bitkiler guzel alternatiflerini de uret
    #    farkli vazo ve bitki cesitlerini uret ayni tarzda") ---------------------
    # Same shell, same size band, same colour reference - the room's own monstera, which
    # is what made the palm come back green after four magenta ones. What varies is the
    # PLANT and the VESSEL, because that is what was asked to vary: a leaf shape and a
    # pot are the two things a second plant in the same room can differ by without
    # becoming a different art style.
    'fx_plant_fiddle': dict(tool='create_image_pro', seed=52405, family='dressing',
        post='prop', label='Kemanyapragi & krem vazo', where='plant_left / plant_right',
        note='Genis, kasik bicimli yapraklar; uzun krem seramik vazo. Palmiyenin ince '
             'yapraklarinin tam karsiti - iki bitki yan yana durursa siluetleri '
             'birbirine karismiyor.',
        args=dict(width=56, height=96, no_background=True, description=(
            'pixel art, ONE isolated fiddle-leaf fig plant' + PLANT_SHELL +
            'about 1.7 times as TALL as it is WIDE, filling the frame from top to bottom: '
            'a few large broad spade-shaped leaves with a clear central vein on upright '
            'woody stems, standing in a TALL slim cream ceramic vase with a narrow neck '
            'and a soft shoulder, ' + UNLIT + ', ' + CRISP))),
    'fx_plant_snake': dict(tool='create_image_pro', seed=52406, family='dressing',
        post='prop', label='Paşakılıcı & beton saksı', where='plant_left / plant_right',
        note='Dimdik, sivri yapraklar; kare beton saksi. Odadaki en grafik bitki - '
             'yapraklar dik cizgiler, o yuzden kucuk olcekte bile okunuyor.',
        args=dict(width=56, height=96, no_background=True, description=(
            'pixel art, ONE isolated snake plant sansevieria' + PLANT_SHELL +
            'about 1.8 times as TALL as it is WIDE, filling the frame from top to bottom: '
            'a tight fan of stiff upright sword-shaped leaves with pale banded edges, '
            'standing in a SQUARE pale concrete pot with straight sides and a plain rim, '
            + UNLIT + ', ' + CRISP))),
    'fx_plant_pothos': dict(tool='create_image_pro', seed=52407, family='dressing',
        post='prop', label='Sarmasik & pirinc ayakli saksi',
        where='plant_left / plant_right',
        note='Sarkan sarmasik, pirinc uc ayakli saksida. Tek "dokulen" siluet - digerleri '
             'yukari buyuyor, bu asagi, o yuzden raf ya da tezgah ucunda da durabilir.',
        args=dict(width=56, height=96, no_background=True, description=(
            'pixel art, ONE isolated trailing pothos plant' + PLANT_SHELL +
            'about 1.7 times as TALL as it is WIDE, filling the frame from top to bottom: '
            'heart-shaped leaves on long vines SPILLING DOWN over both sides of the pot '
            'and hanging below it, in a small round pot raised on THREE thin brass legs, '
            + UNLIT + ', ' + CRISP))),
    'fx_plant_agave': dict(tool='create_image_pro', seed=52408, family='dressing',
        post='prop', label='Agav & genis terracotta kase',
        where='plant_left / plant_right (alcak)',
        note='Alcak ve genis - digerlerinin aksine yayiliyor. Tezgahin ucunde ya da '
             'pencere onunde, uzun bir bitkinin kapatacagi yerde ise yariyor.',
        args=dict(width=72, height=72, no_background=True, description=(
            'pixel art, ONE isolated agave plant' + PLANT_SHELL +
            'as WIDE as it is tall, filling the frame: a low rosette of thick pointed '
            'blue-green succulent leaves splaying outwards from the centre, each leaf with '
            'a paler edge, planted in a WIDE shallow terracotta bowl, ' + UNLIT + ', '
            + CRISP))),

    'fx_deco_mirror': dict(tool='create_image_pro', seed=52403, family='dressing',
        post='prop', label='Deco ayna', where='wall_center yuvasi (triptik yerine)',
        note='Kemerli art-deco ayna, pirinc cerceve. Bar arkasi duvarin klasik parcasi; '
             'triptigin alternatifi olarak ayni orta yuvaya asilir.',
        args=dict(width=64, height=88, no_background=True, description=(
            'pixel art, ONE isolated arched art-deco wall mirror on a plain transparent '
            'background, seen straight on, about 1.4 times as TALL as it is WIDE: a '
            'stepped brass frame #C9822B shaded #8F5A1E with #E8A33D highlights on its '
            'steps, a round-arched top, three thin brass bars fanning across the arch, '
            'the mirror face a flat plum #362447 with one flat #4A3160 band across it and '
            'no picture in it, nothing behind it, no wall, ' + UNLIT + ', ' + CRISP))),
    'fx_jukebox': dict(tool='create_image_pro', seed=52404, family='dressing',
        post='prop', label='Jukebox', where='yeni yuva gerekir (kose, zemin)',
        note='Kose icin ayakta duran bir jukebox - odanin su an hic yuvasi olmayan '
             'parcasi. Secilirse fixtures.json a yeni bir slot acmak gerekiyor.',
        args=dict(width=64, height=96, no_background=True, description=(
            'pixel art, ONE isolated 1950s style floor-standing jukebox on a plain '
            'transparent background, seen straight on at eye level, about 1.5 times as '
            'TALL as it is WIDE, filling the frame from top to bottom: a domed arched top '
            'in polished brass #C9822B over a warm walnut #4A2E14 cabinet, a wide dark '
            'plum #241830 window in the middle with a blank record rack behind it, two '
            'vertical magenta #E84DA6 tube strips down the sides of the arch and a cyan '
            '#26918F strip across the base, a small blank grille of horizontal bars under '
            'the window, nothing behind it, ' + UNLIT + ', ' + CRISP))),
}

UUID = re.compile(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}')


def load():
    return json.load(io.open(STATE, encoding='utf-8')) if os.path.exists(STATE) else {}


def save(s):
    io.open(STATE, 'w', encoding='utf-8').write(json.dumps(s, indent=1))


def logrec(rec):
    rec['ts'] = time.strftime('%Y-%m-%dT%H:%M:%S')
    with io.open(LOG, 'a', encoding='utf-8') as f:
        f.write(json.dumps(rec, ensure_ascii=False) + '\n')


def texts(msgs):
    out = []
    for m in msgs:
        for c in ((m.get('result') or {}).get('content') or []):
            if c.get('type') == 'text':
                out.append(c['text'])
    return '\n'.join(out)


def images(msgs):
    out = []
    for m in msgs:
        for c in ((m.get('result') or {}).get('content') or []):
            if c.get('type') == 'image':
                out.append(Image.open(io.BytesIO(base64.b64decode(c['data']))).convert('RGBA'))
    return out


# -- queue / fetch --------------------------------------------------------------

def queue(only=None):
    st = load()
    for key, a in ASSETS.items():
        if only and key not in only:
            continue
        if st.get(key, {}).get('id'):
            print('%-18s already queued -> %s' % (key, st[key]['id'][:8]))
            continue
        w, h = a['args']['width'], a['args']['height']
        if w % 4 or h % 4:
            raise SystemExit('%s is %dx%d; PixelLab needs both sides divisible by 4, and '
                             'it fails at GET rather than at queue time.' % (key, w, h))
        # 2000 CHARACTERS IS THE CEILING, and it is checked HERE because the server's
        # refusal is a validation error that leaves NO job id: the queue loop walks on,
        # the state file records nothing, and the take is silently absent from the report.
        # Two till prompts were 13 and 37 characters over and this is how they went missing.
        if len(a['args']['description']) > 2000:
            raise SystemExit('%s prompt is %d chars; create_image_pro caps it at 2000 and '
                             'refuses without booking a job.'
                             % (key, len(a['args']['description'])))
        args = dict(a['args'], seed=a['seed'], reference_images=refs(a['family'], key))
        msgs = pixellab.call(a['tool'], args, timeout=900)
        body = texts(msgs)
        m = UUID.search(body)
        st[key] = {'id': m.group(0) if m else None, 'family': a['family']}
        save(st)
        logrec({'asset': key, 'batch': 'vice-room 2026-08-25', 'tool': a['tool'],
                'seed': a['seed'], 'prompt': a['args']['description'],
                'refs': ['palette_miami', 'ref:' + a['family'], 'style:v3_bourbon'],
                'size': [w, h], 'job': st[key]['id'],
                'event': 'queued' if m else 'queue-failed', 'raw': body[:300]})
        print('%-18s -> %s' % (key, st[key]['id'] or body[:160].replace('\n', ' ')))
        time.sleep(0.6)


def fetch():
    """Pull every finished job. EVERY candidate is kept: create_image_pro returns several
    at small sizes and the whole point of this batch is a choice."""
    os.makedirs(RAW, exist_ok=True)
    st = load()

    def done(key):
        return os.path.exists(os.path.join(RAW, key + '_0.png'))

    def pending():
        return {k: v for k, v in st.items() if v.get('id') and not done(k)}

    for _ in range(100):
        if not pending():
            break
        moved = False
        for key, rec in sorted(pending().items()):
            msgs = pixellab.call('get_image', {'job_id': rec['id']}, timeout=300)
            ims, body = images(msgs), texts(msgs)
            if ims:
                for i, im in enumerate(ims):
                    im.save(os.path.join(RAW, '%s_%d.png' % (key, i)))
                print('fetched %-18s %d aday, %dx%d'
                      % (key, len(ims), ims[0].width, ims[0].height))
                logrec({'asset': key, 'event': 'fetched', 'candidates': len(ims)})
                moved = True
            elif 'failed' in body.lower():
                print('FAILED', key, body[:200].replace('\n', ' '))
                logrec({'asset': key, 'event': 'failed', 'raw': body[:300]})
                rec['id'] = None
                save(st)
                moved = True
        if pending() and not moved:
            print(' %d pending...' % len(pending()))
            time.sleep(25)
    print('missing:', sorted(pending()) if pending() else 'none')


# -- post -----------------------------------------------------------------------

def key_green(im):
    """Cut the chroma green window panes, so the sky stays derived from the room's own
    hole rather than painted into it (14 v3 SS7)."""
    a = np.asarray(im.convert('RGBA')).copy()
    r, g, b = (a[:, :, i].astype(np.int16) for i in range(3))
    m = ((a[:, :, 3] > 0) & (g > 150) & (r < 110) & (b < 110)
         & (g - np.maximum(r, b) > 55))
    a[m] = (0, 0, 0, 0)
    return Image.fromarray(a, 'RGBA'), int(m.sum())


# The cellar's own interior, Night[0] - the tone both counter prompts asked for.
BAY_TONE = (0x0D, 0x08, 0x13, 255)


def fill_bays(im, tone=BAY_TONE):
    """Close the bays. A COUNTER HAS NO HOLES IN IT.

    Both counter takes came back with their shelf interiors transparent, twice, through
    two different prompts - and it is not really the model's mistake. On a call with
    `no_background: True` a "solid opaque near-black back wall" and "the background" are
    the same pixels as far as the alpha cut is concerned, so the recess goes out with the
    surround. In the game that would be a counter you can see the ROOM through, which is
    the one thing a cabinet cannot be.

    What is filled is defined structurally, not by colour: for every column that has any
    ink at all, the topmost opaque pixel is the slab, and every transparent pixel BELOW
    that line in that column is inside the unit. Nothing outside the silhouette is
    touched, so the counter's ends stay cut and the plate still keys against the room.

    This is a keying step - the exact inverse of `key_green`, which cuts a hole the art
    marked out - and not painting over the take: one flat palette tone into a region the
    generator drew as empty. Anything it gets WRONG shows up immediately in the preview
    as a dark block where a gap should be.
    """
    a = np.asarray(im.convert('RGBA')).copy()
    op = a[:, :, 3] >= 128
    has = op.any(axis=0)
    if not has.any():
        return im, 0
    h = a.shape[0]
    top = np.where(has, op.argmax(axis=0), h)          # first opaque row per column
    ys = np.arange(h)[:, None]
    inside = (ys >= top[None, :]) & has[None, :] & (~op)
    a[inside] = tone
    return Image.fromarray(a, 'RGBA'), int(inside.sum())


def content_top(im, cover=0.6):
    """First row that is SUBSTANTIALLY opaque, not merely non-empty - a stray pixel of
    back edge thirty rows above the slab once began a crop and shipped a band of nothing."""
    op = (np.asarray(im)[:, :, 3] >= 128).sum(axis=1)
    rows = np.where(op >= cover * im.width)[0]
    return int(rows.min()) if len(rows) else 0


def candidates(key):
    out = []
    for i in range(64):
        p = os.path.join(RAW, '%s_%d.png' % (key, i))
        if not os.path.exists(p):
            break
        out.append(Image.open(p).convert('RGBA'))
    return out


def post(only=None):
    """Bring every candidate to the shape the stage would want. NO RESAMPLE on the happy
    path - each plate was asked for at its final size."""
    os.makedirs(STAGE, exist_ok=True)
    for key, a in ASSETS.items():
        if only and key not in only:
            continue
        for i, im in enumerate(candidates(key)):
            if a['post'] == 'room':
                im, cut = key_green(im)
                print('  %-18s#%d panes keyed: %d px (%.1f%%)'
                      % (key, i, cut, 100.0 * cut / (im.width * im.height)))
            elif a['post'] == 'counter':
                im = nb.ship(im)
                im, filled = fill_bays(im)
                print('  %-18s#%d gozler dolduruldu: %d px (%.1f%%)'
                      % (key, i, filled, 100.0 * filled / (im.width * im.height)))
                top = content_top(im)
                have = min(top + COUNTER_H, im.height) - top
                strip = Image.new('RGBA', (COUNTER_W, COUNTER_H), (0, 0, 0, 0))
                strip.paste(im.crop((0, top, COUNTER_W, top + have)), (0, 0))
                # SAY IT WHEN THE TAKE IS SHORT. The first counter pair was centred in the
                # frame, so the crop began 46 rows down and the plate ended 43 rows short -
                # and the pad is transparent, which in a preview looks exactly like a
                # counter that simply stops. A number here is how that gets noticed.
                print('  %-18s#%d slab top row %d -> %dx%d%s'
                      % (key, i, top, COUNTER_W, COUNTER_H,
                         '' if have >= COUNTER_H
                         else '  KISA: %d satir eksik, alt %d satir bos'
                              % (COUNTER_H - have, COUNTER_H - have)))
                im = strip
            out = nb.ship(im)
            if a['post'] == 'prop':
                # TRIMMED TO THE INK. PixelLab centres an object in its frame and leaves a
                # margin - "fills the whole frame" is asked for and never fully obeyed
                # (0.79 to 0.99 across this project's batches). The stage scales a prop by
                # its SPRITE width into a fixed footprint, so an untrimmed till would be
                # drawn at 46 of its 57 units and read small on the counter for a reason
                # nobody could see. Trimming is also what makes the preview's composite
                # honest, which is the whole point of the composite.
                box = out.getbbox()
                if box:
                    out = out.crop(box)
            out.save(os.path.join(STAGE, '%s_%d.png' % (key, i)))
            print('  staged %-18s#%d %dx%d  renk=%d  duz=%%%.1f'
                  % (key, i, out.width, out.height, colours(out), flatness(out)))
        logrec({'asset': key, 'event': 'staged', 'batch': 'vice-room 2026-08-25'})


# -- measurement ----------------------------------------------------------------

def colours(im):
    return len(im.convert('RGB').getcolors(maxcolors=1 << 24) or [])


def flatness(im):
    """Share of horizontally adjacent opaque pairs that are IDENTICAL. The crispness
    number this project judges scene art on: an area-downscale almost never lands on two
    identical neighbours, and pixel art is BUILT from flat runs. Measured on the shipped
    art: room 9.9%, its counter 36.5%, against 69.0% for a v3 bottle."""
    a = np.asarray(im.convert('RGBA'))
    rgb, al = a[:, :, :3].astype(np.int16), a[:, :, 3]
    both = (al[:, :-1] >= 128) & (al[:, 1:] >= 128)
    if not both.any():
        return 0.0
    same = (rgb[:, :-1, :] == rgb[:, 1:, :]).all(2) & both
    return 100.0 * same.sum() / both.sum()


def plank_bands(im, bay=1):
    """Which rows a take's shelf planks ACTUALLY cross a bay at, measured off the plate.

    The overlay answers this by eye, and this answers it in numbers, because "close
    enough" is the whole question for a counter: the cellar table wants a bottle's foot on
    row 143 and row 233, and a take whose planks land ten rows off is not unusable - it is
    a two-number re-measure. A take whose planks land eighty rows off is a different piece
    of furniture. Only the numbers separate those two cases.

    Measured on the BAY's own columns so the posts and the slab's ends cannot vote, and a
    row counts as structure when most of the bay's width is opaque and not the bay's own
    dark interior."""
    a = np.asarray(im.convert('RGBA'))
    cx = CELLAR_BAY_CENTRE[bay]
    x0, x1 = cx - CELLAR_BAY_WIDTH // 2 + 12, cx + CELLAR_BAY_WIDTH // 2 - 12
    strip = a[:, max(0, x0):min(a.shape[1], x1)]
    op = strip[:, :, 3] >= 128
    dark = op & (strip[:, :, :3].astype(int).sum(2) < 90)
    solid = (op & ~dark).mean(axis=1) > 0.7
    runs, cur = [], None
    for y, v in enumerate(solid):
        if v and cur is None:
            cur = y
        elif not v and cur is not None:
            runs.append((cur, y - 1))
            cur = None
    if cur is not None:
        runs.append((cur, len(solid) - 1))
    # Only the bands below the slab are shelves; the slab is the run that starts at row 0.
    return [r for r in runs if r[0] > CELLAR_CEIL_ROWS[0] and r[1] - r[0] >= 3]


def counter_posts(a):
    """The four vertical posts, as (first, last) column pairs.

    A POST is a column of ONE flat material from the top of the shelf band to the bottom;
    a BAY column crosses the interior AND both planks, so it varies far more. That is the
    whole test - low variation down the band - and it is art-agnostic, which matters
    because the shipped counter's bays are LIGHTER than its posts while both new takes'
    bays are darker. Anything keyed on brightness would read one of them backwards.

    Two of the four are known before any measuring: the plate's own ends stand on posts.
    Taking the four WIDEST flat runs alone got this wrong on the sunset take, which has a
    flat stretch of slab in the middle wider than its end post."""
    h, w = a.shape[0], a.shape[1]
    band = slice(int(h * 0.32), int(h * 0.92))
    sd = (a[band, :, :3].sum(2) / 3.0).std(axis=0)
    flat = sd < np.percentile(sd, 35)
    runs, start = [], None
    for x, v in enumerate(list(flat) + [False]):
        if v and start is None:
            start = x
        elif not v and start is not None:
            runs.append((start, x - 1))
            start = None
    left = [r for r in runs if r[0] <= 2]
    right = [r for r in runs if r[1] >= w - 3]
    cand = [r for r in runs if r not in left and r not in right and r[1] - r[0] >= 3]
    L = left[0] if left else (0, 0)
    R = right[-1] if right else (w - 1, w - 1)

    # THREE EQUAL BAYS is the prior, and picking the two WIDEST flat runs was not it.
    # A take whose bay interiors are near-black and featureless has bay columns as flat as
    # its posts, so the widest-run rule chose a stretch of empty shelf as a post and
    # reported bays 182, 274 and 48 wide on a counter that is visibly even. The counter is
    # described to the generator as three equal bays and drawn that way in every take, so
    # the pair to pick is the one that MAKES them equal - which repairs the chrome take
    # (182/190/183) and leaves the shipped plate's own measurement untouched at
    # 119/319/517, the number this whole function is validated against.
    best, score = None, None
    for i in range(len(cand)):
        for j in range(i + 1, len(cand)):
            p = [L, cand[i], cand[j], R]
            wd = [p[k + 1][0] - p[k][1] - 1 for k in range(3)]
            if min(wd) < 40:
                continue
            spread = float(np.var(wd))
            if score is None or spread < score:
                best, score = p, spread
    return best if best else sorted(left[:1] + cand[:2] + right[-1:])


def counter_opening(a, posts):
    """The row the shelf opening starts at - what ShutterOpeningTopPx names.

    ABOVE the opening a bay column and a post column are the SAME slab; below it they are
    interior and post. So the opening is the first row where the two part company and stay
    parted. Measured on the shipped counter this returns 65, which is the constant the code
    already carries - which is how a measuring tool earns the right to be believed about
    art nobody has measured yet."""
    h = a.shape[0]
    bx = (posts[0][1] + posts[1][0]) // 2
    px = (posts[1][0] + posts[1][1]) // 2
    d = np.abs(a[:, bx, :3] - a[:, px, :3]).sum(axis=1)
    for y in range(4, h - 6):
        if (d[y:y + 6] > 40).all():
            return y
    return 0


def counter_table(im):
    """Every number DiegeticStage measures off counter.png, read back off a take.

    Choosing a counter is then a mechanical change and not a research project: this prints
    the constant block that take would need. VALIDATED ON THE SHIPPED PLATE - it returns
    posts 0-32/206-226/412-430/605-637, bays 119/319/517, narrowest 173 and opening 65
    against the code's own 7-32/209-226/412-429/605-630, 120/319/517, 175 and 65.

    The one number it CANNOT re-measure is the shutter. counter_shutter.png is 592x176 and
    was drawn to fill rows 65..241 of the shipped counter; a take whose opening starts
    higher needs a taller roller, and that is new art, not a new constant."""
    a = np.asarray(im.convert('RGBA')).astype(int)
    posts = counter_posts(a)
    centres = [(posts[i][1] + posts[i + 1][0]) // 2 for i in range(len(posts) - 1)]
    widths = [posts[i + 1][0] - posts[i][1] - 1 for i in range(len(posts) - 1)]
    top = counter_opening(a, posts)
    bands = plank_bands(im)
    feet = [(lo + hi) // 2 for lo, hi in bands]
    ceils = [top] + [hi + 1 for lo, hi in bands[:-1]]
    return dict(posts=posts, centres=centres, width=min(widths) if widths else 0,
                top=top, bands=bands, feet=feet, ceils=ceils,
                opening_h=im.height - top)


def side_travel(im):
    """How far the silhouette's SIDE EDGE travels sideways, as a share of the width.
    The number that says whether the 30 degree instruction landed.

    THE FIRST VERSION OF THIS MEASURED THE WRONG THING and was replaced before it ever
    reached the author. It reported how much narrower the top third was than the widest
    row - and the shipped register scored 16% on that, because it has a raised display
    head that is narrower than its body. A machine drawn dead flat can taper; tapering is
    not perspective, so that metric could not tell the two apart, and it would have
    printed a reassuring number under a picture that failed the brief.

    What a receding top face and a visible side wall actually produce is a side edge that
    WALKS: the leftmost opaque pixel moves steadily across as you go down the machine. A
    front-on box's side edge is vertical and barely moves. Measured on the shipped till it
    is 20%; every take in this batch is between 49% and 68%, and the two do not overlap.
    """
    a = np.asarray(im.convert('RGBA'))
    op = a[:, :, 3] >= 128
    rows = np.where(op.any(axis=1))[0]
    if not len(rows):
        return 0.0
    lefts = np.array([np.where(op[y])[0].min() for y in rows])
    rights = np.array([np.where(op[y])[0].max() for y in rows])
    width = float(rights.max() - lefts.min() + 1)
    return 100.0 * (lefts.max() - lefts.min()) / width if width else 0.0


# -- the preview ----------------------------------------------------------------

def shipped(folder, name):
    p = os.path.join(folder, name + '.png')
    return Image.open(p).convert('RGBA') if os.path.exists(p) else None


def sky(hole):
    """scene_nb_post's derived sky as an image, so a preview shows what the keyed panes
    actually look out on."""
    h, w = hole.shape
    rows = np.where(hole.any(1))[0]
    if not len(rows):
        return None
    ys = np.arange(h)[:, None].repeat(w, 1)
    t = (ys - rows.min()) / max(1, rows.max() - rows.min())
    bayer = (np.indices((h, w)).sum(0) % 2) * 0.5
    t = np.clip(t + (bayer - 0.25) * 0.14, 0, 1)
    out = np.zeros((h, w, 4), np.uint8)
    for c in range(3):
        out[:, :, c] = (nb.SKY_TOP[c] + (nb.SKY_LOW[c] - nb.SKY_TOP[c]) * t).astype(np.uint8)
    out[:, :, 3] = np.where(hole, 255, 0)
    out[~hole] = (0, 0, 0, 0)
    return Image.fromarray(out, 'RGBA')


def composite(room, counter, till=None, extra=None):
    """The stage as DiegeticStage builds it, in the stage's own coordinates: sky behind
    the room's hole, the counter hung from the rest line, the till standing on it inside
    its real 57-unit footprint.

    This is the only honest way to judge a prop. At 4x on black every take looks good;
    what settles a till is whether it reads at 57 units with a customer's head beside it."""
    base = Image.new('RGBA', (REF_W, REF_H), (0, 0, 0, 255))
    s = sky(np.asarray(room)[:, :, 3] < 128)
    if s is not None:
        base.alpha_composite(s)
    base.alpha_composite(room)
    for im, x, foot in (extra or []):
        base.alpha_composite(im, (int(x - im.width / 2), int(REF_H - foot - im.height)))
    if counter is not None:
        top_row = REF_H - (COUNTER_REST_Y + COUNTER_INSET)
        w = min(REF_W, counter.width)
        base.alpha_composite(counter.crop((0, 0, w, min(REF_H - top_row, counter.height))),
                             ((REF_W - w) // 2, top_row))
    if till is not None:
        w = REGISTER_W
        h = int(round(w * till.height / float(till.width)))
        small = till.resize((w, h), Image.BOX) if till.width != w else till
        base.alpha_composite(small, (int(REGISTER_X - w / 2), REF_H - REGISTER_BASE_Y - h))
    return base


def cellar_marks(counter):
    """The code's own cellar table, DRAWN on the take: three bay centres and their edges,
    the two plank surfaces a bottle stands on, and the two ceiling rows. A counter whose
    planks do not land on the cyan lines is not a drop-in - and this is how that is seen
    before it is discovered by a bottle floating in mid-air."""
    im = counter.copy()
    d = ImageDraw.Draw(im)
    rose, cyan, brass = (232, 77, 166, 255), (59, 200, 190, 210), (232, 163, 61, 220)
    for row in CELLAR_FOOT_ROWS:
        d.line([(0, row), (im.width - 1, row)], fill=cyan)
    for row in CELLAR_CEIL_ROWS:
        d.line([(0, row), (im.width - 1, row)], fill=brass)
    for cx in CELLAR_BAY_CENTRE:
        d.line([(cx, CELLAR_CEIL_ROWS[0]), (cx, CELLAR_FOOT_ROWS[-1])], fill=rose)
        for e in (cx - CELLAR_BAY_WIDTH // 2, cx + CELLAR_BAY_WIDTH // 2):
            d.line([(e, CELLAR_CEIL_ROWS[0]), (e, CELLAR_FOOT_ROWS[-1])],
                   fill=(146, 36, 100, 210))
    return im


def zoom(im, box=None, factor=3):
    crop = im.crop(box) if box else im
    return crop.resize((crop.width * factor, crop.height * factor), Image.NEAREST)


def b64(im):
    buf = io.BytesIO()
    im.save(buf, format='PNG')
    return base64.b64encode(buf.getvalue()).decode('ascii')


def staged_all(key):
    out = []
    for i in range(64):
        p = os.path.join(STAGE, '%s_%d.png' % (key, i))
        if not os.path.exists(p):
            break
        out.append(Image.open(p).convert('RGBA'))
    return out


CSS = """
:root{
  --ink:#F2E8D5; --ink-dim:#C9BCA8; --ink-faint:#9C8F80;
  --ground:#1A1023; --panel:#241830; --panel-hi:#362447;
  --line:#4A3160; --rose:#E84DA6; --petrol:#3BC8BE; --brass:#E8A33D;
}
*{box-sizing:border-box}
body{margin:0; background:var(--ground); color:var(--ink);
  font-family:"IBM Plex Sans","Segoe UI",system-ui,sans-serif;
  font-size:15px; line-height:1.6; -webkit-font-smoothing:antialiased}
.wrap{max-width:1080px; margin:0 auto; padding:56px 26px 96px}
.eyebrow{margin:0; font-family:Silkscreen,"IBM Plex Mono",monospace; font-size:11px;
  letter-spacing:.18em; text-transform:uppercase; color:var(--rose)}
h1{font-family:Silkscreen,"IBM Plex Mono",monospace; font-weight:400;
  font-size:clamp(22px,3.6vw,34px); line-height:1.3; margin:16px 0 0; text-wrap:balance}
h2.sec{font-family:Silkscreen,"IBM Plex Mono",monospace; font-weight:400; font-size:20px;
  margin:64px 0 0; padding-top:26px; border-top:2px solid var(--rose); color:var(--rose)}
.lede{max-width:66ch; color:var(--ink-dim); margin:18px 0 0}
.lede b{color:var(--ink); font-weight:600}
code{font-family:"IBM Plex Mono",monospace; font-size:.9em; color:var(--brass)}
.take{padding:34px 0; border-top:1px solid var(--line); display:grid; gap:16px}
.head{display:flex; flex-wrap:wrap; align-items:baseline; gap:12px}
.head h3{font-family:Silkscreen,"IBM Plex Mono",monospace; font-weight:400; font-size:17px;
  margin:0; color:var(--ink)}
.tag{font-family:"IBM Plex Mono",monospace; font-size:10.5px; letter-spacing:.1em;
  text-transform:uppercase; padding:3px 9px; border:1px solid var(--line);
  color:var(--ink-faint)}
.tag.now{color:var(--brass); border-color:#8F5A1E; background:#3A2410}
.tag.where{color:var(--petrol); border-color:#1B5F66}
.note{max-width:66ch; color:var(--ink-dim); margin:0}
figure{margin:0; display:grid; gap:8px}
.shot{background:var(--panel); border:1px solid var(--line); padding:10px; overflow-x:auto}
.shot img{display:block; max-width:100%; height:auto; image-rendering:pixelated}
.wide .shot img{width:640px}
.alpha .shot{background:
  repeating-conic-gradient(var(--panel-hi) 0 25%, var(--panel) 0 50%) 0 0/16px 16px}
figcaption{font-family:"IBM Plex Mono",monospace; font-size:11.5px; color:var(--ink-faint)}
.row{display:flex; flex-wrap:wrap; gap:16px; align-items:flex-end}
.pair{display:grid; grid-template-columns:repeat(auto-fit,minmax(300px,1fr)); gap:16px}
.meta{display:grid; grid-template-columns:repeat(auto-fit,minmax(150px,1fr));
  gap:12px 22px; margin:0; padding:14px 16px; background:var(--panel);
  border:1px solid var(--line)}
.meta > div{display:grid; gap:3px}
dt{font-family:"IBM Plex Mono",monospace; font-size:10.5px; letter-spacing:.1em;
  text-transform:uppercase; color:var(--ink-faint)}
dd{margin:0; font-family:"IBM Plex Mono",monospace; font-size:12.5px;
  font-variant-numeric:tabular-nums; color:var(--ink)}
dd.good{color:var(--petrol)} dd.warn{color:var(--brass)} dd.bad{color:var(--rose)}
details{border-top:1px solid var(--line); padding-top:12px}
summary{cursor:pointer; font-family:"IBM Plex Mono",monospace; font-size:11.5px;
  letter-spacing:.06em; text-transform:uppercase; color:var(--ink-faint)}
details p{font-family:"IBM Plex Mono",monospace; font-size:12px; line-height:1.75;
  color:var(--ink-dim); max-width:82ch}
pre.table{margin:0; padding:14px 16px; background:#0D0813; border:1px solid var(--line);
  overflow-x:auto; font-family:"IBM Plex Mono",monospace; font-size:12px; line-height:1.7;
  color:var(--petrol); white-space:pre}
footer{margin-top:52px; padding-top:22px; border-top:1px solid var(--line);
  color:var(--ink-faint); font-size:13px; max-width:74ch}
"""


def fig(im, caption, alt, cls=''):
    return ('<figure class="%s"><div class="shot"><img alt="%s" '
            'src="data:image/png;base64,%s"></div><figcaption>%s</figcaption></figure>'
            % (cls, alt, b64(im), caption))


def report():
    room0 = shipped(BACKGROUNDS, 'club_room')
    counter0 = shipped(BACKGROUNDS, 'counter')
    till0 = shipped(PROPS, 'register2')
    if room0 is None or counter0 is None or till0 is None:
        raise SystemExit('the shipped plates are missing - nothing to compare against')

    p, a = [], None
    a = p.append
    a('<title>Vice odasi &amp; kasa &mdash; onizleme</title>')
    a('<link rel="stylesheet" href="https://fonts.googleapis.com/css2?'
      'family=IBM+Plex+Mono:wght@400;500&family=IBM+Plex+Sans:wght@400;600&'
      'family=Silkscreen&display=swap">')
    a('<style>%s</style>' % CSS)
    a('<div class="wrap">')
    a('<p class="eyebrow">Last Call &middot; PixelLab partisi &middot; %s</p>'
      % time.strftime('%Y-%m-%d'))
    a('<h1>Vice odasi ve yeni kasa</h1>')
    a('<p class="lede">Hicbiri oyuna kopyalanmadi. Hepsi '
      '<code>Tools/AssetPipeline/staging/vice_room/</code> altinda duruyor; sen sectikten '
      'sonra <code>Assets/</code> altina tasinir. Her take sahnede duracagi gibi '
      'gosteriliyor: gokyuzu odanin kendi deliginden turetildi, tezgah <code>y 122</code> '
      'dayanma hattindan sarkiyor, kasa <b>57 birimlik</b> gercek ayak izinde duruyor. '
      '<b>Butun uretim</b> sahnenin kendi olcusunde, yeniden olcekleme olmadan yapildi ve '
      'hicbirinde pisirilmis isik, yansima ya da golge yok &mdash; oda Unity\'de URP 2D '
      'isikla aydinlaniyor.</p>')

    # -- the till -------------------------------------------------------------
    a('<h2 class="sec">1 &middot; Kasa</h2>')
    a('<p class="lede">Brief: <b>2.5D, 30 derece, tezgah referans, vice.</b> Uc makine, '
      'tek aci &mdash; aci prompt\'ta harfi harfine ayni cumle, cunku degismesi gereken '
      'makinenin kendisi. <b>Yan kenar kaymasi</b> olculen sayi: siluetin en sol pikseli '
      'asagi inerken yana ne kadar yuruyor. Duz cizilmis bir kutuda yan kenar diktir ve '
      'bu sayi kucuk kalir; ust yuzeyi gorunen bir makinede kenar kosegen olur. '
      '<b>Oyundaki kasa %20</b>, bu partideki her take %49 ile %68 arasi &mdash; iki '
      'grup hic ortusmuyor.</p>')
    rows = [('shipped', dict(label='Su an oyunda (register2)', note='49x100 duz cephe. '
             'Ust yuzeyi yok: makineye tepeden bakilmiyor, o yuzden tezgahin uzerinde '
             'durmuyor, tezgaha yapistirilmis gibi duruyor.', where='tezgahin sag ucu',
             seed=None, tool='(eski)'), [till0])]
    for key, asset in ASSETS.items():
        if asset['family'] != 'till':
            continue
        rows.append((key, asset, staged_all(key)))
    for key, asset, ims in rows:
        if not ims:
            continue
        a('<section class="take">')
        a('<div class="head"><h3>%s</h3>%s<span class="tag where">%s</span></div>'
          % (asset['label'],
             '<span class="tag now">oyunda</span>' if key == 'shipped' else '',
             asset['where']))
        a('<p class="note">%s</p>' % asset['note'])
        a('<div class="row">')
        for i, im in enumerate(ims):
            a(fig(zoom(im, factor=3), 'aday %d &mdash; %dx%d, 3&times;' % (i, im.width,
              im.height), 'kasa adayi', 'alpha'))
        a('</div>')
        a(fig(composite(room0, counter0, till=ims[0]),
              'Sahnede &mdash; aday 0, gercek 57 birimlik ayak izinde',
              'kasa sahnede', 'wide'))
        a('<dl class="meta">')
        a('<div><dt>uretim</dt><dd>%s</dd></div>' % asset['tool'])
        a('<div><dt>seed</dt><dd>%s</dd></div>' % (asset['seed'] or '&mdash;'))
        a('<div><dt>aday</dt><dd>%d</dd></div>' % len(ims))
        t = side_travel(ims[0])
        a('<div><dt>yan kenar kaymasi</dt><dd class="%s">%%%.1f</dd></div>'
          % ('good' if t >= 40 else 'bad', t))
        a('<div><dt>renk</dt><dd>%d</dd></div>' % colours(ims[0]))
        a('<div><dt>duz kosu</dt><dd>%%%.1f</dd></div>' % flatness(ims[0]))
        a('</dl>')
        if key != 'shipped':
            a('<details><summary>prompt</summary><p>%s</p></details>'
              % asset['args']['description'])
        a('</section>')

    # -- the room -------------------------------------------------------------
    a('<h2 class="sec">2 &middot; Oda</h2>')
    a('<p class="lede">Ayni kutu, ayni kamera, ayni pencere deligi &mdash; degisen '
      'uretim ve ton. Oyundaki plaka birkac bin piksel boyanip 640\'a kucultuldugu icin '
      'hicbir kenari keskin degil; asagidaki <b>duz kosu</b> sayisi tam olarak bunu '
      'olcuyor (oyundaki oda %%9.9, elle cizilmis bir sise %%69).</p>')
    a('<section class="take"><div class="head"><h3>Su an oyunda (club_room)</h3>'
      '<span class="tag now">oyunda</span></div>')
    a(fig(composite(room0, counter0, till=till0), 'Sahne bilesigi', 'oyundaki oda', 'wide'))
    a('<dl class="meta"><div><dt>renk</dt><dd class="bad">%d</dd></div>'
      '<div><dt>duz kosu</dt><dd class="bad">%%%.1f</dd></div></dl></section>'
      % (colours(room0), flatness(room0)))
    for key, asset in ASSETS.items():
        if asset['family'] != 'room':
            continue
        ims = staged_all(key)
        if not ims:
            continue
        im = ims[0]
        a('<section class="take">')
        a('<div class="head"><h3>%s</h3><span class="tag where">%s</span></div>'
          % (asset['label'], asset['where']))
        a('<p class="note">%s</p>' % asset['note'])
        a(fig(composite(im, counter0, till=till0),
              'Sahne bilesigi &mdash; oyundaki tezgah ve kasayla', 'oda take', 'wide'))
        a('<div class="pair">')
        a(fig(im, 'Plakanin kendisi &mdash; %dx%d, pencere kesilmis' % im.size,
              'oda plakasi', 'wide alpha'))
        a(fig(zoom(im, (0, 40, 160, 130)), 'Pencere kayitlari, 3&times;', 'detay'))
        a('</div>')
        a('<dl class="meta">')
        a('<div><dt>uretim</dt><dd>%s</dd></div>' % asset['tool'])
        a('<div><dt>seed</dt><dd>%s</dd></div>' % asset['seed'])
        a('<div><dt>cozunurluk</dt><dd class="good">%dx%d dogrudan</dd></div>' % im.size)
        a('<div><dt>renk</dt><dd>%d</dd></div>' % colours(im))
        f = flatness(im)
        a('<div><dt>duz kosu</dt><dd class="%s">%%%.1f</dd></div>'
          % ('good' if f > 40 else 'warn', f))
        a('</dl>')
        a('<details><summary>prompt</summary><p>%s</p></details>'
          % asset['args']['description'])
        a('</section>')

    # -- the counter ----------------------------------------------------------
    a('<h2 class="sec">3 &middot; Tezgah</h2>')
    a('<p class="lede">Tezgah oyunun en <b>olculu</b> parcasi: arkasindaki mahzenin uc '
      'gozu, dort dikmesi ve iki rafi <code>DiegeticStage</code> icinde satir satir '
      'yaziliyor. Onizlemedeki cizgiler kodun kendi tablosu &mdash; '
      '<span style="color:#3BC8BE">turkuaz</span> satirlar sisenin ustune bastigi raf '
      'yuzeyi (143 ve 233), <span style="color:#E8A33D">amber</span> satirlar tavan '
      '(65 ve 150), <span style="color:#E84DA6">pembe</span> dikeyler goz merkezleri. '
      '<b>Take\'in raflari turkuaz cizgilere oturmuyorsa</b> o tezgah oyuna dogrudan '
      "giremez; tablo yeniden olculur &mdash; her take’in altinda o tablonun HAZIR "
      "HALI yaziyor, yapistirilacak sekilde. Olcen kod OYUNDAKI tezgahin ustunde "
      "dogrulandi: ayni yontem oradan 119/319/517 ve acilis 65 okuyor, yani kodun kendi "
      "120/319/517 ve 65’ini.</p>"
      "<p class=\"lede\"><b>Ama uc take’in ucunde de tablonun cozmedigi ayni engel "
      "var:</b> ucunun de TABLASI oyundakinden cok daha ince, o yuzden raf acikligi "
      "65’te degil 32/38/44’te basliyor &mdash; aciklik 176 degil <b>197-209 satir</b>. "
      "Kepenk sanati (<code>counter_shutter.png</code>, 592&times;176) tam olarak "
      "oyundaki 65..241 araligini kaplasin diye cizilmisti. Bunlardan biri secilirse "
      "kepengin de yeniden cizilmesi gerekir &mdash; bu bir sabit degisikligi degil, "
      "YENI SANAT. Alternatif: tablayi kalinlastirip tezgahi yeniden cekmek.</p>")
    a('<section class="take"><div class="head"><h3>Su an oyunda (counter)</h3>'
      '<span class="tag now">oyunda</span></div>')
    a(fig(cellar_marks(counter0), 'Kod tablosu ustune cizildi &mdash; oyundaki tezgah',
          'oyundaki tezgah', 'wide alpha'))
    a('<dl class="meta"><div><dt>olcu</dt><dd>%dx%d</dd></div>'
      '<div><dt>renk</dt><dd>%d</dd></div>'
      '<div><dt>duz kosu</dt><dd>%%%.1f</dd></div></dl></section>'
      % (counter0.width, counter0.height, colours(counter0), flatness(counter0)))
    for key, asset in ASSETS.items():
        if asset['family'] != 'counter':
            continue
        ims = staged_all(key)
        if not ims:
            continue
        im = ims[0]
        a('<section class="take">')
        a('<div class="head"><h3>%s</h3><span class="tag where">%s</span></div>'
          % (asset['label'], asset['where']))
        a('<p class="note">%s</p>' % asset['note'])
        a(fig(cellar_marks(im), 'Kod tablosu ustune cizildi &mdash; raflar turkuaza '
              'oturuyor mu?', 'tezgah take', 'wide alpha'))
        a(fig(composite(room0, im, till=till0), 'Sahne bilesigi', 'tezgah sahnede', 'wide'))
        a('<dl class="meta">')
        a('<div><dt>uretim</dt><dd>%s</dd></div>' % asset['tool'])
        a('<div><dt>seed</dt><dd>%s</dd></div>' % asset['seed'])
        a('<div><dt>olcu</dt><dd>%dx%d</dd></div>' % im.size)
        a('<div><dt>renk</dt><dd>%d</dd></div>' % colours(im))
        f = flatness(im)
        a('<div><dt>duz kosu</dt><dd class="%s">%%%.1f</dd></div>'
          % ('good' if f > 40 else 'warn', f))
        t = counter_table(im)
        drift = ([min(abs(m - w) for w in CELLAR_FOOT_ROWS) for m in t['feet']] or [999])
        a('<div><dt>raf satirlari</dt><dd class="%s">%s</dd></div>'
          % ('good' if max(drift) <= 6 else 'warn',
             ', '.join('%d-%d' % b for b in t['bands']) or 'yok'))
        a('<div><dt>acilis ust satiri</dt><dd class="%s">%d (kod: 65)</dd></div>'
          % ('good' if abs(t['top'] - 65) <= 3 else 'bad', t['top']))
        a('<div><dt>acilis yuksekligi</dt><dd class="%s">%d (kepenk 176)</dd></div>'
          % ('good' if abs(t['opening_h'] - 176) <= 4 else 'bad', t['opening_h']))
        a('</dl>')
        a('<pre class="table">%s</pre>' % (
            '// DiegeticStage.cs — bu take secilirse tablo bu olur\n'
            'ShutterOpeningTopPx = %df;\n'
            'CellarBayCentrePx   = { %s };\n'
            'CellarBayWidthPx    = %df;\n'
            'CellarShelfFootPx   = { %s };\n'
            'CellarShelfCeilPx   = { %s };'
            % (t['top'],
               ', '.join('%df' % c for c in t['centres']),
               t['width'],
               ', '.join('%df' % f for f in t['feet']),
               ', '.join('%df' % c for c in t['ceils']))))
        a('<details><summary>prompt</summary><p>%s</p></details>'
          % asset['args']['description'])
        a('</section>')

    # -- the dressing ---------------------------------------------------------
    a('<h2 class="sec">4 &middot; Odaya eklenebilecekler</h2>')
    a('<p class="lede">Dordunun de duracagi yer yaninda yaziyor. Uc tanesi '
      '<code>fixtures.json</code> icinde <b>zaten var olan bir yuvaya</b> giriyor, yani '
      'secilirse tek satir veri ile odaya girer; jukebox\'in yuvasi yok, secilirse '
      'acilmasi gerekir.</p>')
    for key, asset in ASSETS.items():
        if asset['family'] != 'dressing':
            continue
        ims = staged_all(key)
        if not ims:
            continue
        a('<section class="take">')
        a('<div class="head"><h3>%s</h3><span class="tag where">%s</span></div>'
          % (asset['label'], asset['where']))
        a('<p class="note">%s</p>' % asset['note'])
        a('<div class="row">')
        for i, im in enumerate(ims):
            a(fig(zoom(im, factor=3), 'aday %d &mdash; %dx%d, 3&times;'
                  % (i, im.width, im.height), 'dressing adayi', 'alpha'))
        a('</div>')
        a('<dl class="meta">')
        a('<div><dt>uretim</dt><dd>%s</dd></div>' % asset['tool'])
        a('<div><dt>seed</dt><dd>%s</dd></div>' % asset['seed'])
        a('<div><dt>aday</dt><dd>%d</dd></div>' % len(ims))
        a('<div><dt>olcu</dt><dd>%dx%d</dd></div>' % ims[0].size)
        a('<div><dt>renk</dt><dd>%d</dd></div>' % colours(ims[0]))
        a('</dl>')
        a('<details><summary>prompt</summary><p>%s</p></details>'
          % asset['args']['description'])
        a('</section>')

    a('<footer>Uretim <code>Tools/vice_room_gen.py</code>. Ham cikti '
      '<code>Tools/vice_room_raw/</code>, islenmis <code>%s</code>. Her cagri '
      '<code>Tools/AssetPipeline/generation_log.jsonl</code> icine prompt\'u ve seed\'i '
      'ile yazildi. Bir take secince soyle: dosya <code>Assets/</code> altina tasinir, '
      'tezgah secilirse mahzen tablosu yeniden olculur.</footer>')
    a('</div>')

    os.makedirs(STAGE, exist_ok=True)
    out = os.path.join(STAGE, 'preview.html')
    io.open(out, 'w', encoding='utf-8').write('\n'.join(p))
    print('wrote', os.path.relpath(out, ROOT))


def inventory():
    """WHERE EVERYTHING IS. Asked once, so it is a command now rather than an answer.

    Three places, and the difference between them is the whole proof gate:
      raw      exactly what PixelLab returned, untouched. The audit trail.
      staged   the same takes brought to the shape the stage would want - keyed, cropped,
               bays filled, trimmed to the ink. What a pick is made FROM.
      Assets/  the game. Nothing generated has been copied here.

    Both of the first two are gitignored, so they live on THIS machine only - which is
    also why nothing generated is a commit until it is picked."""
    rows = []
    for key, asset in ASSETS.items():
        raw = len(candidates(key))
        st = staged_all(key)
        size = '%dx%d' % st[0].size if st else '-'
        rows.append((asset['family'], key, asset['label'], raw, len(st), size,
                     asset['where']))
    rows.sort()
    fam = None
    total_raw = total_st = 0
    for f, key, label, raw, st, size, where in rows:
        if f != fam:
            fam = f
            print('')
            print('-- %s' % f.upper())
        print('  %-18s %-24s %d aday  %-9s %s' % (key, label, raw, size, where))
        total_raw += raw
        total_st += st
    print('')
    print('  ham      Tools/vice_room_raw/                       %d dosya' % total_raw)
    print('  islenmis Tools/AssetPipeline/staging/vice_room/     %d dosya' % total_st)
    print('  rapor   Tools/AssetPipeline/staging/vice_room/preview.html')
    print('  (ikisi de .gitignore icinde: bu makinede duruyorlar, repoda degil)')
    print('')
    print('  Assets/ altina kopyalanan uretilmis gorsel: YOK')


def status():
    st = load()
    for key in ASSETS:
        rec = st.get(key, {})
        raw = len(candidates(key))
        print('%-18s %-10s job=%s  ham=%d  staged=%d'
              % (key, ASSETS[key]['family'], (rec.get('id') or '-')[:8], raw,
                 len(staged_all(key))))


def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'status'
    rest = [x for x in sys.argv[2:] if not x.startswith('--')]
    if cmd == 'balance':
        pixellab.call('get_balance', {})
    elif cmd == 'queue':
        queue(rest or None)
    elif cmd == 'fetch':
        fetch()
    elif cmd == 'post':
        post(rest or None)
    elif cmd == 'report':
        report()
    elif cmd == 'status':
        status()
    elif cmd == 'inventory':
        inventory()
    else:
        raise SystemExit(__doc__)


if __name__ == '__main__':
    main()
