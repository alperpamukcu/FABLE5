# -*- coding: utf-8 -*-
"""THE TRAILER'S OWN SCORE (2026-10-03). The author on v6: "müzik kötü, uygun bir müzik bul veya oluştur, fragmanlara
gameplay trailerlarına uygun olsun ... seslere yoğunlaşalım". No library track was cut for a trailer, so this one is
written TO the cut: it reads the cut's beat grid and its sections and plays a neon nu-disco cue that turns where the
picture turns.

    intro     filtered pad + plucked arp, a riser and a reverse swell into ...
    DROP      the sign: an impact, then four on the floor, clap, offbeat bass, chord stabs
    groove    the bar opens, cards, bottles, pours (a second, busier groove for the craft)
    hits      a stab + boom on the kick and on the slip
    break     drums out under Roxy's own line ("this room's been dark a long time"), a riser
    CHORUS    the house gets dressed: impact, full groove, a saw lead on top
    button    the end card: impact, drums out, the pad rings, one last hit under "Last call, honey"

Everything is synthesised here with numpy/scipy - no samples, nothing licensed.

    python3 Tools/trailer/score.py v6_60       -> Tools/trailer/out/score_v6_60.wav
"""
import json
import os
import sys

import numpy as np
from scipy.signal import butter, sosfilt, fftconvolve

HERE = os.path.dirname(os.path.abspath(__file__))
SR = 44100
RNG = np.random.default_rng(1986)


def midi(n):
    return 440.0 * 2 ** ((n - 69) / 12.0)


def lp(x, hz, order=2):
    return sosfilt(butter(order, min(hz, SR * 0.45), 'low', fs=SR, output='sos'), x)


def hp(x, hz, order=2):
    return sosfilt(butter(order, hz, 'high', fs=SR, output='sos'), x)


def bp(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, hi], 'band', fs=SR, output='sos'), x)


def saw(f, n, phase=0.0):
    t = np.arange(n) / SR
    ph = (f * t + phase) % 1.0
    # a soft saw: a few harmonics are plenty and nothing aliases
    out = np.zeros(n)
    for k in range(1, 14):
        if f * k > SR * 0.45:
            break
        out += np.sin(2 * np.pi * k * (f * t + phase)) / k
    return out * 0.6


def env_adsr(n, a, d, s, r):
    a, d, r = int(a * SR), int(d * SR), int(r * SR)
    e = np.full(n, s, float)
    a = min(a, n)
    e[:a] = np.linspace(0, 1, a, endpoint=False) if a else e[:a]
    d2 = min(d, max(0, n - a))
    e[a:a + d2] = np.linspace(1, s, d2, endpoint=False) if d2 else e[a:a + d2]
    if r and n > r:
        e[-r:] *= np.linspace(1, 0, r)
    return e


def add(buf, sig, at):
    i = int(at * SR)
    if i >= len(buf) or i + len(sig) <= 0:
        return
    if i < 0:
        sig, i = sig[-i:], 0
    m = min(len(sig), len(buf) - i)
    buf[i:i + m] += sig[:m]


# ── drums ──────────────────────────────────────────────────────────────────────────────────────────────────────
def kick(dur=0.42):
    n = int(dur * SR)
    t = np.arange(n) / SR
    f = 45 + 95 * np.exp(-t * 28)
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 7.5)
    click = hp(RNG.standard_normal(n), 3000) * np.exp(-t * 300) * 0.25
    return np.tanh((body + click) * 1.6) * 0.9


def clap():
    n = int(0.35 * SR)
    t = np.arange(n) / SR
    noise = bp(RNG.standard_normal(n), 900, 3800)
    e = np.zeros(n)
    for off in (0.0, 0.011, 0.022):
        i = int(off * SR)
        e[i:] += np.exp(-(t[:n - i]) * 60)
    e += np.exp(-t * 14) * 0.5
    return noise * e * 0.35


def hat(open_=False):
    n = int((0.22 if open_ else 0.05) * SR)
    t = np.arange(n) / SR
    return hp(RNG.standard_normal(n), 7000) * np.exp(-t * (14 if open_ else 90)) * (0.16 if open_ else 0.12)


def impact(big=1.0):
    n = int(2.6 * SR)
    t = np.arange(n) / SR
    f = 30 + 60 * np.exp(-t * 9)
    boom = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 2.2)
    crash = hp(RNG.standard_normal(n), 2500) * np.exp(-t * 2.5) * 0.35
    thud = lp(RNG.standard_normal(n), 400) * np.exp(-t * 18) * 0.8
    return np.tanh((boom * 1.2 + crash + thud) * big)


def riser(dur):
    n = int(dur * SR)
    t = np.arange(n) / SR
    u = t / dur
    noise = RNG.standard_normal(n)
    out = np.zeros(n)
    # a sweeping band, done in blocks
    blk = 2048
    for i in range(0, n, blk):
        c = 400 + 7000 * u[min(i, n - 1)] ** 2
        out[i:i + blk] = bp(noise[i:i + blk], c * 0.7, min(c * 1.4, SR * 0.45), 1)
    tone = np.sin(2 * np.pi * np.cumsum(200 + 900 * u ** 2) / SR) * 0.15
    return (out * 0.5 + tone) * u ** 2


def reverse_swell(dur=1.2):
    s = (hp(RNG.standard_normal(int(dur * SR)), 1500) * 0.4)[::-1]
    t = np.linspace(0, 1, len(s))
    return s * t ** 3


# ── tone ───────────────────────────────────────────────────────────────────────────────────────────────────────
def pad_chord(notes, dur, bright=1800):
    n = int(dur * SR)
    out = np.zeros(n)
    for m in notes:
        for d in (-0.12, -0.05, 0.0, 0.06, 0.13):
            out += saw(midi(m) * 2 ** (d / 12), n, RNG.random())
    out = lp(out / (len(notes) * 5), bright)
    return out * env_adsr(n, 0.25, 0.3, 0.8, 0.4) * 0.5


def stab(notes, bright=3500):
    n = int(0.32 * SR)
    out = np.zeros(n)
    for m in notes:
        for d in (-0.08, 0.0, 0.08):
            out += saw(midi(m) * 2 ** (d / 12), n, RNG.random())
    out = lp(out / (len(notes) * 3), bright)
    return out * env_adsr(n, 0.004, 0.12, 0.25, 0.15) * 0.55


def pluck(m, dur=0.2, bright=2600):
    n = int(dur * SR)
    t = np.arange(n) / SR
    s = np.sign(np.sin(2 * np.pi * midi(m) * t)) * 0.5 + saw(midi(m), n) * 0.5
    return lp(s, bright) * np.exp(-t * 16) * 0.22


def bass_note(m, dur):
    n = int(dur * SR)
    t = np.arange(n) / SR
    s = saw(midi(m), n) + np.sin(2 * np.pi * midi(m - 12) * t) * 0.8
    cut = 250 + 900 * np.exp(-t * 18)
    out = np.zeros(n)
    blk = 512
    for i in range(0, n, blk):
        out[i:i + blk] = lp(s[i:i + blk], cut[i], 1)
    return np.tanh(out * 1.4) * env_adsr(n, 0.003, 0.08, 0.7, 0.03) * 0.42


def lead_note(m, dur):
    n = int(dur * SR)
    t = np.arange(n) / SR
    vib = 1 + 0.004 * np.sin(2 * np.pi * 5.5 * t) * np.clip(t * 3, 0, 1)
    f = midi(m)
    ph = np.cumsum(f * vib) / SR
    s = sum(np.sin(2 * np.pi * k * ph) / k for k in range(1, 10)) * 0.6
    s += 0.5 * sum(np.sin(2 * np.pi * k * ph * 1.006) / k for k in range(1, 10)) * 0.6
    return lp(s, 3200) * env_adsr(n, 0.01, 0.15, 0.75, 0.08) * 0.2


def reverb(x, secs=1.6, mix=0.25):
    n = int(secs * SR)
    t = np.arange(n) / SR
    ir = RNG.standard_normal(n) * np.exp(-t * 4.2)
    ir = lp(ir, 5000)
    wet = fftconvolve(x, ir)[:len(x)]
    wet *= np.max(np.abs(x)) / (np.max(np.abs(wet)) + 1e-9)
    return x * (1 - mix) + wet * mix


def delay(x, secs, fb=0.35, mix=0.3):
    d = int(secs * SR)
    out = x.copy()
    tap = x.copy()
    for _ in range(4):
        tap = np.concatenate([np.zeros(d), tap[:-d]]) * fb
        out += tap
    return x * (1 - mix) + out * mix


# ── the cue ────────────────────────────────────────────────────────────────────────────────────────────────────
# A minor, i-VI-III-VII (Am F C G), one chord per bar
PROG = [(57, [57, 60, 64]), (53, [53, 57, 60]), (48, [55, 60, 64]), (55, [55, 59, 62])]
LEAD = [76, 74, 72, 74, 76, 79, 76, 74,  72, 72, 69, 72, 74, 72, 69, 67,
        76, 74, 72, 74, 76, 79, 81, 79,  76, 74, 72, 71, 72, 74, 76, 76]


def sections(cut, P):
    """Beat indices of the moments the music turns on, read off the cut."""
    beats, at = [], 0
    marks = {}
    for s in cut['segments']:
        b = max(1, round(s['sec'] / P))
        film, mark = s['take'][0][0], s['take'][0][1]
        if s.get('sign') and 'drop' not in marks:
            marks['drop'] = at
        if film.startswith('S03') and mark == 'kicked':
            marks['kick'] = at
        if film.startswith('S04') and mark == 'slip':
            marks['slip'] = at
        if film.startswith('S00') and s.get('type_blips'):
            marks['break'] = at
        if film.startswith('S07'):
            marks['chorus'] = at + 1           # the first fitting lands a beat in
        if film.startswith('S10') and mark == 'capped':
            marks['craft'] = at
        if s.get('end'):
            marks['end'] = at
        beats.append(b)
        at += b
    marks['total'] = at
    return marks


def build(cut_name):
    cut = json.load(open(os.path.join(HERE, 'cuts', cut_name + '.json')))
    P = cut.get('beat', 0.576923)
    M = sections(cut, P)
    total = M['total'] * P + 4.0
    n = int(total * SR)
    drums, bass, pads, arps, leads, fx = (np.zeros(n) for _ in range(6))
    bar = 4 * P
    drop, end, brk, cho = M.get('drop', 8), M.get('end', M['total'] - 10), M.get('break', 10 ** 6), M.get('chorus', 10 ** 6)
    craft = M.get('craft', 10 ** 6)

    def chord_at(beat):
        return PROG[(int(beat) // 4) % 4]

    total_beats = M['total']
    for b in range(total_beats + 4):
        t = b * P
        root, notes = chord_at(b)
        in_break = brk <= b < cho - 1
        playing = drop <= b < end and not in_break
        # pads: always, brighter after the drop, dark in the break, open on the button
        if b % 4 == 0 and b < total_beats + 2:
            bright = 900 if b < drop else (1100 if in_break else 2400)
            if b >= end:
                bright = 1600
            add(pads, pad_chord(notes + [notes[0] + 12], bar * (2 if b >= end else 1) + 0.3, bright), t)
        # arp: 16ths over the chord, filtered in the intro
        if b < end + 2 and not (drop <= b < drop + 1):
            seq = [notes[0] + 12, notes[1] + 12, notes[2] + 12, notes[1] + 12]
            for k in range(4):
                if b < drop and k % 2:
                    continue
                add(arps, pluck(seq[k], 0.22, 1400 if b < drop else (1800 if in_break else 3000)), t + k * P / 4)
        if playing:
            busy = b >= craft or b >= cho
            add(drums, kick(), t)
            if b % 2 == 1:
                add(drums, clap(), t)
            add(drums, hat(True), t + P / 2)
            for k in (1, 3):
                add(drums, hat(), t + k * P / 4)
            if busy:
                add(drums, hat(), t)
            # offbeat octave bass (the nu-disco pump)
            add(bass, bass_note(root - 12, P * 0.45), t + P / 2)
            if busy or b % 2:
                add(bass, bass_note(root, P * 0.22), t + P * 0.75)
            if b % 4 == 0 and b >= craft:
                add(pads, stab(notes + [notes[0] + 12]), t)
        # the lead: the chorus only, eighths/quarters
        if cho <= b < end:
            i = (b - cho)
            if i < len(LEAD):
                add(leads, lead_note(LEAD[i], P * 0.95), t)

    # moments
    def hit(beat, big=1.0, swell=True):
        if swell:
            add(fx, reverse_swell(1.2), beat * P - 1.2)
        add(fx, impact(big) * 0.9, beat * P)
    add(fx, riser(drop * P - 0.2) * 0.6, 0.2)
    hit(drop, 1.2)
    for key in ('kick', 'slip'):
        if key in M:
            add(fx, impact(0.7) * 0.6, M[key] * P)
            add(pads, stab(chord_at(M[key])[1] + [69]) * 1.2, M[key] * P)
    if brk < 10 ** 6:
        add(fx, riser((cho - brk) * P) * 0.7, brk * P)
    hit(cho, 1.25)
    hit(end, 1.0, swell=False)
    # the button: one last chord stab and a ring-out, two beats before the card ends
    last = (total_beats - 3) * P
    add(pads, stab([57, 60, 64, 69]) * 1.3, last)
    add(fx, impact(0.6) * 0.5, last)

    # sidechain pump on pads/bass/arp from the kick grid
    pump = np.ones(n)
    for b in range(drop, end):
        if brk <= b < cho - 1:
            continue
        i = int(b * P * SR)
        k = int(0.18 * SR)
        if i + k < n:
            pump[i:i + k] = np.minimum(pump[i:i + k], 0.35 + 0.65 * np.linspace(0, 1, k) ** 1.5)
    tonal = reverb((pads + arps * 0.9) * pump, 1.8, 0.3) + bass * pump
    leadbus = reverb(delay(leads, P * 0.75, 0.3, 0.25), 1.6, 0.25)
    mix = drums * 0.9 + tonal + leadbus + reverb(fx, 2.0, 0.3)
    mix = hp(mix, 28)
    mix = np.tanh(mix * 1.15) / np.tanh(1.15)
    mix /= np.max(np.abs(mix)) + 1e-9
    mix *= 0.89
    # fade the tail
    fade = int(3.0 * SR)
    mix[-fade:] *= np.linspace(1, 0, fade)
    out = os.path.join(HERE, 'out', 'score_%s.wav' % cut_name)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    pcm = (np.stack([mix, mix], 1) * 32767).astype(np.int16)
    import wave
    with wave.open(out, 'wb') as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    print('score:', out, '%.1f s' % total, M)
    return out


if __name__ == '__main__':
    build(sys.argv[1] if len(sys.argv) > 1 else 'v6_60')
