# -*- coding: utf-8 -*-
"""LAST CALL's sound bank — every clip the bar makes, synthesised.

Run:  py -3 Tools/sfx_bank.py            build them all into Assets/Resources/Audio
      py -3 Tools/sfx_bank.py click cash build only those

WHY SYNTHESISED AND NOT DOWNLOADED. The brief allowed either. Freesound-grade packs
arrive at whatever level, rate and trim their uploader used, which is exactly the
fault this replaces — the old set was mismatched, dull and popping. Here every clip
leaves through one door (`sfx_dsp.render`) that force-fades, DC-blocks, soft-limits
and asserts the endpoints are zero, so the whole bank shares one mastering chain and
one deliberate level ladder. It also means no licences to track and no binary blobs
whose provenance is a dead URL.

HOW THINGS ARE MADE TO SOUND LIKE THINGS. Nothing here is a beep. Real objects are
told apart almost entirely by WHICH partials ring and how fast each one dies:

  glass   high, inharmonic, slow decay      (1 : 2.76 : 5.40 : 8.93, ~0.4s)
  wood    low-mid, very fast decay, noisy   (~0.06s, a lot of transient)
  metal   inharmonic and long               (rings past a second)
  paper   bright noise in short bursts, no pitch at all
  liquid  band-limited noise, amplitude-modulated, plus bubbles

The bar's voice is deliberately WARM: almost everything is low-passed under 8 kHz,
because a pixel-art bar at 2am is not a bright room, and unfiltered noise is the
'harsh' the brief forbids. Levels differ by design — see LEVELS in sfx_dsp.
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sfx_dsp import (SR, LEVELS, analog, bandpass, dc_block, env_ad, env_ar,  # noqa
                     highpass, impact, lowpass, modal, noise, normalize, place,
                     render, rng, silence, soft_limit, sweep, write)
from sfx_dsp import t as t_

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                   '..', 'Assets', 'Resources', 'Audio')
OUT = os.path.normpath(OUT)


# ── material voices ─────────────────────────────────────────────────────────

def glass_body(seconds, base, name, amp=1.0, decay=0.34):
    """Struck glass. The ratios are a tumbler's, not a wine glass's — tighter and
    shorter, or every clink in the bar sounds like a wedding toast."""
    return modal(seconds, [
        (base * 1.00, 1.00 * amp, decay),
        (base * 2.76, 0.55 * amp, decay * 0.62),
        (base * 5.40, 0.28 * amp, decay * 0.38),
        (base * 8.93, 0.12 * amp, decay * 0.22),
    ], name)


def wood_body(seconds, base, name, amp=1.0, decay=0.055):
    """Struck wood: low, fast, and mostly transient. A counter, a drawer, a shelf."""
    return modal(seconds, [
        (base * 1.00, 1.00 * amp, decay),
        (base * 1.87, 0.42 * amp, decay * 0.7),
        (base * 3.10, 0.18 * amp, decay * 0.5),
    ], name)


def metal_body(seconds, base, name, amp=1.0, decay=0.5):
    """Struck metal: inharmonic and it hangs about — a shaker tin, a cap, a spoon."""
    return modal(seconds, [
        (base * 1.00, 1.00 * amp, decay),
        (base * 2.31, 0.62 * amp, decay * 0.85),
        (base * 4.17, 0.40 * amp, decay * 0.6),
        (base * 6.71, 0.22 * amp, decay * 0.4),
        (base * 9.03, 0.10 * amp, decay * 0.3),
    ], name)


def paper(seconds, name, bright=5200.0, bursts=7):
    """Paper: no pitch whatsoever, just several bright crackles in quick succession."""
    n = int(round(seconds * SR))
    out = np.zeros(n)
    r = rng(name + ':paper')
    for i in range(bursts):
        at = int(r.uniform(0.02, 0.92) * n)
        ln = int(r.uniform(0.004, 0.020) * SR)
        if at + ln >= n:
            continue
        b = r.standard_normal(ln) * np.exp(-np.linspace(0, 6, ln))
        place(out, at / SR, b * r.uniform(0.4, 1.0))
    return lowpass(highpass(out, 900.0), bright)


def liquid(seconds, name, low=280.0, high=2600.0, bubbles=0.0, thickness=0.5,
           glug=0.0, vessel=0.0):
    """Running liquid.

    REBUILT 2026-08-27 (the author: "su dokme sesi ... gercekci degil"). The old one
    was band-limited noise with a slow wobble and a scattering of quiet bubbles, and
    it hissed. The mistake was treating the STREAM as the sound.

    It is not. A stream of water is nearly silent — what you hear when someone pours
    is AIR: pockets of it dragged under the surface and collapsing. Each collapse is
    a little resonator whose pitch RISES sharply as the bubble shrinks, and a crowd
    of those is the "glug" that makes water unmistakable. Get the bubbles wrong and
    no amount of stream noise will save it; get them right and the stream is almost
    a garnish.

    So `glug` is now the main parameter. Bubbles are LOUDER, far more numerous, and
    they cluster — real pouring gurgles in bursts as the neck of the bottle lets air
    back in, rather than fizzing evenly. `vessel` adds the container's own resonance
    to the bubbles, which is what makes the same water sound different in a glass, a
    tin and on the floor.
    """
    n = int(round(seconds * SR))
    x = t_(seconds)
    core = noise(seconds, name + ':liq', 'pink')
    core = lowpass(highpass(core, low), high)
    breathe = (1.0
               + 0.30 * np.sin(2 * np.pi * 3.1 * x)
               + 0.18 * np.sin(2 * np.pi * 7.7 * x + 1.1)
               + 0.10 * np.sin(2 * np.pi * 13.3 * x + 2.3))
    out = core * breathe * (0.6 + thickness * 0.5) * 0.55
    r = rng(name + ':bub')

    # THE GLUG. Bursts, not an even sprinkle: a bottle gurgles when air gets back in,
    # so the bubbles arrive in clumps with quieter gaps between them.
    if glug > 0:
        bursts = max(1, int(seconds * 7 * glug))
        for b in range(bursts):
            at0 = r.uniform(0.0, max(seconds - 0.10, 0.01))
            for _ in range(int(r.uniform(3, 8))):
                at = at0 + r.uniform(0.0, 0.075)
                if at >= seconds - 0.02:
                    continue
                f0 = r.uniform(180.0, 620.0)
                ln = int(r.uniform(0.018, 0.055) * SR)
                xx = np.arange(ln) / SR
                # Minnaert: the bubble shrinks, so its pitch climbs steeply.
                ph = 2 * np.pi * np.cumsum(f0 * (1.0 + 9.0 * xx)) / SR
                body = np.sin(ph) * np.exp(-xx / 0.014)
                if vessel > 0:
                    body = body + np.sin(ph * 1.6) * np.exp(-xx / 0.009) * vessel * 0.4
                place(out, at, body, r.uniform(0.35, 0.95) * glug)

    # The finer fizz that rides on top of any pour.
    if bubbles > 0:
        for _ in range(int(bubbles * seconds * 26)):
            at = r.uniform(0.0, max(seconds - 0.04, 0.01))
            f = r.uniform(700.0, 2200.0)
            ln = int(r.uniform(0.006, 0.020) * SR)
            xx = np.arange(ln) / SR
            ph = 2 * np.pi * np.cumsum(f * (1 + 7 * xx)) / SR
            place(out, at, np.sin(ph) * np.exp(-xx / 0.006), r.uniform(0.05, 0.18))
    return out

def s_click():
    """The UI tick. Everything in this game is a physical plate, so the tick is a
    small wooden tap and NOT a synthesised blip — 35ms, and it sits low in the ladder
    because it fires more than any other sound in the game."""
    d = 0.045
    x = impact(d, 'click', tone=2100.0, q=2.2, crack=0.0022) * 0.9
    x += wood_body(d, 320.0, 'click', amp=0.5, decay=0.020)
    return x * env_ad(d, 0.0004, 0.014)


def s_hover():
    """Barely a sound: the cursor warming a prop. If this is ever noticeable on its
    own, it is too loud — it exists to make the room feel touched, not to be heard."""
    d = 0.06
    x = lowpass(noise(d, 'hover', 'pink'), 1800.0)
    return x * env_ad(d, 0.010, 0.020, curve=1.4)


def s_key_press():
    """A key PLATE going down — chunkier than the tick, with a lip that lands."""
    d = 0.075
    x = impact(d, 'key', tone=1150.0, q=1.8, crack=0.0035)
    x += wood_body(d, 210.0, 'key', amp=0.7, decay=0.028)
    return x * env_ad(d, 0.0006, 0.022)


def s_deny():
    """Refused. A muted double thud, low and short — no buzzer. A buzz would be the
    'ear-hurting' the brief rules out, and this game never scolds the player."""
    d = 0.22
    a = wood_body(0.09, 150.0, 'deny_a', decay=0.030) * env_ad(0.09, 0.001, 0.030)
    b = wood_body(0.09, 128.0, 'deny_b', decay=0.034) * env_ad(0.09, 0.001, 0.034)
    out = silence(d)
    place(out, 0.0, a)
    at = int(0.085 * SR)
    place(out, at / SR, b, 0.8)
    return lowpass(out, 1400.0)


def s_whoosh():
    """A stage sliding past the camera. Filtered noise moving through a band —
    air, not a synth sweep."""
    d = 0.34
    n = noise(d, 'whoosh', 'pink')
    x = t_(d)
    # The band travels down as the panel passes: near, then gone.
    out = np.zeros_like(n)
    for f0, f1, w in ((1800.0, 420.0, 1.0), (3200.0, 900.0, 0.5)):
        band = bandpass(n, (f0 + f1) * 0.5, 1.6)
        out += band * w
    return out * env_ar(d, 0.05, 0.16) * (0.5 + 0.5 * np.sin(np.pi * x / d))


def s_page_turn():
    """A PAGE GOING OVER, WHICH IS A JOURNEY (2026-08-27, the author: "menu sayfa
    degistirme sesi kotu").

    The old one was six noise bursts scattered over a quarter-second — the texture of
    paper with none of the SHAPE of a page turning. A page does three things and the
    middle one is the part everybody recognises:

      1. the LIFT   a corner peeling off the sheet below it: a short rising rustle
      2. the ARC    the sheet travelling through the air. THIS is the sound people
                    mean by "page turn" — a broad whoosh whose brightness falls as
                    the paper slows, and it lasts a good fifth of a second
      3. the LAND   the sheet meeting the stack: a soft, dull slap with no ring

    Paper has no pitch anywhere in it, so all three are filtered noise — but they are
    filtered DIFFERENTLY and they follow each other, which is what makes it a page
    rather than a handful of crackle.
    """
    d = 0.46
    out = silence(d)
    # 1 · the corner peeling
    place(out, 0.0, highpass(noise(0.10, 'pg_lift', 'white'), 3000.0)
          * env_ar(0.10, 0.02, 0.05), 0.45)
    # 2 · the arc: a band of air sliding down as the sheet slows
    n = int(round(0.24 * SR))
    air = noise(0.24, 'pg_arc', 'pink')
    bright = bandpass(air, 2600.0, 1.0)
    dark = bandpass(air, 900.0, 1.0)
    k = np.linspace(0.0, 1.0, n) ** 0.8
    arc = bright * (1.0 - k) + dark * k
    # It is loudest in the middle of the swing, where the sheet is moving fastest.
    place(out, 0.055, arc * np.sin(np.pi * np.linspace(0, 1, n)) ** 0.8, 1.0)
    # 3 · landing on the stack — dull, and the only impact in it
    land = lowpass(noise(0.12, 'pg_land', 'white'), 2400.0)
    land = land + lowpass(noise(0.12, 'pg_land2', 'pink'), 700.0) * 0.8
    place(out, 0.28, land * env_ad(0.12, 0.0016, 0.026), 0.9)
    return lowpass(out, 8000.0)

def s_book_open():
    """The counter book opening: a wooden cover, then pages settling."""
    d = 0.42
    out = silence(d)
    cov = wood_body(0.14, 175.0, 'bookcov', decay=0.045) * env_ad(0.14, 0.001, 0.045)
    place(out, 0.0, cov)
    pg = paper(0.30, 'bookpg', bright=4200.0, bursts=9)
    at = int(0.06 * SR)
    place(out, at / SR, pg, 0.8)
    return lowpass(out, 6500.0)


def s_book_close():
    d = 0.32
    out = silence(d)
    pg = paper(0.16, 'bkcl_pg', bright=4000.0, bursts=5)
    place(out, 0.0, pg, 0.7)
    cov = wood_body(0.20, 145.0, 'bkcl', decay=0.055) * env_ad(0.20, 0.0008, 0.055)
    at = int(0.10 * SR)
    place(out, at / SR, cov)
    return lowpass(out, 5200.0)


def s_door():
    """The street door. A latch, the leaf swinging, and it meets the frame."""
    d = 0.55
    out = silence(d)
    latch = impact(0.05, 'door_l', tone=2600.0, q=4.0, crack=0.0018)
    place(out, 0.0, latch, 0.55)
    air = lowpass(noise(0.30, 'door_air', 'pink'), 900.0)
    at = int(0.04 * SR)
    place(out, at / SR, air, 0.35 * env_ar(0.30, 0.08, 0.15))
    thud = wood_body(0.26, 96.0, 'door_t', decay=0.075) * env_ad(0.26, 0.0012, 0.075)
    at = int(0.28 * SR)
    place(out, at / SR, thud, 1.2)
    return lowpass(out, 4200.0)


def s_stool_take():
    """SOMEONE PUTTING THEIR WEIGHT ON A BAR STOOL (2026-08-27, the author: "masaya
    musteri oturma sesi kotu").

    The old one was cloth and a single wooden ring, which is a person brushing past
    furniture. Sitting down is WEIGHT ARRIVING, and weight arriving on a frame does
    something a struck object never does: the creak BENDS. A joint under a growing
    load rises in pitch as it tightens, and that glide is the difference between a
    stool taking someone and a stick being tapped.

    Four parts, all overlapping rather than in a row, because a body lands as one
    movement: the cushion compressing (a soft low thump), the clothing settling, the
    frame's joints taking up under the load (the rising creak), and the footrest
    taking a shoe.
    """
    d = 0.70
    out = silence(d)
    # The seat compressing — low, soft, no ring at all.
    place(out, 0.0, lowpass(noise(0.20, 'sit_cush', 'pink'), 420.0)
          * env_ad(0.20, 0.006, 0.055), 1.0)
    place(out, 0.005, modal(0.22, [(96.0, 1.0, 0.045), (148.0, 0.4, 0.030)], 'sit_thump')
          * env_ad(0.22, 0.004, 0.045), 0.9)
    # Clothing.
    place(out, 0.02, bandpass(noise(0.30, 'sit_cloth', 'pink'), 2200.0, 1.1)
          * env_ar(0.30, 0.04, 0.16), 0.55)
    # THE CREAK THAT BENDS: a joint tightening under load climbs as it takes up.
    n = int(round(0.34 * SR))
    x = np.arange(n) / SR
    f = 300.0 + 190.0 * (x / 0.34) ** 0.7
    ph = 2 * np.pi * np.cumsum(f) / SR
    creak = np.zeros(n)
    for k in (1, 2, 3):
        creak += np.sin(ph * k) / (k * 1.6)
    # A real creak is a stick-slip judder, not a clean glide.
    creak *= 0.55 + 0.45 * np.sin(2 * np.pi * 34.0 * x)
    place(out, 0.10, lowpass(creak, 2600.0) * env_ar(0.34, 0.06, 0.18), 0.85)
    # A shoe finding the footrest.
    place(out, 0.30, (impact(0.12, 'sit_foot', tone=700.0, q=1.8, crack=0.0028)
                      + metal_body(0.12, 260.0, 'sit_rest', amp=0.5, decay=0.030))
          * env_ad(0.12, 0.0008, 0.028), 0.9)
    return lowpass(out, 5200.0)

def s_cellar_open():
    """THE CELLAR IS A ROLLER SHUTTER, NOT A DRAWER (2026-08-27, the author:
    "backbar kapaginin acilma sesi kisa ve kotu").

    They are right twice. It WAS short — 0.46s — and it was the wrong object: a band
    of noise and a wooden knock, which is a drawer sliding and stopping. What is
    actually over the cellar is a metal roller: dozens of slats running up a track,
    and the sound of that is RHYTHMIC. Each slat crossing the guide is its own small
    metallic tick, and the ticks come faster or slower as the shutter moves.

    Two things make it read as a real one. First the rate DECELERATES — a shutter
    thrown upward slows as it runs out of throw, so the ticks spread out toward the
    end, and a constant rate is the giveaway of a synthesised rattle. Second the
    whole curtain RINGS underneath: a sheet of linked metal has a body, so the ticks
    drive a resonance rather than sitting on silence. It ends with the shutter
    reaching its stop and the curtain ringing off.
    """
    d = 1.30
    out = silence(d)
    r = rng('roll:open')
    # The slats, decelerating. Time is walked forward by a gap that grows.
    at = 0.02
    gap = 0.028
    k = 0
    while at < 1.02:
        tick = impact(0.030, 'ro%d' % k, tone=r.uniform(1700, 3100), q=3.0, crack=0.0011)
        tick += metal_body(0.030, r.uniform(700, 1150), 'rom%d' % k, amp=0.35, decay=0.012)
        place(out, at, tick * env_ad(0.030, 0.0003, 0.010), r.uniform(0.9, 1.5))
        at += gap
        gap *= 1.055          # every slat takes a little longer than the last
        k += 1
    # The curtain itself: the ticks drive its body, so it is a sheet and not a list.
    out = out + bandpass(out, 520.0, 1.1) * 0.55
    # And it arrives at the top stop.
    place(out, 1.03, (impact(0.22, 'ro_stop', tone=820.0, q=1.5, crack=0.0026) * 1.2
                      + metal_body(0.22, 300.0, 'ro_stopm', amp=0.9, decay=0.085))
          * env_ad(0.22, 0.0006, 0.075), 0.75)
    return lowpass(out, 7000.0)

def s_cellar_close():
    """The same curtain coming down. Gravity is the difference: it ACCELERATES where
    the opening decelerates, so the ticks crowd together toward the end, and it lands
    harder because it arrives with the weight of the whole sheet behind it."""
    d = 1.15
    out = silence(d)
    r = rng('roll:close')
    at = 0.02
    gap = 0.058
    k = 0
    while at < 0.88:
        tick = impact(0.028, 'rc%d' % k, tone=r.uniform(1500, 2800), q=3.0, crack=0.0011)
        tick += metal_body(0.028, r.uniform(620, 1000), 'rcm%d' % k, amp=0.35, decay=0.012)
        place(out, at, tick * env_ad(0.028, 0.0003, 0.010), r.uniform(0.85, 1.45))
        at += gap
        gap *= 0.955          # falling: each slat arrives sooner than the last
        k += 1
    out = out + bandpass(out, 470.0, 1.1) * 0.55
    # It meets the sill with the sheet's whole weight on it.
    place(out, 0.90, (impact(0.26, 'rc_stop', tone=560.0, q=1.3, crack=0.0032) * 1.4
                      + metal_body(0.26, 220.0, 'rc_stopm', amp=1.0, decay=0.10)
                      + wood_body(0.26, 120.0, 'rc_sill', amp=0.7, decay=0.055))
          * env_ad(0.26, 0.0006, 0.090), 1.2)
    return lowpass(out, 5800.0)

def s_bottle_open():
    """A cap coming off: the crack of the seal, then the gas."""
    d = 0.30
    out = silence(d)
    pop = impact(0.03, 'bo_pop', tone=1750.0, q=1.4, crack=0.0016) * 1.4
    pop += modal(0.03, [(760.0, 0.7, 0.010)], 'bo_p2')
    place(out, 0.0, pop)
    hiss = highpass(noise(0.26, 'bo_hiss', 'pink'), 2400.0)
    at = int(0.022 * SR)
    place(out, at / SR, hiss, 0.30 * env_ad(0.26, 0.006, 0.075))
    return lowpass(out, 8000.0)


def s_bottle_set():
    """A bottle set down on the bench: glass base on wood."""
    d = 0.26
    x = impact(d, 'bset', tone=900.0, q=2.6, crack=0.0026)
    x += glass_body(d, 430.0, 'bset', amp=0.45, decay=0.12)
    x += wood_body(d, 165.0, 'bset_w', amp=0.7, decay=0.038)
    return lowpass(x * env_ad(d, 0.0006, 0.055), 6000.0)


def s_glass_down():
    """An empty glass meeting the counter."""
    d = 0.30
    x = impact(d, 'gd', tone=1500.0, q=3.0, crack=0.0020)
    x += glass_body(d, 620.0, 'gd', amp=0.75, decay=0.16)
    x += wood_body(d, 180.0, 'gd_w', amp=0.45, decay=0.030)
    return lowpass(x * env_ad(d, 0.0005, 0.070), 8500.0)


def s_serve_clink():
    """The made drink placed in front of a customer — the game's small reward, so it
    rings a touch longer and brighter than setting an empty glass down."""
    d = 0.55
    x = impact(d, 'sc', tone=2300.0, q=3.4, crack=0.0018) * 0.8
    x += glass_body(d, 780.0, 'sc', amp=1.0, decay=0.30)
    x += glass_body(d, 1170.0, 'sc2', amp=0.32, decay=0.22)
    return lowpass(x * env_ad(d, 0.0004, 0.16), 9500.0)


def s_ice_drop():
    """A cube into a glass: a knock, and then it settles against the wall."""
    d = 0.34
    out = silence(d)
    a = (impact(0.12, 'ice_a', tone=2900.0, q=2.0, crack=0.0016)
         + glass_body(0.12, 1250.0, 'ice_a', amp=0.5, decay=0.055))
    place(out, 0.0, a, env_ad(0.12, 0.0004, 0.030))
    b = (impact(0.14, 'ice_b', tone=2200.0, q=2.4, crack=0.0018)
         + glass_body(0.14, 980.0, 'ice_b', amp=0.35, decay=0.050))
    at = int(0.085 * SR)
    place(out, at / SR, b, 0.55 * env_ad(0.14, 0.0004, 0.032))
    return lowpass(out, 9000.0)


def s_garnish():
    """Something small and soft landing in a drink — a twist, an olive."""
    d = 0.16
    x = lowpass(noise(d, 'garn', 'pink'), 1500.0) * env_ad(d, 0.0016, 0.022)
    x += modal(d, [(480.0, 0.5, 0.020), (735.0, 0.25, 0.014)], 'garn') * env_ad(d, 0.001, 0.020)
    return x


def s_grain_pinch():
    """Salt or sugar between the fingers — dry, tiny, high."""
    d = 0.14
    x = highpass(noise(d, 'pinch', 'white'), 3200.0)
    return lowpass(x, 11000.0) * env_ad(d, 0.004, 0.030, curve=1.6)


def s_rim_turn():
    """HELD LOOP: the glass being turned through the salt dish. Dry grit, and it must
    tile — the player runs this for a whole second or two."""
    d = 0.90
    x = highpass(noise(d, 'rim', 'white'), 2600.0)
    x = lowpass(x, 9000.0)
    # A slow grind under the grit so it is a TURN and not a hiss.
    g = 1.0 + 0.45 * np.sin(2 * np.pi * 5.3 * t_(d)) + 0.2 * np.sin(2 * np.pi * 11.9 * t_(d))
    return x * g


def s_rim_done():
    """The lap closes: a small settle, the sound of a job finished."""
    d = 0.30
    x = glass_body(d, 690.0, 'rimd', amp=0.7, decay=0.16)
    x += impact(d, 'rimd', tone=1900.0, q=3.0, crack=0.0018) * 0.5
    return lowpass(x * env_ad(d, 0.0006, 0.085), 8000.0)


def s_pour_loop():
    """HELD LOOP: spirit running from a bottle into a tin. Thinner and higher than
    beer, with bubbles as it breaks on what is already in there."""
    d = 1.20
    return liquid(d, 'pour', low=340.0, high=3100.0, bubbles=1.0, thickness=0.55)


def s_tap_pull():
    """HELD LOOP: beer running from a font. Fuller and lower than a spirit pour, with
    the fizz riding on top."""
    d = 1.40
    x = liquid(d, 'tap', low=190.0, high=2200.0, bubbles=1.4, thickness=0.9)
    fizz = highpass(noise(d, 'tap_fizz', 'white'), 4200.0) * 0.16
    return x + lowpass(fizz, 11000.0)


def s_shake_loop():
    """HELD LOOP: A COCKTAIL SHAKER BEING WORKED (2026-08-27, the author: "shaker
    karistirma sesi kotu ... gercekci degil").

    The old one scattered twenty-six knocks at random across half a second, and
    random is exactly what a shake is not. A shake is a STROKE: the tin goes one way,
    the ice slams into the end, it comes back, the ice slams into the other end. Two
    impacts a cycle, evenly spaced, at about two and a half strokes a second — and
    the ear reads that rhythm as a person doing work. Scattered knocks read as a
    maraca.

    Three layers over that rhythm, and the liquid is the one that was missing
    entirely: ice hitting steel is the transient, the TIN rings between hits because
    it is a closed metal box, and the drink inside sloshes and crashes with the ice.
    Without the liquid it is a tin of stones.

    The loop is one full stroke — out and back — so it tiles at exactly the rhythm it
    was built on.
    """
    d = 0.40           # one out-and-back stroke, 2.5 per second
    n = int(round(d * SR))
    out = np.zeros(n)
    r = rng('shake:v2')
    # THE TWO SLAMS. Each is a cluster of cubes, not one cube — a shaker holds a
    # handful, and they arrive a few milliseconds apart.
    for slam, when in ((0, 0.03), (1, 0.23)):
        for c in range(5):
            jitter = r.uniform(0.0, 0.016)
            hit = impact(0.045, 'sh%d_%d' % (slam, c), tone=r.uniform(1900, 3600),
                         q=2.4, crack=0.0010)
            hit += modal(0.045, [(r.uniform(2400, 3400), 0.5, 0.012)], 'shi%d_%d' % (slam, c))
            place(out, when + jitter, hit * env_ad(0.045, 0.0003, 0.011),
                  r.uniform(0.5, 1.0) * (1.0 if slam == 0 else 0.85))
    # THE TIN. A closed steel box driven by the slams — this is what makes it a
    # shaker rather than ice on a table.
    out = out + bandpass(out, 560.0, 1.6) * 0.7 + bandpass(out, 1450.0, 2.4) * 0.35
    # THE DRINK. Liquid crashing end to end with the ice, in time with the strokes.
    x = t_(d)
    liq = liquid(d, 'sh_liq', low=180.0, high=2400.0, bubbles=1.2, thickness=0.9)
    swing = 0.45 + 0.55 * np.abs(np.sin(np.pi * 2.5 * x + 0.3))
    out = out + liq * swing * 0.55
    return lowpass(out, 8000.0)

def s_stir_loop():
    """HELD LOOP: A BAR SPOON CIRCLING A MIXING TIN.

    The opposite of the shake in every way, and it has to sound like it: stirring is
    the quiet, unhurried verb. What defines it is a single tone — the spoon's shaft
    riding the inside wall as it goes round — which RISES AND FALLS once a
    revolution, because the spoon is nearer the ear at the front of the circle than
    at the back. That doppler-ish sweep is the whole character, and the old one
    (a band of noise plus five ticks) did not have it.

    Under it: ice turning over slowly, and the drink moving with the spoon rather
    than crashing about. One revolution per loop, so the circle is continuous.
    """
    d = 0.62           # one revolution
    n = int(round(d * SR))
    x = t_(d)
    # The spoon riding the wall: a narrow band sweeping once round.
    src = noise(d, 'stir_ride', 'white')
    near = bandpass(src, 2600.0, 5.0)
    far = bandpass(src, 1200.0, 5.0)
    k = 0.5 - 0.5 * np.cos(2 * np.pi * x / d)          # 0 at the wrap, 1 mid-circle
    ride = near * k + far * (1.0 - k)
    out = ride * (0.45 + 0.55 * k) * 0.55
    # The metal it is riding on, answering faintly.
    out = out + bandpass(out, 900.0, 2.0) * 0.45
    # Ice turning over — a couple of soft knocks a revolution, not a rattle.
    r = rng('stir:ice')
    for i, when in enumerate((0.12, 0.47)):
        kn = impact(0.040, 'sti%d' % i, tone=r.uniform(1400, 2200), q=2.6, crack=0.0014)
        kn += modal(0.040, [(r.uniform(900, 1300), 0.4, 0.014)], 'stim%d' % i)
        place(out, when, kn * env_ad(0.040, 0.0004, 0.013), 0.35)
    # The drink turning with it — smooth, no bubbles, nothing breaking.
    out = out + liquid(d, 'stir_liq', low=220.0, high=1300.0, thickness=0.35) * 0.30
    return lowpass(out, 6500.0)

def s_cap_on():
    """The lid seating on the tin: metal meeting metal, then a short ring."""
    d = 0.34
    x = impact(d, 'cap', tone=1250.0, q=2.0, crack=0.0022)
    x += metal_body(d, 420.0, 'cap', amp=0.85, decay=0.14)
    return lowpass(x * env_ad(d, 0.0005, 0.075), 7500.0)


def s_tin_tip():
    """The tin tilting over the glass before the drink comes out."""
    d = 0.20
    x = bandpass(noise(d, 'tip', 'pink'), 900.0, 1.5) * env_ar(d, 0.02, 0.09)
    x += metal_body(d, 380.0, 'tip', amp=0.30, decay=0.055) * env_ad(d, 0.006, 0.055)
    return lowpass(x, 4500.0)


def s_tap_handle():
    """The font's handle moving on its pivot."""
    d = 0.18
    x = impact(d, 'th', tone=780.0, q=2.2, crack=0.0030)
    x += metal_body(d, 300.0, 'th', amp=0.5, decay=0.045)
    return lowpass(x * env_ad(d, 0.0008, 0.040), 4000.0)


def s_head_settle():
    """The foam settling on a pint — a fine, dry fizz that fades."""
    d = 0.70
    x = highpass(noise(d, 'head', 'white'), 3800.0)
    x = lowpass(x, 10500.0)
    return x * env_ad(d, 0.020, 0.22, curve=1.2)


def s_drain():
    """A drink going down the sink: liquid, then the hollow of the pipe."""
    d = 0.75
    x = liquid(d, 'drain', low=200.0, high=2400.0, bubbles=1.6, thickness=0.8)
    x = x * env_ar(d, 0.02, 0.30)
    hollow = modal(d, [(210.0, 0.35, 0.30), (318.0, 0.18, 0.22)], 'drain_h')
    return lowpass(x + hollow * 0.5, 5000.0)


def s_blowout():
    """The tin bursts (a fizzy drink shaken, GDD 21 §12). This was two borrowed clips
    played at once — a bottle cap and a disappointed customer — and it is neither: it
    is a lid leaving under pressure and the drink going everywhere. Loud, but it is
    still soft-limited like everything else, so it is a BURST and not a crack."""
    d = 0.90
    out = silence(d)
    # The seal letting go.
    crack = impact(0.05, 'blow_c', tone=1450.0, q=1.2, crack=0.0014) * 1.6
    place(out, 0.0, crack)
    # The lid, tumbling.
    place(out, 0.012, metal_body(0.55, 520.0, 'blow_l', amp=0.9, decay=0.20)
          * env_ad(0.55, 0.001, 0.16), 0.8)
    # The gas, and then the mess.
    place(out, 0.010, lowpass(highpass(noise(0.40, 'blow_g', 'white'), 1800.0), 9000.0)
          * env_ad(0.40, 0.004, 0.11), 0.55)
    place(out, 0.06, liquid(0.60, 'blow_w', low=220.0, high=2800.0, bubbles=2.2,
                            thickness=0.9) * env_ar(0.60, 0.03, 0.30), 0.7)
    return lowpass(out, 8500.0)


def s_coin():
    """A tip landing on the counter — one coin, ringing then settling."""
    d = 0.45
    x = metal_body(d, 1850.0, 'coin', amp=1.0, decay=0.20)
    x += metal_body(d, 2640.0, 'coin2', amp=0.45, decay=0.13)
    x += impact(d, 'coin', tone=3200.0, q=3.0, crack=0.0014) * 0.7
    return lowpass(x * env_ad(d, 0.0004, 0.13), 11000.0)


def s_cash():
    """The till. A mechanical clack, the bell, and the drawer running out."""
    d = 0.80
    out = silence(d)
    clack = (impact(0.06, 'cash_c', tone=1100.0, q=1.8, crack=0.0025)
             + wood_body(0.06, 220.0, 'cash_c', amp=0.6, decay=0.022))
    place(out, 0.0, clack, env_ad(0.06, 0.0006, 0.022))
    bell = metal_body(0.50, 1420.0, 'cash_b', amp=1.0, decay=0.24)
    at = int(0.035 * SR)
    place(out, at / SR, bell, env_ad(0.50, 0.0006, 0.16) * 0.9)
    slide = bandpass(noise(0.34, 'cash_s', 'pink'), 700.0, 1.3)
    at = int(0.10 * SR)
    place(out, at / SR, slide, 0.30 * env_ar(0.34, 0.04, 0.16))
    return lowpass(out, 9500.0)


def s_buy():
    """A purchase lands: two rising notes, warm, over in a moment. Not a fanfare."""
    d = 0.42
    out = silence(d)
    for k, (hz, at) in enumerate(((523.25, 0.0), (783.99, 0.085))):
        v = modal(0.30, [(hz, 1.0, 0.12), (hz * 2, 0.30, 0.07),
                         (hz * 3, 0.10, 0.045)], 'buy%d' % k)
        i = int(at * SR)
        place(out, i / SR, v, env_ad(0.30, 0.004, 0.10) * (1.0 - 0.25 * k))
    return lowpass(out, 7000.0)


def s_star_earn():
    """A star awarded. Bright, but rounded off hard at the top so it sparkles rather
    than stings — the brief's 'ear-hurting' lives in exactly this register."""
    d = 0.75
    out = silence(d)
    for k, hz in enumerate((880.0, 1318.5, 1760.0)):
        v = modal(0.55, [(hz, 1.0, 0.22), (hz * 2.01, 0.28, 0.13)], 'star%d' % k)
        i = int(k * 0.070 * SR)
        place(out, i / SR, v, env_ad(0.55, 0.006, 0.18) * (1.0 - 0.22 * k))
    return lowpass(out, 8000.0)


def s_cheer_sfx():
    """A customer pleased. NOT a crowd sample — a warm two-note lift with a little
    room behind it, so it reads as one person's approval at a quiet bar."""
    d = 0.70
    out = silence(d)
    for k, (hz, at) in enumerate(((392.0, 0.0), (587.33, 0.10), (783.99, 0.19))):
        v = modal(0.48, [(hz, 1.0, 0.20), (hz * 2, 0.34, 0.12),
                         (hz * 3, 0.12, 0.07)], 'cheer%d' % k)
        i = int(at * SR)
        place(out, i / SR, v, env_ad(0.48, 0.008, 0.16) * (1.0 - 0.18 * k))
    room = lowpass(noise(d, 'cheer_r', 'pink'), 1600.0) * 0.10 * env_ar(d, 0.05, 0.30)
    return lowpass(out + room, 6500.0)


def s_upset_sfx():
    """A customer unhappy. It falls instead of rising, and it is SOFT — the game does
    not punish with volume."""
    d = 0.60
    out = silence(d)
    for k, (hz, at) in enumerate(((349.23, 0.0), (277.18, 0.11))):
        v = modal(0.42, [(hz, 1.0, 0.20), (hz * 2, 0.22, 0.10)], 'ups%d' % k)
        i = int(at * SR)
        place(out, i / SR, v, env_ad(0.42, 0.010, 0.15) * (1.0 - 0.2 * k))
    return lowpass(out, 3200.0)


def s_patience_warn():
    """Someone is running out of patience: one soft tap on the counter. Deliberately
    NOT an alarm — the brief's whole objection is to sounds that hurt."""
    d = 0.22
    x = wood_body(d, 260.0, 'pat', amp=1.0, decay=0.045)
    x += impact(d, 'pat', tone=900.0, q=2.6, crack=0.0026) * 0.5
    return lowpass(x * env_ad(d, 0.0008, 0.048), 3600.0)


def s_id_card():
    """A LICENCE COMING OUT OF A WALLET (2026-08-27, the author: "kimlik gosterme
    sesi kotu").

    It was 0.26 seconds of filtered noise and a tap — a thin nothing for the single
    most important gesture in this game. A licence handed over is three distinct
    things and they happen in order, which is what the old one had none of:

      1. the WALLET   leather opening — soft, low, no pitch
      2. the SLIDE    the card drawn out against the leather: a friction sound that
                      RISES in pitch as the card clears, because less of it is still
                      gripped. That rise is the whole tell of something being drawn.
      3. the LAY      plastic landing flat on a wooden counter — bright, short, and
                      the only hard edge in the whole clip

    A card is thin plastic, so the landing rings high and dies almost instantly:
    nothing like the glass and metal elsewhere in the bank.
    """
    d = 0.62
    out = silence(d)
    # 1 · leather
    place(out, 0.0, lowpass(noise(0.16, 'id_leather', 'pink'), 900.0)
          * env_ar(0.16, 0.03, 0.09), 0.55)
    # 2 · the card drawn out, its friction climbing as it clears
    n = int(round(0.26 * SR))
    sl = noise(0.26, 'id_slide', 'white')
    lo = bandpass(sl, 1500.0, 1.5)
    hi = bandpass(sl, 3600.0, 1.5)
    climb = np.linspace(0.0, 1.0, n) ** 0.8
    slide = lo * (1.0 - climb) + hi * climb
    place(out, 0.09, slide * env_ar(0.26, 0.05, 0.10), 0.85)
    # 3 · thin plastic meeting wood
    lay = impact(0.14, 'id_lay', tone=2600.0, q=2.6, crack=0.0014) * 1.0
    lay += modal(0.14, [(1950.0, 0.6, 0.016), (3400.0, 0.25, 0.010)], 'id_lay')
    lay += wood_body(0.14, 240.0, 'id_wood', amp=0.45, decay=0.020)
    place(out, 0.36, lay * env_ad(0.14, 0.0005, 0.024), 1.0)
    return lowpass(out, 9000.0)

def s_bill_slip():
    """The night's slip: paper coming off a roll, and a stamp landing on it."""
    d = 0.65
    out = silence(d)
    roll = paper(0.42, 'slip', bright=5000.0, bursts=11)
    place(out, 0.0, roll, 0.85)
    stamp = (impact(0.10, 'slip_s', tone=700.0, q=2.0, crack=0.0030)
             + wood_body(0.10, 155.0, 'slip_s', amp=0.9, decay=0.030))
    at = int(0.46 * SR)
    place(out, at / SR, stamp, env_ad(0.10, 0.0008, 0.030) * 1.3)
    return lowpass(out, 7000.0)


def s_day_open():
    """The bar opening: the sign, and the room coming up underneath it."""
    d = 0.85
    out = silence(d)
    clunk = (impact(0.08, 'do_c', tone=820.0, q=1.8, crack=0.0032)
             + wood_body(0.08, 145.0, 'do_c', amp=0.8, decay=0.030))
    place(out, 0.0, clunk, env_ad(0.08, 0.0008, 0.030))
    for k, hz in enumerate((261.63, 392.0, 523.25)):
        v = modal(0.55, [(hz, 1.0, 0.24), (hz * 2, 0.26, 0.12)], 'do%d' % k)
        i = int((0.10 + k * 0.075) * SR)
        place(out, i / SR, v, env_ad(0.55, 0.010, 0.20) * 0.75)
    return lowpass(out, 6000.0)


def s_day_close():
    """Last call: the same shape as the open, walking downward."""
    d = 0.90
    out = silence(d)
    for k, hz in enumerate((523.25, 392.0, 261.63)):
        v = modal(0.62, [(hz, 1.0, 0.26), (hz * 2, 0.22, 0.13)], 'dc%d' % k)
        i = int(k * 0.095 * SR)
        place(out, i / SR, v, env_ad(0.62, 0.012, 0.22) * (0.85 - 0.1 * k))
    return lowpass(out, 4800.0)


def s_ambience_loop():
    """THE HUM IS GONE (2026-08-27, the author: "oyunda ugultu sesi var, bu gercekci
    ve iyi degil, bu kaldirilsin; oyunda arka planda ortama uygun alttan muzik
    calmali").

    They were right and the fault was mine: the old bed was room tone plus a murmur
    plus two sine waves at 100 and 120 Hz standing in for a neon transformer. A
    steady low sine IS a drone — it has no beginning, no movement and no reason, and
    over a whole night it stops being atmosphere and becomes tinnitus. Nothing
    justifies a hum in a game the player sits inside for twenty minutes at a time.

    What replaces it is MUSIC, not texture: a slow four-chord turn on the house
    polysynth, i - VI - III - VII in A minor, the progression this kind of room has
    used since 1984. Each chord is a full bar of eight seconds and the whole cycle is
    32 seconds, so nothing repeats inside a customer's visit. It sits at -26 dBFS,
    quieter than the old bed, because a bed you notice is a bed that is too loud —
    the test is whether you can hold a conversation over it.

    A little room tone stays UNDER the music (a bar is not a vacuum), but it is
    filtered to a whisper and carries no tone of its own.
    """
    d = 32.0
    x = t_(d)
    out = silence(d)
    # A minor: i, VI, III, VII — the four chords, one bar each.
    bars = [
        (220.00, 261.63, 329.63),   # Am
        (174.61, 220.00, 261.63),   # F
        (261.63, 329.63, 392.00),   # C
        (196.00, 246.94, 293.66),   # G
    ]
    bar = d / len(bars)
    for k, chord in enumerate(bars):
        # Each chord swells in and out so the turn breathes rather than steps.
        v = silence(bar * 1.6)
        for n, hz in enumerate(chord):
            tone = analog(bar * 1.6, hz, 'ambm%d_%d' % (k, n), voices=3,
                          detune=0.012, cut0=900.0, cut1=380.0, res=1.1)
            v = v + tone * (1.0 - 0.18 * n)
        v = v * env_ar(bar * 1.6, bar * 0.55, bar * 0.75)
        place(out, k * bar, v, 0.30)
        # A bass note under each chord, an octave and a half down — this is what makes
        # it a BED rather than a pad floating in the middle of the mix.
        root = analog(bar * 1.4, chord[0] * 0.5, 'ambb%d' % k, voices=2,
                      detune=0.008, cut0=320.0, cut1=150.0)
        place(out, k * bar, root * env_ar(bar * 1.4, bar * 0.4, bar * 0.6), 0.22)
    # The room itself, well under the music and with no tone in it.
    room = lowpass(noise(d, 'amb_room', 'pink'), 420.0)
    out = out + room * 0.16
    return lowpass(out, 2200.0)



# ── the rest of the bar (2026-08-27, second pass) ───────────────────────────
#
# The author asked for the whole game to speak, "oyunun temasini ve turunu goz
# onunde bulundurarak". So the split is deliberate and runs through everything
# below: THE BAR IS FOLEY, THE GAME IS SYNTH.
#
# Anything the bartender's hands touch — glass, wood, metal, paper, liquid — is
# modelled as the physical object it is, because the player is meant to believe they
# are behind a counter. Anything the SYSTEM says — a star, a level, a verdict, the
# night opening, the run ending — is a 1980s polysynth, because that is the game
# talking rather than the room, and because this bar is lit by neon in Miami. Two
# voices, never mixed up, so the player always knows whether the bar spoke or the
# game did.


def s_glass_pickup():
    """A glass lifted off the bar: the base breaking contact with wood, and the
    ring it was resting against dying away. Much softer than setting one DOWN —
    picking up is a release, not an impact."""
    d = 0.18
    x = impact(d, 'gpick', tone=1900.0, q=2.4, crack=0.0016) * 0.55
    x += glass_body(d, 700.0, 'gpick', amp=0.30, decay=0.070)
    return lowpass(x * env_ad(d, 0.0008, 0.038), 8000.0)


def s_beer_spill():
    """Beer going over the rim and onto the bar — wet, flat, and a little
    disappointing. No splash 'plink': this is loss, not an event."""
    d = 0.55
    x = liquid(d, 'spill', low=160.0, high=1900.0, bubbles=1.8, thickness=1.0)
    x = x * env_ar(d, 0.02, 0.26)
    return lowpass(x, 3600.0)


def s_pour_cutoff():
    """The stream stopping at the brim: the tail of running liquid, cut short and
    given the little knock a tap makes when it shuts."""
    d = 0.26
    out = silence(d)
    place(out, 0.0, liquid(0.16, 'cut', low=220.0, high=2400.0, thickness=0.7)
          * env_ad(0.16, 0.004, 0.045), 0.8)
    place(out, 0.10, metal_body(0.14, 640.0, 'cut_k', amp=0.6, decay=0.040)
          * env_ad(0.14, 0.0008, 0.040), 0.9)
    return lowpass(out, 5000.0)


# ── the game's own voice: the polysynth ─────────────────────────────────────

def _chord(seconds, notes, name, gain=1.0, spread=0.0, **kw):
    """Several analog voices, optionally arriving one after another."""
    out = silence(seconds)
    for i, hz in enumerate(notes):
        v = analog(seconds * 0.85, hz, name + str(i), **kw)
        v = v * env_ad(seconds * 0.85, 0.010, seconds * 0.30)
        place(out, i * spread, v, gain * (1.0 - 0.14 * i))
    return out


def s_verdict_good():
    """A GOOD PINT. A rising major third on the house synth — short, warm, and
    over before the player has finished being pleased with themselves."""
    return lowpass(_chord(0.55, [440.0, 554.37], 'vg', spread=0.055,
                          cut0=2600.0, cut1=700.0), 7000.0)


def s_verdict_bad():
    """TOO MUCH HEAD. The same voice, a semitone-flat pair — wrong rather than
    punishing. It is quieter than the good one by design: the game corrects, it
    does not scold."""
    return lowpass(_chord(0.50, [415.30, 493.88], 'vb', spread=0.050,
                          cut0=1500.0, cut1=420.0), 4200.0)


def s_verdict_flat():
    """A FLAT PINT — no head at all. One note, alone, going nowhere."""
    return lowpass(_chord(0.48, [349.23], 'vf', cut0=1300.0, cut1=380.0), 3600.0)


def s_another_round():
    """A perfect streak earns another round: the bank's brightest moment, four
    notes up an add9 and a soft neon shimmer behind them. The one place this game
    is allowed to be triumphant."""
    d = 1.20
    out = _chord(d, [523.25, 659.25, 783.99, 987.77], 'ar', spread=0.075,
                 cut0=3400.0, cut1=900.0, detune=0.013)
    shimmer = highpass(noise(d, 'ar_sh', 'pink'), 4000.0) * 0.10
    place(out, 0.05, lowpass(shimmer, 11000.0)[:int(0.9 * d * SR)]
          * env_ad(0.9 * d, 0.05, 0.35), 1.0)
    return lowpass(out, 9000.0)


def s_level_up():
    """A fixture bought and installed — the bar itself got better. Rising, with
    a low root under it so it lands as WEIGHT rather than as a chime."""
    d = 0.95
    out = _chord(d, [329.63, 493.88, 659.25], 'lu', spread=0.085,
                 cut0=2800.0, cut1=760.0)
    place(out, 0.0, analog(d * 0.8, 164.81, 'lu_root', voices=2, cut0=900.0,
                           cut1=300.0) * env_ad(d * 0.8, 0.012, d * 0.30), 0.55)
    return lowpass(out, 7500.0)


def s_bar_closed():
    """Going broke. A long minor fall with the filter shutting almost to nothing —
    the lights going off, not an alarm."""
    d = 1.60
    out = _chord(d, [220.0, 261.63, 311.13], 'bc', spread=0.16,
                 cut0=1400.0, cut1=240.0, detune=0.014)
    return lowpass(out, 2600.0)


def s_debt_alarm():
    """The bar goes under water. A slow two-note pulse, LOW and soft — the brief
    forbids sounds that hurt, and money trouble in this game is a mood, not a
    klaxon. It should worry the player without making them reach for the volume."""
    d = 1.10
    out = silence(d)
    for i, at in enumerate((0.0, 0.42)):
        v = analog(0.55, 138.59, 'da%d' % i, voices=2, cut0=700.0, cut1=220.0)
        place(out, at, v * env_ad(0.55, 0.030, 0.18), 1.0 - 0.2 * i)
    return lowpass(out, 1800.0)


def s_last_call_bell():
    """LAST CALL. A real bell over the bar — struck brass, not a synth: this is the
    one announcement the ROOM makes rather than the game, because in a bar it is a
    person ringing it."""
    d = 1.50
    x = metal_body(d, 1046.5, 'lcb', amp=1.0, decay=0.62)
    x += metal_body(d, 1567.0, 'lcb2', amp=0.35, decay=0.40)
    x += impact(d, 'lcb', tone=2800.0, q=3.0, crack=0.0016) * 0.6
    return lowpass(x * env_ad(d, 0.0006, 0.44), 9000.0)


def s_synth_swell():
    """The closing beat's pad: the ceiling coming down on the last customer. Slow
    in, slow out, and it never resolves — it just hangs there."""
    d = 2.40
    out = _chord(d, [174.61, 261.63, 349.23], 'sw', spread=0.20,
                 cut0=1200.0, cut1=380.0, detune=0.016, voices=4)
    return lowpass(out * env_ar(d, 0.55, 0.80), 3000.0)


def s_curtain():
    """The black between two nights: a soft downward breath, no pitch to speak of."""
    d = 0.85
    x = lowpass(noise(d, 'curt', 'pink'), 900.0)
    x = x * env_ar(d, 0.10, 0.45)
    x += analog(d, 110.0, 'curt_s', voices=2, cut0=520.0, cut1=180.0) * 0.35 \
        * env_ar(d, 0.15, 0.50)
    return lowpass(x, 1600.0)


def s_order_ready():
    """A customer closes the menu and knows what they want. Two soft notes — the
    game's most FREQUENT synth cue, so it is small and it never gets in the way."""
    return lowpass(_chord(0.34, [659.25, 880.0], 'ord', spread=0.045, gain=0.9,
                          cut0=2400.0, cut1=800.0), 7000.0)


def s_prompt_up():
    """A panel arriving in front of the player. A short filtered rise, not a note."""
    d = 0.28
    x = bandpass(noise(d, 'prompt', 'pink'), 1100.0, 1.4)
    return lowpass(x * env_ar(d, 0.06, 0.14), 4000.0)


# ── the rest of the room's foley ────────────────────────────────────────────

def s_stamp():
    """The van's stamp coming down on a listing: a rubber head, then the desk
    under it."""
    d = 0.28
    out = silence(d)
    place(out, 0.0, impact(0.06, 'stamp', tone=520.0, q=1.4, crack=0.0032), 1.3)
    place(out, 0.004, wood_body(0.20, 128.0, 'stamp_w', amp=1.0, decay=0.045)
          * env_ad(0.20, 0.0008, 0.045), 1.0)
    return lowpass(out, 2600.0)


def s_printer_feed():
    """HELD LOOP: the till's printer feeding the night's slip out. A small motor
    and paper being dragged over a bar — it runs for about two and a half seconds,
    so it must tile without a bump."""
    d = 0.55
    x = t_(d)
    motor = bandpass(noise(d, 'pf_m', 'pink'), 420.0, 2.2)
    # The stepper's own rate, at an exact multiple of the loop so it tiles.
    step = 0.5 + 0.5 * np.sin(2 * np.pi * (11.0 / d) * x)
    drag = highpass(noise(d, 'pf_d', 'white'), 2600.0) * 0.22
    return lowpass(motor * (0.6 + 0.6 * step) + drag, 6000.0)


# (rent_line was built and then cut, 2026-08-27. It wanted one strike per cost row
#  on the night's slip, and there is no such moment: RebuildDayEnd lays RENT, STOCK
#  and SHOP down in a single silent pass with no stagger and no per-row timer. Giving
#  it a home would mean BUILDING the stagger, which is a feature and not a sound pass.
#  The recipe stays here so it costs nothing to bring back if the slip ever prints
#  line by line; the wav does not, because art nothing loads is debt.)
def _s_rent_line_unused():
    d = 0.10
    x = impact(d, 'rl', tone=1600.0, q=2.6, crack=0.0018)
    x += paper(d, 'rl_p', bright=5000.0, bursts=2) * 0.6
    return lowpass(x * env_ad(d, 0.0006, 0.024), 6500.0)


def s_bowl_down():
    """A snack bowl set in front of someone: ceramic on wood, duller and heavier
    than a glass."""
    d = 0.26
    x = impact(d, 'bowl', tone=1050.0, q=2.2, crack=0.0026)
    x += modal(d, [(560.0, 0.7, 0.075), (1290.0, 0.30, 0.045),
                   (2010.0, 0.12, 0.028)], 'bowl')
    x += wood_body(d, 170.0, 'bowl_w', amp=0.5, decay=0.030)
    return lowpass(x * env_ad(d, 0.0006, 0.055), 6000.0)


def s_dish_down():
    """A prep dish put back on the rail — small, dry, and final."""
    d = 0.18
    x = impact(d, 'dish', tone=1350.0, q=2.6, crack=0.0022)
    x += modal(d, [(720.0, 0.5, 0.040), (1610.0, 0.2, 0.024)], 'dish')
    return lowpass(x * env_ad(d, 0.0006, 0.032), 7000.0)


def s_serve_it():
    """SERVE IT — the one press that ends the whole build, and it was silent. A
    key plate under a hand, and the counter answering it: bigger than any other
    press in the game, because it is the only one that finishes something."""
    d = 0.30
    out = silence(d)
    place(out, 0.0, impact(0.09, 'si', tone=980.0, q=1.8, crack=0.0034), 1.0)
    place(out, 0.0, wood_body(0.26, 175.0, 'si_w', amp=1.0, decay=0.055)
          * env_ad(0.26, 0.0008, 0.055), 1.0)
    place(out, 0.030, analog(0.22, 587.33, 'si_s', voices=2, cut0=2000.0, cut1=620.0)
          * env_ad(0.22, 0.006, 0.07), 0.35)
    return lowpass(out, 6000.0)


def s_tin_set_down():
    """The tin walked back to its place on the bench: steel meeting wood, with the
    body still ringing from the shake."""
    d = 0.40
    x = impact(d, 'tsd', tone=760.0, q=2.0, crack=0.0028)
    x += metal_body(d, 340.0, 'tsd', amp=0.8, decay=0.16)
    x += wood_body(d, 150.0, 'tsd_w', amp=0.6, decay=0.035)
    return lowpass(x * env_ad(d, 0.0006, 0.085), 5500.0)


def s_stir_commit():
    """The stir registering: the spoon's last turn against the tin's wall."""
    d = 0.28
    x = metal_body(d, 880.0, 'sc_m', amp=0.8, decay=0.11)
    x += liquid(d, 'sc_l', low=240.0, high=1400.0, thickness=0.4) * 0.35 \
        * env_ad(d, 0.010, 0.070)
    return lowpass(x * env_ad(d, 0.002, 0.075), 5500.0)


def s_id_card_away():
    """The licence going back across the counter — the same card, moving away."""
    d = 0.20
    x = highpass(noise(d, 'ida', 'pink'), 1500.0) * env_ar(d, 0.010, 0.10)
    return lowpass(x, 5200.0) * 0.8



# ── the third pass (2026-08-27): the pour, the stamp, and voices ────────────


def s_pour_glass():
    """HELD LOOP: spirit going into a GLASS.

    Glugging leads and the stream follows — see `liquid`, rebuilt 2026-08-27. On top
    of that the glass's own air column rings clear and bright around 700 Hz, because
    a tumbler is a hard open tube and it is the most resonant of the three vessels.
    The loop's PITCH rises with the fill at the call site: the column shortens as the
    drink goes in, which is the other half of what the author asked for.
    """
    d = 1.20
    x = liquid(d, 'pg', low=340.0, high=3200.0, bubbles=0.7, thickness=0.5,
               glug=1.15, vessel=0.9)
    body = bandpass(noise(d, 'pg_body', 'pink'), 700.0, 3.0) * 0.32
    # The column answers the bubbles, not just the hiss — that is the glassy ring.
    body = body + bandpass(x, 760.0, 4.5) * 0.45
    return lowpass(x + body, 7000.0)

def s_pour_tin():
    """HELD LOOP: the same spirit going into a METAL TIN.

    Steel is stiffer than glass and damps far faster, so its answer is lower, duller
    and shorter, with a metallic sheen instead of a clear tone. The tin's mouth is
    also narrower, so the stream breaks up less and the glugs are FEWER and deeper —
    a shaker fills with a low chug where a glass chatters.
    """
    d = 1.20
    x = liquid(d, 'pt', low=220.0, high=2000.0, bubbles=0.35, thickness=0.8,
               glug=0.75, vessel=0.4)
    body = bandpass(noise(d, 'pt_body', 'pink'), 430.0, 2.2) * 0.35
    body = body + bandpass(x, 380.0, 3.0) * 0.5
    sheen = bandpass(noise(d, 'pt_sheen', 'white'), 2900.0, 6.0) * 0.08
    return lowpass(x + body + sheen, 5200.0)

def s_pour_floor():
    """HELD LOOP: liquid missing everything and hitting the bar.

    NO VESSEL, SO NO RESONANCE AND ALMOST NO GLUG — the two things that make a pour
    sound like a pour both come from a container, and there is none. What is left is
    splatter: a broad wet hiss and a scatter of flat, pitchless ticks as drops break
    on a hard surface. It is meant to be the least satisfying sound in the game,
    because it is the sound of losing money.
    """
    d = 1.10
    x = liquid(d, 'pf', low=150.0, high=2600.0, bubbles=1.6, thickness=1.0, glug=0.20)
    n = int(round(d * SR))
    spat = np.zeros(n)
    r = rng('pf:spat')
    for _ in range(90):
        at = r.uniform(0.0, 0.96) * d
        ln = int(r.uniform(0.004, 0.016) * SR)
        b = r.standard_normal(ln) * np.exp(-np.linspace(0, 7, ln))
        place(spat, at, b, r.uniform(0.15, 0.55))
    return lowpass(x + highpass(spat, 700.0) * 0.6, 4200.0)

def s_stamp():
    """THE STAMP LANDS (2026-08-27, the author: "damga vurma sesi daha tatmin edici
    olmali ve damga tam vuruldugunda hissi vermeli").

    The old one was a rubber head and a desk under it, and it was over in 280ms with
    nothing to land ON. What makes a stamp satisfying is not loudness, it is the
    SEQUENCE — and there are four parts to it, in this order:

      1. the travel   a short breath of air as the head comes down
      2. the STRIKE   the ink pad meeting paper: the hard, brief transient
      3. the desk     the bench under it taking the blow, low and immediate
      4. the lift     a faint suction as the rubber peels off the sheet

    Part 4 is the one nobody thinks of and the one that makes it feel FINISHED: a
    stamp you never hear leave is a stamp still pressed to the page. The strike is
    also given real weight (-9 dBFS) because this is the night's verdict landing.
    """
    d = 0.55
    out = silence(d)
    # 1 · the travel down
    place(out, 0.0, lowpass(noise(0.06, 'st_air', 'pink'), 1400.0)
          * env_ar(0.06, 0.02, 0.03), 0.30)
    # 2 · the strike — rubber and ink on paper, wide and very short
    strike = impact(0.07, 'st_hit', tone=620.0, q=1.1, crack=0.0026) * 1.5
    strike += paper(0.07, 'st_pap', bright=6000.0, bursts=3) * 0.8
    place(out, 0.055, strike * env_ad(0.07, 0.0004, 0.020), 1.0)
    # 3 · the desk taking it
    place(out, 0.057, wood_body(0.28, 104.0, 'st_desk', amp=1.0, decay=0.070)
          * env_ad(0.28, 0.0008, 0.070), 1.15)
    # 4 · the lift: rubber peeling off paper
    place(out, 0.20, highpass(noise(0.16, 'st_peel', 'pink'), 1800.0)
          * env_ar(0.16, 0.03, 0.09), 0.30)
    return lowpass(out, 7500.0)


# ── voices ──────────────────────────────────────────────────────────────────
#
# The author asked whether a "sim language" is worth doing and what I think. My
# answer, in code: NOT full babble. Simlish is voice-acted and cannot be synthesised
# convincingly, and the cheap alternative — Animal Crossing's clipped chirping —
# would fight this game's whole register. A Miami bar at 2am whose mechanic is
# READING PEOPLE cannot have its customers chirp.
#
# So these are MURMURS, not speech: one to three formant-shaped syllables, low, warm
# and short, played only where a person actually says something (they place an order,
# they react to the drink). Formant synthesis is what makes them read as a voice
# rather than a beep — a pulse train through three resonances IS a vowel, and moving
# the resonances between syllables is what makes it sound like words rather than a
# held note.


def _voice(seconds, pitch, formants, name, breath=0.10):
    """One syllable: a pulse train through three resonances."""
    x = t_(seconds)
    # A glottal pulse train, slightly drifting — a perfectly steady voice is a synth.
    r = rng(name + ':v')
    f0 = pitch * (1.0 + 0.02 * np.sin(2 * np.pi * 4.5 * x + r.uniform(0, 6)))
    ph = 2 * np.pi * np.cumsum(f0) / SR
    src = np.zeros_like(x)
    for k in range(1, 14):
        src += np.sin(ph * k) / (k ** 1.1)
    src += noise(seconds, name + ':br', 'pink') * breath
    out = np.zeros_like(x)
    for hz, amp, q in formants:
        out += bandpass(src, hz, q) * amp
    return out


def _say(syllables, name, pitch):
    """A short utterance: a few syllables with a gap between them."""
    total = sum(s[0] for s in syllables) + 0.05 * len(syllables)
    out = silence(total + 0.10)
    at = 0.02
    # The vowel shapes, roughly: [a] [e] [o] [u] — three resonances each.
    VOWELS = {
        'a': [(730.0, 1.0, 7.0), (1090.0, 0.45, 9.0), (2440.0, 0.16, 11.0)],
        'e': [(530.0, 1.0, 7.0), (1840.0, 0.40, 9.0), (2480.0, 0.18, 11.0)],
        'o': [(570.0, 1.0, 6.0), (840.0, 0.40, 8.0), (2410.0, 0.10, 11.0)],
        'u': [(300.0, 1.0, 6.0), (870.0, 0.30, 8.0), (2240.0, 0.08, 11.0)],
    }
    for k, (dur, vowel, bend) in enumerate(syllables):
        v = _voice(dur, pitch * bend, VOWELS[vowel], '%s%d' % (name, k))
        v = v * env_ar(dur, dur * 0.22, dur * 0.42)
        place(out, at, v, 1.0 - 0.12 * k)
        at += dur + 0.05
    return lowpass(out, 3400.0)


def s_voice_order():
    """A customer saying what they want. Two syllables, level then falling — the
    shape of a statement, not a question."""
    return _say([(0.13, 'a', 1.0), (0.11, 'o', 0.88)], 'vo', 165.0)


def s_voice_happy():
    """Pleased. Two syllables RISING, and a little brighter."""
    return _say([(0.11, 'e', 1.0), (0.13, 'a', 1.18)], 'vh', 190.0)


def s_voice_upset():
    """Not pleased. One syllable, low, falling away."""
    return _say([(0.20, 'u', 1.0)], 'vu', 132.0)


def s_voice_greet():
    """Someone taking a stool. Short, low, barely a word."""
    return _say([(0.10, 'o', 1.0)], 'vg', 150.0)


def s_screen_on():
    """A screen coming up — the market tablet, the night's boards. A short filtered
    rise with a touch of the house synth under it, so the CHROME has a voice of its
    own distinct from the room's foley."""
    d = 0.30
    out = silence(d)
    place(out, 0.0, bandpass(noise(0.20, 'so_n', 'pink'), 1500.0, 1.6)
          * env_ar(0.20, 0.03, 0.12), 0.7)
    place(out, 0.02, analog(0.24, 440.0, 'so_s', voices=2, cut0=2400.0, cut1=900.0)
          * env_ad(0.24, 0.008, 0.075), 0.5)
    return lowpass(out, 6000.0)


def s_screen_off():
    """The same screen going away — the shape reversed, and darker."""
    d = 0.28
    out = silence(d)
    place(out, 0.0, analog(0.22, 330.0, 'sf_s', voices=2, cut0=1600.0, cut1=520.0)
          * env_ad(0.22, 0.006, 0.070), 0.5)
    place(out, 0.01, bandpass(noise(0.18, 'sf_n', 'pink'), 1000.0, 1.6)
          * env_ar(0.18, 0.02, 0.11), 0.6)
    return lowpass(out, 4000.0)


# name -> (builder, level, loop?, drive)
BANK = {
    'click':          (s_click,         'tick',    False, 1.0),
    'hover':          (s_hover,         'whisper', False, 1.0),
    'key_press':      (s_key_press,     'light',   False, 1.0),
    'deny':           (s_deny,          'light',   False, 1.0),
    'whoosh':         (s_whoosh,        'light',   False, 1.0),
    'page_turn':      (s_page_turn,     'light',   False, 1.0),
    'book_open':      (s_book_open,     'body',    False, 1.0),
    'book_close':     (s_book_close,    'body',    False, 1.0),
    'door':           (s_door,          'weight',  False, 1.0),
    'stool_take':     (s_stool_take,    'body',    False, 1.4),
    'cellar_open':    (s_cellar_open,   'weight',  False, 1.5),
    'cellar_close':   (s_cellar_close,  'weight',  False, 1.5),
    'bottle_open':    (s_bottle_open,   'body',    False, 1.0),
    'bottle_set':     (s_bottle_set,    'body',    False, 1.0),
    'glass_down':     (s_glass_down,    'body',    False, 1.0),
    'serve_clink':    (s_serve_clink,   'moment',  False, 1.0),
    'ice_drop':       (s_ice_drop,      'light',   False, 1.0),
    'garnish':        (s_garnish,       'light',   False, 1.0),
    'grain_pinch':    (s_grain_pinch,   'light',   False, 1.0),
    'rim_turn':       (s_rim_turn,      'loop',    True,  1.0),
    'rim_done':       (s_rim_done,      'light',   False, 1.0),
    'tap_pull':       (s_tap_pull,      'loop',    True,  1.0),
    'shake_loop':     (s_shake_loop,    'loop',    True,  1.1),
    'stir_loop':      (s_stir_loop,     'loop',    True,  1.0),
    'cap_on':         (s_cap_on,        'body',    False, 1.0),
    'tin_tip':        (s_tin_tip,       'light',   False, 1.0),
    'tap_handle':     (s_tap_handle,    'light',   False, 1.0),
    'head_settle':    (s_head_settle,   'light',   False, 1.0),
    'drain':          (s_drain,         'body',    False, 1.0),
    'blowout':        (s_blowout,       'moment',  False, 1.0),
    'pour_glass':     (s_pour_glass,    'loop',    True,  1.0),
    'pour_tin':       (s_pour_tin,      'loop',    True,  1.0),
    'pour_floor':     (s_pour_floor,    'loop',    True,  1.0),
    'voice_order':    (s_voice_order,   'light',   False, 1.0),
    'voice_happy':    (s_voice_happy,   'light',   False, 1.0),
    'voice_upset':    (s_voice_upset,   'light',   False, 1.0),
    'voice_greet':    (s_voice_greet,   'light',   False, 1.0),
    'screen_on':      (s_screen_on,     'light',   False, 1.0),
    'screen_off':     (s_screen_off,    'light',   False, 1.0),
    'glass_pickup':   (s_glass_pickup,  'light',   False, 1.0),
    'pour_cutoff':    (s_pour_cutoff,   'light',   False, 1.0),
    'verdict_good':   (s_verdict_good,  'body',    False, 1.0),
    'verdict_bad':    (s_verdict_bad,   'light',   False, 1.0),
    'verdict_flat':   (s_verdict_flat,  'light',   False, 1.0),
    'another_round':  (s_another_round, 'moment',  False, 1.0),
    'level_up':       (s_level_up,      'moment',  False, 1.0),
    'bar_closed':     (s_bar_closed,    'weight',  False, 1.0),
    'debt_alarm':     (s_debt_alarm,    'body',    False, 1.0),
    'last_call_bell': (s_last_call_bell,'moment',  False, 1.0),
    'synth_swell':    (s_synth_swell,   'bed',     False, 1.0),
    'curtain':        (s_curtain,       'light',   False, 1.0),
    'order_ready':    (s_order_ready,   'light',   False, 1.0),
    'stamp':          (s_stamp,         'weight',  False, 1.0),
    'printer_feed':   (s_printer_feed,  'loop',    True,  1.0),
    'bowl_down':      (s_bowl_down,     'body',    False, 1.0),
    'dish_down':      (s_dish_down,     'light',   False, 1.0),
    'serve_it':       (s_serve_it,      'weight',  False, 1.0),
    'tin_set_down':   (s_tin_set_down,  'body',    False, 1.0),
    'stir_commit':    (s_stir_commit,   'light',   False, 1.0),
    'id_card_away':   (s_id_card_away,  'light',   False, 1.0),
    'coin':           (s_coin,          'body',    False, 1.0),
    'cash':           (s_cash,          'moment',  False, 1.0),
    'buy':            (s_buy,           'body',    False, 1.0),
    'star_earn':      (s_star_earn,     'moment',  False, 1.0),
    'cheer_sfx':      (s_cheer_sfx,     'moment',  False, 1.0),
    'upset_sfx':      (s_upset_sfx,     'body',    False, 1.0),
    'patience_warn':  (s_patience_warn, 'light',   False, 1.0),
    'id_card':        (s_id_card,       'light',   False, 1.0),
    'bill_slip':      (s_bill_slip,     'body',    False, 1.0),
    'day_open':       (s_day_open,      'moment',  False, 1.0),
    'day_close':      (s_day_close,     'moment',  False, 1.0),
    'ambience_loop':  (s_ambience_loop, 'bed',     True,  1.0),   # music, not a hum
}


def build(names=None):
    names = names or sorted(BANK)
    rows = []
    for n in names:
        if n not in BANK:
            print('  ?? no such clip: %s' % n)
            continue
        fn, level, loop, drive = BANK[n]
        x = render(fn(), level=level, name=n, loop=loop, drive=drive)
        rows.append(write(os.path.join(OUT, n + '.wav'), x, n))
        r = rows[-1]
        print('  %-16s %5.2fs  peak %6.1f dB  rms %6.1f dB  %6d B'
              % (r['name'], r['seconds'], r['peak_db'], r['rms_db'], r['bytes']))
    return rows


if __name__ == '__main__':
    want = sys.argv[1:] or None
    print('building into %s' % OUT)
    build(want)
