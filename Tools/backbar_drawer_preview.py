# -*- coding: utf-8 -*-
"""The backbar DRAWER, previewed before it is built (2026-08-20).

The author's mechanic, in their words and then in numbers:

    "ana tezgah backba-opened olan bu gorselin belli bir kismi oyun ekraninin altina
     sarkicak, kapak tam olarak raflarin oldugu yere oturtulacak, rafin bittigi yerde
     oyun ekrani bitecek, kapaga tiklandiginda kapak asagi dogru yavasca inecek ve
     sahneden neredeyse cikacak ucunda birkac pixel gozukecek, diger tum ekran yukari
     dogru kayacak ... ve mevcut alkoller ekranin ortasina kayan backbar raflarda
     gozukecek. herhangi arkaplan kartma vs. olmayacak"

WHAT THAT MEANS GEOMETRICALLY. The counter is ONE tall sprite and the screen is a WINDOW
onto it. Not two states of a panel - one object and a moving camera. Both frames come out
of two numbers, and the animation is one interpolation of each:

    SCENE_Y  :  0          ->  -LIFT
    SHUTTER_Y:  shelf_top  ->  SCREEN_H - PEEK

  CLOSED  the window sits high. Visible: the room, the counter's top slab, and the
          shutter lying over the shelves. The shelf's bottom edge IS the screen's bottom
          edge - the author's alignment rule, and what makes the shut bar read as a solid
          piece of furniture instead of a lid with something obviously under it.

  OPEN    the whole scene translates UP by LIFT so the shelf opening lands centred in the
          frame. The room rides up with it and is NEVER dimmed - no scrim, the author has
          now asked for that on the market and the menu too. The shutter travels the
          other way, down and out, until only PEEK pixels of its leading edge show.

MEASURED OFF THE AUTHOR'S OWN ART, not guessed (all in the counter sprite's own pixels):

    y 112-147   dark top slab, with the magenta edge line at 141-147
    y 147-177   the frame under the slab
    y 177-353   THE SHELF OPENING - 176 tall, which is the shutter's height exactly,
                confirming the shutter was drawn to cover it and nothing else
    y 249-262   the middle shelf board   -> bottles stand on y 249
    y 339-352   the bottom shelf board   -> bottles stand on y 339

Run:  py backbar_drawer_preview.py
"""
import io, os, sys, glob
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SRC = os.path.join(HERE, 'AssetPipeline', 'sources', 'pixellab_user')
OUT = os.path.join(HERE, 'scene_cast_raw')

SCREEN_W, SCREEN_H = 640, 360      # the game's art resolution (14 §5b, 16 §4)
PEEK = 4                           # "ucunda birkac pixel gozukecek"

# HOW TALL THE SHUT BAR IS, the author's number (2026-08-20: "closed iken tezgahin boyu
# 120 pixel olacak"). It is also the number that finally explains "belli bir kismi oyun
# ekraninin altina sarkicak": the counter's drawn body is 241 px tall, so showing 120 of
# it leaves 121 hanging below the screen. The lift is therefore not a free choice - it is
# exactly that 121, the amount that brings the hidden part up until the counter's bottom
# edge rests on the screen's bottom edge.
#
# That solves the hole the first mock-up had. The previous version lifted 92 px and then
# stretched the counter's last row to fill what no sprite reached; the author's answer to
# that was "raflari uzatmisin uzatma, boyu gorseldeki kadar olacak, ekleme yapma". With
# the lift pinned to the overhang there is nothing to invent: the art ends exactly where
# the screen does.
COUNTER_SHUT_H = 120

BACKGROUND = os.path.join(OUT, 'cast_room7_a.png')
COUNTER = os.path.join(SRC, 'backba-opened-png.png')
SHUTTER = os.path.join(SRC, 'backbar-kapak.png')

# Shelf geometry in the counter sprite's own pixels, from the docstring's measurement.
SHELF_TOP, SHELF_BOT = 177, 353
SHELF_SURFACES = (249, 339)        # the two rows a bottle's foot stands on
BAYS = ((14, 205), (222, 414), (431, 624))

# Two customers, because the author asked for them in the scenes. They stand behind the
# bar on the room's floor and let the counter crop them, which is the order DiegeticStage
# draws in.
PATRONS = (('clubgirl', 150), ('shaved', 470))   # spanishsuit was cut 2026-08-25
# Where a customer's feet land when the bar is shut, and it is set from the WAIST rather
# than from the floor (the author: "musterilerin konumu dogru degil masanin arkasinda ve
# tezgah bel hizasinin cok az ustunde bitmeli"). The bar's top slab starts at y 240, and a
# customer has to be standing so that edge crosses them just above the belt - that is what
# reads as a person at a bar rather than a person on a stage behind one.
#
# The figures are ~200 px head to heel and an adult's waist sits about 106 px above the
# heel at that height, so feet at 352 put the waist at 246 and the slab six pixels above
# it. Their legs and feet are entirely behind the counter, which is the point: the earlier
# 250 left them standing in full view with the bar at their ankles.
PATRON_FEET_Y = 352

BOTTLES = ('v3_bourbon_ashfall_flat', 'v3_amaro_notte_flat', 'v3_bourbon_hollow_oak_flat',
           'v3_bourbon_old_harrow_flat')


def need(path, what):
    if not os.path.exists(path):
        sys.exit('MISSING %s\n  expected: %s' % (what, path))
    return Image.open(path).convert('RGBA')


def bottle_sprites():
    out = []
    for name in BOTTLES:
        p = os.path.join(ROOT, 'Assets', 'Resources', 'Items', name + '.png')
        if os.path.exists(p):
            out.append(Image.open(p).convert('RGBA'))
    return out


def shelve(frame, bottles, counter_y, dy):
    """Stand bottles on both shelf boards, three bays wide.

    The v3 bottles are authored ~145 px tall and a shelf bay is ~72, so they are scaled
    to the SHELF, not to a fixed number - a shelf that grows later should not need this
    line edited. Nearest-neighbour, because everything here is pixel art.
    """
    if not bottles:
        return
    i = 0
    for si, surface in enumerate(SHELF_SURFACES):
        # headroom for this shelf: distance up to the board above it (or the opening top)
        top = SHELF_TOP if si == 0 else SHELF_SURFACES[si - 1] + 14
        room = surface - top - 6
        for bx0, bx1 in BAYS:
            n = 3
            for k in range(n):
                b = bottles[i % len(bottles)]
                i += 1
                s = room / float(b.height)
                w, h = max(1, int(b.width * s)), max(1, int(b.height * s))
                sp = b.resize((w, h), Image.NEAREST)
                x = bx0 + int((bx1 - bx0) * (k + 0.5) / n) - w // 2
                frame.alpha_composite(sp, (x, counter_y + surface - h + dy))


def compose(bg, counter, shutter, bottles, counter_y, lift, opened):
    """One frame. `lift` moves EVERYTHING except the shutter, which goes the other way -
    that opposition is the whole feel of the mechanic."""
    f = Image.new('RGBA', (SCREEN_W, SCREEN_H), (0, 0, 0, 255))
    dy = -lift if opened else 0

    f.paste(bg, (0, dy))                                   # the room, never dimmed

    # NOTHING IS EXTENDED OR INVENTED. The first mock-up stretched the counter's last row
    # to plug the strip an over-large lift exposed; the author refused that outright, and
    # the lift below is now pinned to the overhang so the strip does not exist.

    for name, x in PATRONS:
        p = os.path.join(ROOT, 'Assets', 'Resources', 'Patron', name, 'idle', 'idle_00.png')
        if os.path.exists(p):
            sp = Image.open(p).convert('RGBA')
            # Feet on the floor behind the bar; the counter is drawn after and crops them.
            f.alpha_composite(sp, (x - sp.width // 2, PATRON_FEET_Y - 210 + dy))

    f.alpha_composite(counter, ((SCREEN_W - counter.width) // 2, counter_y + dy))
    if opened:
        shelve(f, bottles, counter_y, dy)

    sy = (SCREEN_H - PEEK) if opened else (counter_y + SHELF_TOP)
    f.alpha_composite(shutter, ((SCREEN_W - shutter.width) // 2, sy))
    return f.convert('RGB')


def main():
    bg = need(BACKGROUND, 'background').convert('RGBA')
    counter = need(COUNTER, 'counter (bar + backbar shelves)')
    shutter = need(SHUTTER, 'shutter (slatted door)')

    # CLOSED: the counter shows COUNTER_SHUT_H px and the rest hangs below. BODY_TOP is
    # the first drawn row of the sprite, so this puts that row COUNTER_SHUT_H above the
    # screen's bottom edge.
    body_top = counter.getbbox()[1]
    counter_y = SCREEN_H - COUNTER_SHUT_H - body_top
    # OPEN: lift by exactly what was hanging below, which lands the counter's bottom edge
    # on the screen's bottom edge. No gap to fill, nothing to draw that is not in the art.
    lift = (counter_y + counter.height) - SCREEN_H

    print('counter %dx%d  shutter %dx%d' % (counter.width, counter.height,
                                            shutter.width, shutter.height))
    print('shelf opening %d..%d (%d tall) == shutter height %d  -> %s'
          % (SHELF_TOP, SHELF_BOT, SHELF_BOT - SHELF_TOP, shutter.height,
             'exact' if SHELF_BOT - SHELF_TOP == shutter.height else 'MISMATCH'))
    print('closed: counter top at y=%d -> %d px of bar on screen, %d px hanging below'
          % (counter_y + counter.getbbox()[1], COUNTER_SHUT_H,
             counter.height - counter.getbbox()[1] - COUNTER_SHUT_H))
    print('open:   LIFT=%d px, shelf opening lands at y=%d..%d, counter bottom at y=%d'
          % (lift, counter_y + SHELF_TOP - lift, counter_y + SHELF_BOT - lift,
             counter_y + counter.height - lift))

    bottles = bottle_sprites()
    for opened in (False, True):
        img = compose(bg, counter, shutter, bottles, counter_y, lift, opened)
        name = 'drawer_%s.png' % ('open' if opened else 'closed')
        img.save(os.path.join(OUT, name))
        print('wrote', name)


if __name__ == '__main__':
    main()
