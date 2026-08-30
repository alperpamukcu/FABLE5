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
from sfx_dsp import (SR, LEVELS, bandpass, dc_block, env_ad, env_ar, highpass,  # noqa
                     impact, lowpass, modal, noise, normalize, place, render,
                     rng, silence, soft_limit, sweep, t, write)

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


def liquid(seconds, name, low=280.0, high=2600.0, bubbles=0.0, thickness=0.5):
    """Running liquid: band-limited noise whose loudness breathes, plus optional
    bubbles. The breathing is what separates a pour from a hiss — a real stream is
    never steady, it wobbles as it breaks up."""
    n = int(round(seconds * SR))
    x = t(seconds)
    core = noise(seconds, name + ':liq', 'pink')
    core = lowpass(highpass(core, low), high)
    # Two slow, non-harmonic wobbles so the breathing never falls into a rhythm.
    breathe = (1.0
               + 0.30 * np.sin(2 * np.pi * 3.1 * x)
               + 0.18 * np.sin(2 * np.pi * 7.7 * x + 1.1)
               + 0.10 * np.sin(2 * np.pi * 13.3 * x + 2.3))
    out = core * breathe * (0.6 + thickness * 0.5)
    if bubbles > 0:
        r = rng(name + ':bub')
        count = int(bubbles * seconds * 30)
        for _ in range(count):
            at = int(r.uniform(0.0, 0.96) * n)
            f = r.uniform(420.0, 1500.0)
            ln = int(r.uniform(0.010, 0.035) * SR)
            if at + ln >= n:
                continue
            xx = np.arange(ln) / SR
            # A bubble is a pitch that RISES as it collapses.
            ph = 2 * np.pi * np.cumsum(f * (1 + 5 * xx)) / SR
            place(out, at / SR, np.sin(ph) * np.exp(-xx / 0.008) * r.uniform(0.05, 0.16))
    return out


# ── the bank ────────────────────────────────────────────────────────────────
# Each entry returns a finished float array; `render` is called by BUILD.

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
    x = t(d)
    # The band travels down as the panel passes: near, then gone.
    out = np.zeros_like(n)
    for f0, f1, w in ((1800.0, 420.0, 1.0), (3200.0, 900.0, 0.5)):
        band = bandpass(n, (f0 + f1) * 0.5, 1.6)
        out += band * w
    return out * env_ar(d, 0.05, 0.16) * (0.5 + 0.5 * np.sin(np.pi * x / d))


def s_page_turn():
    """One sheet of a bar menu turning over."""
    d = 0.26
    return paper(d, 'page', bright=4800.0, bursts=6) * env_ar(d, 0.006, 0.10)


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
    """Someone settling onto a stool: cloth and a little wooden creak."""
    d = 0.30
    cloth = lowpass(noise(d, 'stool_c', 'pink'), 1300.0) * env_ar(d, 0.03, 0.14)
    creak = wood_body(0.18, 240.0, 'stool_w', amp=0.5, decay=0.040)
    out = cloth * 0.8
    at = int(0.09 * SR)
    place(out, at / SR, creak, env_ad(0.18, 0.004, 0.040))
    return out


def s_cellar_open():
    """The counter's cellar: a wooden door on a runner, sliding then stopping."""
    d = 0.46
    run = bandpass(noise(0.34, 'cell_r', 'pink'), 620.0, 1.4) * env_ar(0.34, 0.04, 0.10)
    out = silence(d)
    place(out, 0.0, run, 0.9)
    stop = wood_body(0.14, 130.0, 'cell_s', decay=0.045) * env_ad(0.14, 0.001, 0.045)
    at = int(0.31 * SR)
    place(out, at / SR, stop, 1.1)
    return lowpass(out, 3400.0)


def s_cellar_close():
    d = 0.40
    run = bandpass(noise(0.28, 'cellc_r', 'pink'), 560.0, 1.4) * env_ar(0.28, 0.03, 0.09)
    out = silence(d)
    place(out, 0.0, run, 0.85)
    stop = wood_body(0.16, 112.0, 'cellc_s', decay=0.055) * env_ad(0.16, 0.001, 0.055)
    at = int(0.25 * SR)
    place(out, at / SR, stop, 1.25)
    return lowpass(out, 3000.0)


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
    g = 1.0 + 0.45 * np.sin(2 * np.pi * 5.3 * t(d)) + 0.2 * np.sin(2 * np.pi * 11.9 * t(d))
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
    """HELD LOOP: ice in a metal tin. This is the loudest thing a bartender does, and
    it is ICE ON METAL — a rattle of hard knocks inside a ringing box."""
    d = 0.52
    n = int(round(d * SR))
    out = np.zeros(n)
    r = rng('shake:hits')
    # Knocks land in a loose rhythm around two shakes per loop.
    for _ in range(26):
        at = int(r.uniform(0.0, 0.97) * n)
        ln = int(0.030 * SR)
        if at + ln >= n:
            continue
        h = (impact(0.030, 'sh%d' % at, tone=r.uniform(1500, 3400), q=2.0, crack=0.0012)
             + metal_body(0.030, r.uniform(380, 560), 'shm%d' % at, amp=0.4, decay=0.020))
        place(out, at / SR, h * r.uniform(0.4, 1.0))
    # The tin's own body, driven by the rattle.
    out += bandpass(out, 620.0, 1.2) * 0.5
    return lowpass(out, 8500.0)


def s_stir_loop():
    """HELD LOOP: a bar spoon circling a mixing tin. Almost the opposite of a shake —
    a continuous ring with the spoon ticking round the wall, quiet and unhurried."""
    d = 0.85
    n = int(round(d * SR))
    x = t(d)
    # The spoon travelling: a soft band of noise moving in a circle.
    trav = bandpass(noise(d, 'stir_t', 'pink'), 1700.0, 2.0)
    trav = trav * (0.55 + 0.45 * np.sin(2 * np.pi * 2.35 * x))
    out = trav * 0.5
    # Ticks where the spoon meets the wall — twice a revolution.
    r = rng('stir:ticks')
    for k in range(5):
        at = int((k / 5.0 + 0.03) * n)
        ln = int(0.022 * SR)
        if at + ln >= n:
            continue
        tick = metal_body(0.022, r.uniform(900, 1400), 'st%d' % k,
                          amp=0.5, decay=0.014)
        place(out, at / SR, tick, 0.55)
    # The liquid turning with it.
    out += liquid(d, 'stir_liq', low=240.0, high=1500.0, thickness=0.35) * 0.35
    return lowpass(out, 7000.0)


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
    """A licence slid out of a wallet and laid on the counter — card on wood."""
    d = 0.26
    x = highpass(noise(d, 'id', 'pink'), 1600.0) * env_ar(d, 0.012, 0.10)
    x = lowpass(x, 6000.0) * 0.7
    tap = wood_body(0.10, 300.0, 'id_t', amp=0.6, decay=0.024)
    at = int(0.14 * SR)
    out = x.copy()
    place(out, at / SR, tap, env_ad(0.10, 0.0008, 0.024))
    return out


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
    """The bar bed: a low room tone, a distant murmur, and the faintest hum from the
    neon. It must be almost subliminal — it plays for the whole night, and anything
    with a feature in it becomes maddening by the third minute.

    Six seconds, and `render(loop=True)` crossfades the wrap, which is the fix for
    the old bed cracking audibly once every 5.75 seconds."""
    d = 6.0
    x = t(d)
    room = lowpass(noise(d, 'amb_room', 'pink'), 520.0) * 1.0
    murmur = bandpass(noise(d, 'amb_mur', 'pink'), 640.0, 0.8) * 0.30
    murmur = murmur * (0.7 + 0.3 * np.sin(2 * np.pi * 0.13 * x)
                       + 0.2 * np.sin(2 * np.pi * 0.31 * x + 0.9))
    # The neon's hum, at exact multiples of the loop length so it tiles perfectly.
    hum = (0.030 * np.sin(2 * np.pi * (100.0 // (1 / d) / d) * x)
           + 0.018 * np.sin(2 * np.pi * 120.0 * x + 0.4))
    return lowpass(room + murmur + hum, 2600.0)


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
    'stool_take':     (s_stool_take,    'light',   False, 1.0),
    'cellar_open':    (s_cellar_open,   'weight',  False, 1.0),
    'cellar_close':   (s_cellar_close,  'weight',  False, 1.0),
    'bottle_open':    (s_bottle_open,   'body',    False, 1.0),
    'bottle_set':     (s_bottle_set,    'body',    False, 1.0),
    'glass_down':     (s_glass_down,    'body',    False, 1.0),
    'serve_clink':    (s_serve_clink,   'moment',  False, 1.0),
    'ice_drop':       (s_ice_drop,      'light',   False, 1.0),
    'garnish':        (s_garnish,       'light',   False, 1.0),
    'grain_pinch':    (s_grain_pinch,   'light',   False, 1.0),
    'rim_turn':       (s_rim_turn,      'loop',    True,  1.0),
    'rim_done':       (s_rim_done,      'light',   False, 1.0),
    'pour_loop':      (s_pour_loop,     'loop',    True,  1.0),
    'tap_pull':       (s_tap_pull,      'loop',    True,  1.0),
    'shake_loop':     (s_shake_loop,    'loop',    True,  1.1),
    'stir_loop':      (s_stir_loop,     'loop',    True,  1.0),
    'cap_on':         (s_cap_on,        'body',    False, 1.0),
    'tin_tip':        (s_tin_tip,       'light',   False, 1.0),
    'tap_handle':     (s_tap_handle,    'light',   False, 1.0),
    'head_settle':    (s_head_settle,   'light',   False, 1.0),
    'drain':          (s_drain,         'body',    False, 1.0),
    'blowout':        (s_blowout,       'moment',  False, 1.0),
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
    'ambience_loop':  (s_ambience_loop, 'bed',     True,  1.0),
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
