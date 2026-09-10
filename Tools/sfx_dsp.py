# -*- coding: utf-8 -*-
"""The house's sound kitchen: a small DSP toolkit that CANNOT produce a pop.

The author's brief (2026-08-27): "profesyonel bir sonuc cikmali seslerde patlamalar
farkli yuksekliklerde sesler, kulak rahatsiz eden, bozuk sesler kesinlikle olmamali."

The thirteen clips that shipped before this were placeholder-grade and measured badly:
seven of them ended far from zero (click.wav stopped at 45% of full scale, which is a
hard pop on every single press), several carried DC offset, and all of them were
22 kHz — half a Nyquist, so everything sounded dull. ambience_loop cracked on every
loop wrap, once every 5.75 seconds, forever.

So this file is written so those faults are UNREPRESENTABLE rather than merely avoided:

  * `render` is the only way out, and it force-fades, DC-blocks, soft-limits and then
    ASSERTS that the first and last samples are exactly zero. A pop cannot be exported.
  * `loopify` crossfades a clip's tail into its head, so a loop's wrap is a crossfade
    and not a splice — there is no seam to click.
  * Every level is set by `LEVELS`, a deliberate ladder in dBFS, because "different
    loudnesses" is a design decision and not an accident of synthesis.
  * Nothing is a raw sine. Sines are what a placeholder sounds like; real objects ring
    with several partials and a noise transient, which is what `modal` and `impact` make.

No randomness reaches an exported file: every "noise" is drawn from a numpy Generator
seeded per-clip by name, so the same clip renders byte-identical on any machine, which
keeps the house determinism rule true of the audio pipeline as well.
"""
import hashlib
import io
import math
import os
import struct
import wave

import numpy as np

SR = 44100            # CD rate. The old set was 22050 and it showed.
BITS = 16


# ── the level ladder ────────────────────────────────────────────────────────
#
# Peak dBFS per category. This IS the "sounds at different heights" the brief asks
# for: a UI tick must sit far under a cash drawer, or every press fights the moment
# it is supposed to serve. Numbers are peaks, not RMS, because short transients read
# by their peak; the RMS spread comes out even wider, which is the point.
LEVELS = {
    'whisper': -30.0,   # hover, a prop warming under the cursor
    'tick':    -24.0,   # UI ticks, page corners, small keys
    'light':   -18.0,   # a garnish, a cube, paper
    'body':    -13.0,   # glass down, cap seating, a bottle set on wood
    'weight':   -9.0,   # a door, the cellar, a tin capped
    'moment':   -6.0,   # cash, a served drink, a star
    # THE BED, MEASURED IN PLAY (2026-08-27). -26 was a guess and it was wrong by
    # about eight decibels: the music reached the output at roughly -40 dBFS, which is
    # not subtle, it is inaudible — replacing a hum with silence is not what was asked
    # for. -19 puts it under the quietest regular effect (the UI tick at -24 peaks far
    # higher than the bed's average) while staying continuously present. It can sit
    # this much louder than the old drone precisely BECAUSE it is music: a steady sine
    # wears through at any level, a chord turning every eight seconds does not.
    'bed':     -19.0,
    'loop':    -16.0,   # held loops: pour, shake, stir, tap
}


def rng(name):
    """A generator seeded by clip NAME — same clip, same noise, every machine."""
    h = hashlib.sha256(name.encode('utf-8')).digest()
    return np.random.default_rng(int.from_bytes(h[:8], 'little'))


def t(seconds):
    return np.arange(int(round(seconds * SR)), dtype=np.float64) / SR


def silence(seconds):
    return np.zeros(int(round(seconds * SR)), dtype=np.float64)


# ── filters ─────────────────────────────────────────────────────────────────

def _biquad(x, b0, b1, b2, a1, a2):
    y = np.zeros_like(x)
    x1 = x2 = y1 = y2 = 0.0
    for i in range(x.size):
        xi = x[i]
        yi = b0 * xi + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        y[i] = yi
        x2, x1 = x1, xi
        y2, y1 = y1, yi
    return y


def lowpass(x, freq, q=0.707):
    """RBJ low-pass. The single most important tool here: nothing in a bar is bright,
    and unfiltered noise is exactly the 'harsh' the brief forbids."""
    w = 2 * math.pi * min(freq, SR * 0.45) / SR
    alpha = math.sin(w) / (2 * q)
    cw = math.cos(w)
    b0 = (1 - cw) / 2; b1 = 1 - cw; b2 = (1 - cw) / 2
    a0 = 1 + alpha; a1 = -2 * cw; a2 = 1 - alpha
    return _biquad(x, b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0)


def highpass(x, freq, q=0.707):
    w = 2 * math.pi * max(freq, 1.0) / SR
    alpha = math.sin(w) / (2 * q)
    cw = math.cos(w)
    b0 = (1 + cw) / 2; b1 = -(1 + cw); b2 = (1 + cw) / 2
    a0 = 1 + alpha; a1 = -2 * cw; a2 = 1 - alpha
    return _biquad(x, b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0)


def bandpass(x, freq, q=4.0):
    w = 2 * math.pi * min(freq, SR * 0.45) / SR
    alpha = math.sin(w) / (2 * q)
    cw = math.cos(w)
    b0 = alpha; b1 = 0.0; b2 = -alpha
    a0 = 1 + alpha; a1 = -2 * cw; a2 = 1 - alpha
    return _biquad(x, b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0)


# ── sources ─────────────────────────────────────────────────────────────────

# ── shelves and bells (RBJ cookbook), for the voicing chain ─────────────────

def _shelf(x, freq, gain_db, kind, slope=1.0):
    a = 10.0 ** (gain_db / 40.0)
    w = 2 * math.pi * freq / SR
    cs, sn = math.cos(w), math.sin(w)
    al = sn / 2.0 * math.sqrt((a + 1.0 / a) * (1.0 / slope - 1.0) + 2.0)
    tsa = 2.0 * math.sqrt(a) * al
    if kind == 'low':
        b0 = a * ((a + 1) - (a - 1) * cs + tsa)
        b1 = 2 * a * ((a - 1) - (a + 1) * cs)
        b2 = a * ((a + 1) - (a - 1) * cs - tsa)
        a0 = (a + 1) + (a - 1) * cs + tsa
        a1 = -2 * ((a - 1) + (a + 1) * cs)
        a2 = (a + 1) + (a - 1) * cs - tsa
    else:
        b0 = a * ((a + 1) + (a - 1) * cs + tsa)
        b1 = -2 * a * ((a - 1) + (a + 1) * cs)
        b2 = a * ((a + 1) + (a - 1) * cs - tsa)
        a0 = (a + 1) - (a - 1) * cs + tsa
        a1 = 2 * ((a - 1) - (a + 1) * cs)
        a2 = (a + 1) - (a - 1) * cs - tsa
    return _biquad(x, b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0)


def lowshelf(x, freq, gain_db):
    return _shelf(x, freq, gain_db, 'low')


def highshelf(x, freq, gain_db):
    return _shelf(x, freq, gain_db, 'high')


def peaking(x, freq, gain_db, q=1.0):
    a = 10.0 ** (gain_db / 40.0)
    w = 2 * math.pi * freq / SR
    al = math.sin(w) / (2 * q)
    cs = math.cos(w)
    b0, b1, b2 = 1 + al * a, -2 * cs, 1 - al * a
    a0, a1, a2 = 1 + al / a, -2 * cs, 1 - al / a
    return _biquad(x, b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0)


# ── THE ROOM (2026-09-10) ───────────────────────────────────────────────────
#
# The author heard the first bank and did not like it, and the measurement said why
# more clearly than the ear could: median spectral centroid 1626 Hz, 85% rolloff at
# 2832 Hz — warm enough — but every single clip bone DRY. That is the tell. Real foley
# is recorded in a place, and a synthesised hit with no reflections behind it reads as
# a synthesiser however good its partials are. Nothing else in the bank changes what
# it sounds like as much as putting the bar around it.
#
# Built as a synthetic impulse response and convolved, rather than as a comb network:
# an IR is one FFT per clip instead of a per-sample recursion in Python (the ambience
# bed alone is 1.4M samples), and it lets the early reflections be PLACED rather than
# fall out of whatever delay lengths were chosen.

def room_ir(rt60=0.42, size=1.0, name='room', bright=3200.0):
    """A small warm room: a few placed early reflections, then a damped noise tail.

    THE IR CARRIES NO DIRECT SOUND. Putting the dry impulse at sample 0 and then mixing
    the result against the dry signal was the first attempt, and measuring it showed why
    that is wrong: the wet path is then MOSTLY the dry clip again, so `wet` scales the
    reflections and the direct sound together and a 14% mix moves almost nothing (median
    tail energy 0.159 -> 0.150, which is no room at all). Reflections only, mixed on top
    of the dry, is a send — and then `wet` means what it says."""
    n = int(round(min(1.6, rt60 * 1.7) * SR))
    ir = np.zeros(n)
    r = rng('ir:' + name)
    # Early reflections — the walls of a room this size, alternating in sign because a
    # reflection off a hard surface inverts. Spaced unevenly on purpose: an even spacing
    # is a comb filter, which is heard as a pitch and not as a room.
    for ms, amp in ((7.0, 0.52), (11.3, -0.40), (17.1, 0.33), (23.8, -0.26),
                    (31.5, 0.20), (41.2, -0.15), (52.9, 0.11)):
        i = int(round(ms * size * SR / 1000.0))
        if i < n:
            ir[i] += amp * (1.0 + 0.06 * r.standard_normal())
    # The tail: noise under an exponential, -60 dB at rt60.
    x = np.arange(n) / float(SR)
    tail = r.standard_normal(n) * np.exp(-6.908 * x / max(rt60, 0.05))
    tail[:int(0.006 * SR)] = 0.0               # predelay: no tail before first contact
    ir = ir + 0.34 * tail
    # A room absorbs highs faster than lows, and a bar full of wood and bodies more so.
    ir = lowpass(ir, bright, 0.707)
    ir = highpass(ir, 130.0, 0.707)            # keep the tail out of the low end
    return ir / max(float(np.max(np.abs(ir))), 1e-9)


_IR_CACHE = {}


def room(x, wet=0.14, rt60=0.42, size=1.0, name='room', tail=True):
    """Put the bar behind a dry clip. `wet` is a mix, not a send: 0 is unchanged.

    `tail` LENGTHENS the clip by the room's own decay. Without it the convolution is
    cut at the dry clip's last sample and the export fade then forces that cut to zero
    — which is a room with its reverb chopped off, i.e. a worse sound than no room at
    all. A LOOP keeps its length instead: its tail belongs at its own head, and that is
    `loopify`'s job.
    """
    if wet <= 0.0:
        return x
    key = (round(rt60, 3), round(size, 3), name)
    ir = _IR_CACHE.get(key)
    if ir is None:
        ir = room_ir(rt60, size, name)
        _IR_CACHE[key] = ir
    n = x.size + ir.size - 1
    m = 1 << int(math.ceil(math.log2(n)))
    wetsig = np.fft.irfft(np.fft.rfft(x, m) * np.fft.rfft(ir, m), m)[:n]
    dry = np.pad(x, (0, n - x.size)) if tail else x
    if not tail:
        wetsig = wetsig[:x.size]
    p = float(np.max(np.abs(wetsig)))
    if p > 1e-9:
        wetsig = wetsig * (float(np.max(np.abs(x))) / p)
    # A SEND, not a crossfade: the dry stays whole and the reflections sit on top of it,
    # `wet` decibels down. Crossfading dry against wet takes the object away as it adds
    # the room, which is why the first pass sounded no wetter — it was losing as much
    # direct sound as it gained reflection.
    return dry + wet * wetsig


def trim_tail(x, floor_db=-56.0, keep=0.04):
    """Cut a clip where its own tail has fallen out of hearing.

    A room adds half a second to everything it touches, and most of that half second is
    below anything a bar at 2am will ever let through. Trimming at the point the tail
    passes -46 dB keeps a glass short and lets a door stay long, WITHOUT either being a
    number somebody typed in."""
    p = float(np.max(np.abs(x)))
    if p <= 1e-9:
        return x
    over = np.flatnonzero(np.abs(x) > p * (10.0 ** (floor_db / 20.0)))
    if over.size == 0:
        return x
    end = min(x.size, int(over[-1]) + int(keep * SR))
    return x[:max(end, int(0.02 * SR))]


# ── THE VOICE OF THE HOUSE ──────────────────────────────────────────────────

def cozy(x, air=-3.5, harsh=-2.2, warmth=2.0):
    """The tone every clip wears, in one place (2026-09-10, the author: "cozy seslere
    yakın ses efektleri olsun").

    Cozy is a specific balance and not simply "dull" — a clip with nothing above 3 kHz
    does not sound warm, it sounds like a bad sample. Three moves, in this order:

      * 45 Hz highpass. Nothing in a bar is subsonic, and rumble only eats headroom
        the limiter then gives away.
      * A dip at 3 kHz. This is the band the ear is most sensitive to and where cheap
        digital sound lives; taking a couple of decibels out of it is most of what
        separates "warm" from "harsh" while leaving the detail above it alone.
      * A low shelf up at 320 Hz and a high shelf gently down at 6.5 kHz. Body added,
        glare removed, and the air between them kept.
    """
    x = highpass(x, 45.0, 0.707)
    x = peaking(x, 3000.0, harsh, 1.1)
    x = lowshelf(x, 320.0, warmth)
    x = highshelf(x, 6500.0, air)
    return x


def noise(seconds, name, kind='white'):
    n = rng(name).standard_normal(int(round(seconds * SR)))
    if kind == 'white':
        return n
    # Pink-ish: a cascade of one-poles. Warmer, and what real rooms and liquids are.
    out = np.zeros_like(n)
    b = [0.0, 0.0, 0.0]
    for i in range(n.size):
        b[0] = 0.99765 * b[0] + n[i] * 0.0990460
        b[1] = 0.96300 * b[1] + n[i] * 0.2965164
        b[2] = 0.57000 * b[2] + n[i] * 1.0526913
        out[i] = b[0] + b[1] + b[2] + n[i] * 0.1848
    return out / 4.0


def modal(seconds, partials, name, damp_spread=1.0):
    """A struck object: several decaying sinusoids at once.

    `partials` is a list of (hz, amplitude, decay_seconds). Real glass, wood and metal
    differ almost entirely in WHICH ratios ring and how fast each dies — a single sine
    is a beep, three well-chosen partials is a thing you can name with your eyes shut.

    TWO THINGS THIS GOT WRONG UNTIL 2026-09-10, and between them they were most of why
    the bank read as a synthesiser:

      * every partial started at FULL amplitude on sample zero. A struck object does
        not: the contact takes a moment to hand its energy over, and it hands the high
        partials over faster than the low ones. Starting them all at once is a click
        with a chord behind it, which is precisely the sound of an envelope generator.
      * every partial decayed at the rate it was given. Real objects lose their highs
        FIRST — that is why a glass goes from bright to hollow as it rings, and why a
        set of equal decays sounds like a bell rather than like a thing.

    Both are one line each and neither is a matter of taste; they are what the physics
    does. The partial table each caller passes is unchanged.
    """
    x = t(seconds)
    out = np.zeros_like(x)
    r = rng(name + ':modal')
    for hz, amp, dec in partials:
        # A hair of detune per partial so a struck object is never a perfect chord.
        f = hz * (1.0 + 0.0013 * r.standard_normal())
        phase = r.uniform(0, 2 * math.pi)
        # Highs die first: a partial an octave up loses roughly a sixth of its life.
        dec_f = dec * damp_spread / (1.0 + hz / 5200.0)
        # And each one takes a moment to come up — faster the higher it sits, which is
        # the contact handing its energy over rather than an envelope opening.
        atk = max(0.00025, 0.0016 / (1.0 + hz / 900.0))
        env = (1.0 - np.exp(-x / atk)) * np.exp(-x / max(dec_f, 1e-4))
        out += amp * np.sin(2 * math.pi * f * x + phase) * env
    return out


def impact(seconds, name, tone=1400.0, q=3.0, crack=0.004, body=0.9):
    """A hit: a very short noise transient through a resonant band, plus a body.

    This is the shape of nearly every physical sound in a bar — something meets
    something, air moves sharply, and the object rings a little.

    A hit is not one band of noise. Real contact is BROADBAND — two surfaces scuffing
    is most of what the ear uses to name the materials — and the resonant band on its
    own is a filtered click, which is what this made until 2026-09-10. So there are two
    layers now: the scuff, low-passed to the material's weight and dying almost at once,
    and the resonant crack over it.
    """
    n = noise(seconds, name + ':imp')
    x = t(seconds)
    env = np.exp(-x / max(crack, 1e-4))
    trans = bandpass(n * env, tone, q)
    # The scuff: shorter than the crack, and only as bright as the object is light.
    scuff = lowpass(noise(seconds, name + ':scuff') * np.exp(-x / max(crack * 0.55, 1e-4)),
                    min(9000.0, tone * 2.2), 0.707)
    return (trans + 0.42 * scuff) * body


def analog(seconds, hz, name, detune=0.010, voices=3, shape='saw',
           cut0=2200.0, cut1=520.0, res=1.6, drift=0.0018):
    """AN ANALOG-STYLE VOICE — the game's period, in one function (2026-08-27).

    LAST CALL is a 1980s Miami bar, and the era's reward and progression sounds are
    not chiptune: chiptune is a square wave from a console's chip, while this room's
    sound is a polysynth — several oscillators at slightly different pitches, run
    through a resonant low-pass that CLOSES as the note decays. Three things make it
    read as that rather than as a beep:

      * DETUNE. Several voices a few cents apart beat against each other, which is
        the whole warmth of the era. One oscillator is a test tone.
      * A MOVING FILTER. Brightness falling over the note is what a real synth's
        envelope does to a real filter, and it is why the sound has a shape instead
        of just a volume.
      * DRIFT. A slow, tiny wobble on the pitch — analog oscillators never sit still,
        and a perfectly stable pitch is the one thing that always sounds digital.

    The filter is applied in two bands and mixed rather than swept per-sample: a true
    per-sample sweep needs a time-varying biquad, and at these lengths the difference
    is inaudible while the cost is not.
    """
    x = t(seconds)
    r = rng(name + ':analog')
    out = np.zeros_like(x)
    for v in range(voices):
        # Spread the voices either side of centre, and let each drift on its own.
        cents = (v - (voices - 1) / 2.0) * detune
        wob = drift * np.sin(2 * math.pi * (0.7 + 0.31 * v) * x + r.uniform(0, 6.28))
        f = hz * (1.0 + cents + wob)
        ph = 2 * math.pi * np.cumsum(f) / SR + r.uniform(0, 6.28)
        if shape == 'saw':
            # A band-limited-ish saw: a short harmonic sum, so nothing aliases into
            # the harshness the brief forbids.
            v_out = np.zeros_like(x)
            for k in range(1, 9):
                v_out += np.sin(ph * k) / k
            v_out *= 0.6
        elif shape == 'square':
            v_out = np.zeros_like(x)
            for k in (1, 3, 5, 7, 9):
                v_out += np.sin(ph * k) / k
            v_out *= 0.75
        else:
            v_out = np.sin(ph)
        out += v_out
    out /= max(voices, 1)
    # The filter envelope, as a crossfade from open to closed.
    bright = lowpass(out, cut0, res)
    dark = lowpass(out, cut1, res)
    k = np.linspace(0.0, 1.0, x.size) ** 0.7
    return bright * (1.0 - k) + dark * k


def sweep(seconds, f0, f1, name, curve=1.0):
    """A pitch glide — air, a whoosh, a drawer sliding."""
    x = t(seconds)
    k = (x / max(x[-1], 1e-9)) ** curve
    f = f0 + (f1 - f0) * k
    phase = 2 * math.pi * np.cumsum(f) / SR
    return np.sin(phase)


# ── envelopes ───────────────────────────────────────────────────────────────

def env_ad(seconds, attack, decay, curve=2.0):
    """Attack-decay. Always reaches exactly zero at the end."""
    n = int(round(seconds * SR))
    x = np.arange(n, dtype=np.float64) / SR
    a = int(round(attack * SR))
    e = np.zeros(n)
    if a > 0:
        e[:a] = np.linspace(0.0, 1.0, a)
    e[a:] = np.exp(-(x[a:] - x[a] if a < n else 0) / max(decay, 1e-4)) ** curve
    return e


def env_ar(seconds, attack, release):
    """A plateau with fades either side — for loops and beds."""
    n = int(round(seconds * SR))
    e = np.ones(n)
    a = max(1, int(round(attack * SR)))
    r = max(1, int(round(release * SR)))
    e[:a] *= np.linspace(0.0, 1.0, a)
    e[-r:] *= np.linspace(1.0, 0.0, r)
    return e


# ── shaping and safety ──────────────────────────────────────────────────────

def place(out, at, payload, gain=1.0):
    """Mix `payload` into `out` starting at `at` SECONDS, CLIPPED to fit.

    Nearly every sound here is "this, and then that a moment later", and doing that
    with raw slice arithmetic gives each clip its own chance to be one sample too
    long for the buffer. cellar_close was exactly that: a 0.16s knock placed 0.25s
    into a 0.40s clip. The arithmetic is done once, here, and a payload that runs
    past the end is truncated instead of raising.
    """
    i = int(round(at * SR))
    if i >= out.size or payload.size == 0:
        return out
    n = min(payload.size, out.size - i)
    out[i:i + n] += payload[:n] * gain
    return out


def dc_block(x):
    """Remove DC. Several of the old clips carried an offset, which wastes headroom
    and turns the very first sample into a step — that is a click before the sound."""
    if x.size == 0:
        return x
    return highpass(x - float(np.mean(x)), 22.0)


def soft_limit(x, drive=1.0):
    """tanh saturation instead of a hard ceiling.

    Hard clipping is what the brief calls 'patlama' — it snaps the waveform flat and
    generates a spray of odd harmonics. tanh bends the peaks instead, so a loud sound
    gets denser rather than broken, and the output can never exceed 1.0.
    """
    return np.tanh(x * drive)


def normalize(x, db):
    """Scale so the PEAK sits at `db` dBFS."""
    p = float(np.max(np.abs(x))) if x.size else 0.0
    if p < 1e-12:
        return x
    return x * ((10.0 ** (db / 20.0)) / p)


def loopify(x, crossfade=0.06):
    """Make a clip loop with no seam.

    The tail is faded out over the head's fade-in and summed onto it, so the wrap
    point is a crossfade of the signal with itself rather than a splice. This is the
    single fix for ambience_loop, which cracked once every 5.75 seconds for months.
    """
    n = x.size
    c = min(int(round(crossfade * SR)), n // 3)
    if c < 2:
        return x
    head, tail = x[:c].copy(), x[-c:].copy()
    fade = np.linspace(0.0, 1.0, c)
    x = x[:-c].copy()
    x[:c] = head * fade + tail * (1.0 - fade)
    return x


# How far under the direct sound the reflections sit, and how long the room rings.
# These are send levels, so 0.28 is reflections about 11 dB down — present, nameable as
# a room, and nowhere near the wash that "put reverb on it" usually means.
SPACES = {
    'dry':   (0.00, 0.30),   # UI, and anything that is not IN the room
    'near':  (0.16, 0.32),   # a small prop under the hand
    'prop':  (0.28, 0.45),   # the everyday: glass, bottle, paper, garnish
    'wide':  (0.42, 0.60),   # a door, the cellar, a drawer — the room answers
    'far':   (0.55, 0.78),   # a voice, a bell, something across the bar
}


def render(x, level='body', name='clip', fade_in=0.0015, fade_out=0.012,
           drive=1.0, loop=False, space='prop'):
    """The ONLY export path. Everything that reaches a .wav passes through here.

    Order matters: voice and room FIRST, while the clip is still at synthesis level and
    a shelf cannot push it into the limiter; then DC (so the limiter is not biased),
    then limit (so peaks bend rather than snap), then set the level, and only THEN force
    the edges to zero — a fade applied before normalising would be scaled back up.

    `space` is how much bar stands behind the clip; see SPACES. It is a mix and not a
    send, so 'dry' leaves a clip exactly as it was — which is right for the UI, because
    a click does not happen in the room, it happens under the player's finger.
    """
    x = np.asarray(x, dtype=np.float64)
    if x.size == 0:
        raise ValueError('empty clip: ' + name)
    wet, rt = SPACES[space] if isinstance(space, str) else space
    x = cozy(x)
    x = room(x, wet=wet, rt60=rt, name='bar', tail=not loop)
    if not loop and wet > 0.0:
        x = trim_tail(x)
    x = dc_block(x)
    x = soft_limit(x, drive)
    x = normalize(x, LEVELS[level] if isinstance(level, str) else float(level))
    if loop:
        x = loopify(x)
        # A loop still needs its very first and last sample at zero: Unity starts it
        # from sample 0 on the first play, and that entry is a hard edge otherwise.
        fade_in = max(fade_in, 0.004)
        fade_out = max(fade_out, 0.004)
    n = x.size
    fi = max(2, int(round(fade_in * SR)))
    fo = max(2, int(round(fade_out * SR)))
    if fi + fo >= n:
        fi = fo = max(2, n // 4)
    # Raised-cosine fades: gentler than linear at the join, and they leave the
    # endpoints at exactly 0 rather than merely near it.
    x[:fi] *= 0.5 - 0.5 * np.cos(np.linspace(0, math.pi, fi))
    x[-fo:] *= 0.5 + 0.5 * np.cos(np.linspace(0, math.pi, fo))
    x[0] = 0.0
    x[-1] = 0.0
    return x


def write(path, x, name=None):
    """Quantise and write, then PROVE the file is clean before returning."""
    name = name or os.path.basename(path)
    x = np.asarray(x, dtype=np.float64)
    peak = float(np.max(np.abs(x)))
    if peak > 1.0:
        raise AssertionError('%s would clip: peak %.4f' % (name, peak))
    q = np.clip(np.round(x * 32767.0), -32768, 32767).astype(np.int16)
    if q.size and (abs(int(q[0])) != 0 or abs(int(q[-1])) != 0):
        raise AssertionError('%s does not start/end at zero: %d..%d'
                             % (name, q[0], q[-1]))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    w = wave.open(path, 'wb')
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes(q.tobytes())
    w.close()
    return {
        'name': name,
        'seconds': q.size / float(SR),
        'peak_db': 20 * math.log10(max(peak, 1e-9)),
        'rms_db': 20 * math.log10(max(float(np.sqrt(np.mean(x * x))), 1e-9)),
        'bytes': os.path.getsize(path),
    }
