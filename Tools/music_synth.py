# -*- coding: utf-8 -*-
"""LAST CALL's synthwave — the bar's own music, composed and rendered here from oscillators. No samples.

Run:  py -3 Tools/music_synth.py                   every song into Assets/Resources/Audio as music_<name>.ogg
      py -3 Tools/music_synth.py night_1 story_1    only those
      py -3 Tools/music_synth.py night_1 --bars 8   a sketch of the first eight bars, as a .wav in the temp folder

WHY (2026-09-15, the author, pointing at a Pixabay track tagged synthwave / synth pop / synth disco / neon / night /
laid back / dreamy: "oyun müziği için bu tarzı uygun buldum bu tarz şarkılar oluşturabilir misin"). That track is
Content ID registered, so nothing of it is used: the style is the brief, and every note here is written by the code
below from the song table at the bottom.

HOW IT SOUNDS 80s WITHOUT A SINGLE SAMPLE
  oscillators  additive: each note is its waveform's partials, so nothing aliases, and the "filter" is the gain each
               partial gets from a 24 dB/oct low-pass response evaluated at its frequency — a moving cutoff is a
               moving response, which is the pluck of a synth bass and the swell of a pad
  instruments  LinnDrum-style kit (kick with a pitch drop, snare with the gated reverb the decade is known for,
               clap, hats, toms, a crash), octave-pumping saw bass, a three-voice detuned saw pad, a square arp
               through a dotted-eighth ping-pong delay, a gliding saw lead, and a DX7-type FM electric piano
  the mix      the kick ducks the bass, pad and arp (sidechain); a chorus widens the pad and the piano; a hall
               reverb and the delay are sends; the master is a static EQ, a slow glue compressor and tape-like
               saturation. Loudness is set to -18 LUFS on export, like every other music file in the game
  the writing  diatonic seventh chords voiced for the smallest movement from the last; a lead built from a
               two-bar motif (A A' B A-end) on the key's pentatonic, snapped to chord tones on the strong beats;
               sections switch parts in and out (intro, verse, chorus, break, outro) and open the filters as they go

Randomness is a seeded generator per song, so a song renders the same every time.
The short musical cues the game plays (another_round, verdict_good, ...) come from the same instruments through
`stinger`, which Tools/sfx_bank.py calls, so the UI and the music share one voice.
"""
import math
import os
import subprocess
import sys
import tempfile
import time
import wave

import numpy as np

SR = 44100
F32 = np.float32
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, '..', 'Assets', 'Resources', 'Audio'))
CTRL = 256          # samples per control step for moving filters


def hz(m):
    return 440.0 * 2.0 ** ((m - 69) / 12.0)


def noise(n, seed):
    return np.random.default_rng(seed).standard_normal(n).astype(F32)


def fade(x, fin=0.001, fout=0.006):
    x = np.array(x, dtype=F32)
    a, b = max(1, int(fin * SR)), max(1, int(fout * SR))
    x[:a] *= np.linspace(0.0, 1.0, a, dtype=F32)
    x[-b:] *= np.linspace(1.0, 0.0, b, dtype=F32)
    return x


# ── oscillators and envelopes ───────────────────────────────────────────────────────────────────────────────────

def _saw(k):
    return 1.0 / k


def _square(k):
    return np.where(k % 2 == 1, 1.0 / k, 0.0)


SHAPES = {'saw': _saw, 'square': _square}


def lp_gain(freq, cut, reso):
    """A 24 dB/oct low-pass's magnitude at `freq` for cutoff `cut`, with a resonant bump of `reso` at the cutoff."""
    r = freq / np.maximum(cut, 30.0)
    g = 1.0 / np.sqrt(1.0 + r ** 8)
    if reso > 0.0:
        g = g * (1.0 + reso * np.exp(-np.log2(np.maximum(r, 1e-6)) ** 2 / 0.045))
    return g


def osc(f0, n, shape='saw', cutoff=2000.0, cut_mul=None, reso=0.0, detune=0.0, vib=(0.0, 0.0, 0.3), glide=None,
        phase=0.0, max_k=48):
    """One note of `shape`, `n` samples long, additive and alias-free. `cut_mul(t)` (seconds -> multiplier) moves the
    cutoff; `vib` is (rate Hz, depth cents, delay s); `glide` is (from Hz, seconds)."""
    t = np.arange(n) / SR
    f = np.full(n, f0 * 2.0 ** (detune / 1200.0))
    if glide is not None:
        k = np.clip(t / max(glide[1], 1e-3), 0.0, 1.0)
        f = f * (glide[0] / f0) ** (1.0 - k)
    rate, cents, delay = vib
    if rate > 0.0 and cents > 0.0:
        depth = np.clip((t - delay) / 0.4, 0.0, 1.0) * cents
        f = f * 2.0 ** (depth * np.sin(2.0 * np.pi * rate * t) / 1200.0)
    ph = 2.0 * np.pi * np.cumsum(f) / SR + phase
    nc = n // CTRL + 2
    cut = cutoff * (cut_mul(np.arange(nc) * CTRL / SR) if cut_mul is not None else np.ones(nc))
    top = min(float(cut.max()) * 3.0, SR * 0.45)
    K = int(max(1, min(max_k, top // float(f.max()))))
    out = np.zeros(n, dtype=F32)
    idx = np.arange(n) / CTRL if cut_mul is not None else None
    grid = np.arange(nc)
    for k in range(1, K + 1):
        a = float(SHAPES[shape](np.array([k]))[0])
        if a == 0.0:
            continue
        if cut_mul is None:
            g = a * float(lp_gain(k * f0, cutoff, reso))
            if g < 2e-4:
                continue
            out += F32(g) * np.sin((k * ph).astype(F32))
        else:
            gk = a * lp_gain(k * f0, cut, reso)
            if float(gk.max()) < 2e-4:
                continue
            out += np.interp(idx, grid, gk).astype(F32) * np.sin((k * ph).astype(F32))
    return out


def adsr(n, gate, a, d, s, r):
    """Attack to 1, decay toward `s`; from `gate` samples on, a release that is ~-43 dB after `r` seconds."""
    t = np.arange(n) / SR
    a, d = max(a, 1e-3), max(d, 1e-3)
    env = np.where(t < a, t / a, s + (1.0 - s) * np.exp(-(t - a) / d))
    if gate < n:
        g = gate / SR
        lvl = g / a if g < a else s + (1.0 - s) * math.exp(-(g - a) / d)
        tail = t >= g
        env[tail] = lvl * np.exp(-(t[tail] - g) / max(r / 5.0, 1e-3))
    m = min(n, 256)
    env[-m:] *= np.linspace(1.0, 0.0, m)
    return env.astype(F32)


def epiano(f0, n, gate, vel):
    """A DX7-type electric piano: a sine carrier phase-modulated at its own frequency with a decaying index, and the
    short high 'tine' that makes the attack a bell."""
    t = np.arange(n) / SR
    index = (0.9 + 2.2 * vel) * np.exp(-t / 0.45) + 0.2
    body = np.sin(2.0 * np.pi * f0 * t + index * np.sin(2.0 * np.pi * f0 * t))
    if f0 * 14.0 < SR * 0.45:
        body = body + np.sin(2.0 * np.pi * f0 * 14.0 * t) * np.exp(-t / 0.03) * 0.18 * vel
    decay = np.exp(-t / (1.8 * (261.6 / f0) ** 0.4))
    return (body * decay).astype(F32) * adsr(n, gate, 0.002, 10.0, 1.0, 0.35)


# ── signal tools ─────────────────────────────────────────────────────────────────────────────────────────────────

def fft_filter(x, lo=None, hi=None, order=2):
    n = x.shape[-1]
    X = np.fft.rfft(x, axis=-1)
    f = np.fft.rfftfreq(n, 1.0 / SR)
    g = np.ones_like(f)
    if hi:
        g /= np.sqrt(1.0 + (f / hi) ** (2 * order))
    if lo:
        g /= np.sqrt(1.0 + (lo / np.maximum(f, 1e-3)) ** (2 * order))
    return np.fft.irfft(X * g, n, axis=-1).astype(F32)


def convolve(x, ir):
    """Convolution by FFT, trimmed to the input's length. x is (n,) or (channels, n); ir is (n,) or (channels, n)."""
    mono = x.ndim == 1
    xs = x[None, :] if mono else x
    irs = ir[None, :] if ir.ndim == 1 else ir
    n = xs.shape[1]
    m = 1 << int(math.ceil(math.log2(n + irs.shape[1])))
    out = np.empty_like(xs)
    for c in range(xs.shape[0]):
        out[c] = np.fft.irfft(np.fft.rfft(xs[c], m) * np.fft.rfft(irs[c % irs.shape[0]], m), m)[:n]
    return out[0] if mono else out


def movavg(v, w):
    w = max(1, int(w))
    c = np.cumsum(np.concatenate(([0.0], v.astype(np.float64))))
    out = (c[w:] - c[:-w]) / w
    pad = w // 2
    return np.concatenate((np.full(pad, out[0]), out, np.full(v.size - out.size - pad, out[-1])))


def put(buf, x, at, pan=0.0, gain=1.0):
    """Mix mono `x` into stereo `buf` at `at` seconds, panned -1..1 (equal power, unity in the middle)."""
    a = int(round(at * SR))
    if a < 0 or a >= buf.shape[1]:
        return
    m = min(x.size, buf.shape[1] - a)
    th = (pan + 1.0) * math.pi / 4.0
    buf[0, a:a + m] += x[:m] * F32(gain * math.cos(th) * math.sqrt(2.0))
    buf[1, a:a + m] += x[:m] * F32(gain * math.sin(th) * math.sqrt(2.0))


def chorus(part, rate=0.45, depth_ms=3.0, base_ms=11.0, mix=0.45):
    n = part.shape[1]
    t = np.arange(n) / SR
    grid = np.arange(n)
    out = np.empty_like(part)
    for c in range(2):
        d = (base_ms + depth_ms * np.sin(2.0 * np.pi * rate * t + c * np.pi)) * SR / 1000.0
        wet = np.interp(grid - d, grid, part[c]).astype(F32)
        out[c] = part[c] * F32(1.0 - mix * 0.5) + wet * F32(mix)
    return out


def sidechain(n, kicks, depth, release=0.16):
    g = np.ones(n, dtype=F32)
    L = int(0.4 * SR)
    t = np.arange(L) / SR
    shape = (1.0 - depth * np.exp(-t / release) * np.clip(t / 0.005, 0.0, 1.0)).astype(F32)
    for s in kicks:
        a = int(round(s * SR))
        m = min(L, n - a)
        if m > 0:
            np.minimum(g[a:a + m], shape[:m], out=g[a:a + m])
    return g


_HALL = None


def hall_ir(rt=2.4, pre=0.03):
    """A stereo hall: decorrelated noise under an exponential decay that darkens as it goes, after a pre-delay."""
    global _HALL
    if _HALL is not None:
        return _HALL
    n = int((rt * 1.1 + pre) * SR)
    t = np.arange(n) / SR
    env = np.where(t > pre, np.exp(-6.9 * (t - pre) / rt), 0.0).astype(F32)
    ir = np.stack([noise(n, 71), noise(n, 72)]) * env
    bright, dark = fft_filter(ir, lo=180.0, hi=6500.0), fft_filter(ir, lo=180.0, hi=1800.0)
    w = np.clip((t - pre) / rt, 0.0, 1.0).astype(F32)
    ir = bright * (1.0 - w) + dark * w
    _HALL = ir / F32(np.sqrt(np.sum(ir ** 2) / 2.0))
    return _HALL


def ping_pong(send, beat, fb=0.4, repeats=6):
    n = send.shape[1]
    d = int(round(0.75 * beat * SR))
    m = fft_filter((send[0] + send[1]) * F32(0.5), lo=250.0, hi=4500.0)
    out = np.zeros_like(send)
    for r in range(1, repeats + 1):
        sh = d * r
        if sh >= n:
            break
        out[(r + 1) % 2, sh:] += m[:n - sh] * F32(fb ** (r - 1))
    return out


# ── the kit ──────────────────────────────────────────────────────────────────────────────────────────────────────

def _norm(x):
    p = float(np.max(np.abs(x)))
    return fade(x / p if p > 1e-9 else x)


def kick_sample():
    n = int(0.5 * SR)
    t = np.arange(n) / SR
    f = 52.0 + 100.0 * np.exp(-t / 0.028)
    body = np.sin(2.0 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.2)
    click = fft_filter(noise(n, 11) * np.exp(-t / 0.004).astype(F32), lo=1800.0) * 0.45
    # Measured on the first render: the kick's tail under 60 Hz was most of the song's energy. Shorter, higher, and
    # with its lowest octave rolled off, it is a thump at a bar's volume rather than a subwoofer test.
    return _norm(fft_filter(np.tanh(1.8 * (body + click)) / math.tanh(1.8), lo=38.0))


def snare_sample():
    """The gated snare: body and rattle, a dense room behind them, cut dead at 0.3 s — the drum machine's signature."""
    n = int(0.42 * SR)
    t = np.arange(n) / SR
    tone = (0.6 * np.sin(2.0 * np.pi * 188.0 * t) + 0.3 * np.sin(2.0 * np.pi * 331.0 * t)) * np.exp(-t / 0.055)
    rattle = fft_filter(noise(n, 12), lo=1200.0, hi=9000.0) * np.exp(-t / 0.12).astype(F32)
    dry = (0.55 * tone).astype(F32) + 0.9 * rattle
    ti = np.arange(int(0.4 * SR)) / SR
    ir = fft_filter(noise(ti.size, 13) * np.exp(-ti / 0.5).astype(F32), lo=400.0, hi=7000.0)
    wet = convolve(dry, ir)
    wet *= np.clip((0.30 - t) / 0.04, 0.0, 1.0).astype(F32)
    wet *= F32(0.75 * np.max(np.abs(dry)) / max(float(np.max(np.abs(wet))), 1e-9))
    return _norm(dry + wet)


def clap_sample():
    n = int(0.35 * SR)
    t = np.arange(n) / SR
    env = np.zeros(n)
    for k, at in enumerate((0.0, 0.011, 0.022)):
        env += np.where(t >= at, np.exp(-(t - at) / 0.006), 0.0) * (0.7 if k < 2 else 1.0)
    env += np.where(t >= 0.022, np.exp(-(t - 0.022) / 0.09), 0.0) * 0.45
    return _norm(fft_filter(noise(n, 14), lo=900.0, hi=5000.0) * env.astype(F32))


def hat_sample(open_=False):
    n = int((0.45 if open_ else 0.12) * SR)
    t = np.arange(n) / SR
    return _norm(fft_filter(noise(n, 15 if open_ else 16), lo=7000.0) * np.exp(-t / (0.28 if open_ else 0.035)).astype(F32))


def shaker_sample():
    n = int(0.09 * SR)
    t = np.arange(n) / SR
    env = np.clip(t / 0.012, 0.0, 1.0) * np.exp(-t / 0.03)
    return _norm(fft_filter(noise(n, 17), lo=5000.0, hi=11000.0) * env.astype(F32))


def rim_sample():
    n = int(0.08 * SR)
    t = np.arange(n) / SR
    body = np.sin(2.0 * np.pi * 1650.0 * t) * np.exp(-t / 0.012) + fft_filter(noise(n, 18), lo=2000.0) * np.exp(-t / 0.005).astype(F32) * 0.4
    return _norm(body)


def tom_sample(f):
    n = int(0.5 * SR)
    t = np.arange(n) / SR
    fr = f * (1.0 + 0.6 * np.exp(-t / 0.04))
    return _norm(np.sin(2.0 * np.pi * np.cumsum(fr) / SR) * np.exp(-t / 0.22))


def crash_sample():
    n = int(2.2 * SR)
    t = np.arange(n) / SR
    return _norm(fft_filter(noise(n, 19), lo=4000.0, hi=14000.0) * np.exp(-t / 1.1).astype(F32))


_KIT = None


def kit():
    global _KIT
    if _KIT is None:
        _KIT = dict(kick=kick_sample(), snare=snare_sample(), clap=clap_sample(), hat=hat_sample(), ohat=hat_sample(True),
                    shaker=shaker_sample(), rim=rim_sample(), crash=crash_sample(),
                    toms=[tom_sample(f) for f in (190.0, 150.0, 118.0, 92.0)])
    return _KIT


# ── harmony and melody ───────────────────────────────────────────────────────────────────────────────────────────

MODES = {'minor': (0, 2, 3, 5, 7, 8, 10), 'dorian': (0, 2, 3, 5, 7, 9, 10), 'major': (0, 2, 4, 5, 7, 9, 11)}
PENTA = {'minor': (0, 3, 5, 7, 10), 'dorian': (0, 3, 5, 7, 10), 'major': (0, 2, 4, 7, 9)}


def chord(key, mode, degree, size=4):
    steps = MODES[mode]
    return [key + steps[(degree + 2 * e) % 7] + 12 * ((degree + 2 * e) // 7) for e in range(size)]


def voice(tones, prev, low, high):
    """The inversion of `tones` inside [low, high] that moves least from `prev` (or sits mid-range with no prev)."""
    pcs = [t % 12 for t in tones]
    best, best_score = None, 1e9
    for inv in range(len(pcs)):
        order = pcs[inv:] + pcs[:inv]
        for base in range(low, high + 1):
            if base % 12 != order[0]:
                continue
            notes = [base]
            for pc in order[1:]:
                m = notes[-1] + 1
                while m % 12 != pc:
                    m += 1
                notes.append(m)
            if notes[-1] > high:
                continue
            score = (sum(abs(a - b) for a, b in zip(notes, prev)) if prev and len(prev) == len(notes)
                     else abs(sum(notes) / len(notes) - (low + high) / 2.0))
            if score < best_score:
                best, best_score = notes, score
    return best or [low + (p - low) % 12 for p in pcs]


LEAD_RHYTHMS = [      # (start beat, length) within a bar
    [(0.0, 1.5), (1.5, 0.5), (2.0, 1.0), (3.0, 1.0)],
    [(0.0, 0.5), (0.5, 1.0), (1.5, 1.5), (3.0, 1.0)],
    [(0.0, 2.0), (2.5, 0.5), (3.0, 1.0)],
    [(0.5, 0.5), (1.0, 1.0), (2.0, 2.0)],
    [(0.0, 1.0), (1.0, 0.5), (1.5, 0.5), (2.0, 1.5)],
    [(0.0, 3.0), (3.0, 0.5), (3.5, 0.5)],
]


def motif(rng):
    """An eight-bar lead phrase as rhythms and melodic steps: A (2 bars), A again, B (2 new bars), A ending long."""
    ra, rb, rc, rd = (int(rng.integers(len(LEAD_RHYTHMS))) for _ in range(4))
    bars = [ra, rb, ra, rb, rc, rd, ra, None]

    def steps(count):
        out, last, run = [], 0, 0
        for _ in range(count):
            s = int(rng.choice([-2, -1, -1, 1, 1, 2]))
            if last and (s > 0) == (last > 0):
                run += 1
                if run >= 2:
                    s, run = -s, 0
            else:
                run = 0
            out.append(s)
            last = s
        return out

    a_steps = [steps(len(LEAD_RHYTHMS[ra])), steps(len(LEAD_RHYTHMS[rb]))]
    b_steps = [steps(len(LEAD_RHYTHMS[rc])), steps(len(LEAD_RHYTHMS[rd]))]
    return dict(bars=bars, steps=[a_steps[0], a_steps[1], a_steps[0], a_steps[1], b_steps[0], b_steps[1], a_steps[0], [0]])


def realize(mot, key, mode, chords, low=62, high=84):
    """The phrase's notes over the given bar chords: (beat offset, length, midi). Strong beats land on chord tones."""
    penta = {(key + p) % 12 for p in PENTA[mode]}
    pool = [m for m in range(low, high + 1) if m % 12 in penta]
    prev = pool[len(pool) // 2]
    notes = []
    for i, tones in enumerate(chords):
        r = mot['bars'][i % 8]
        rhythm = [(0.0, 3.5)] if r is None else LEAD_RHYTHMS[r]
        steps = mot['steps'][i % 8]
        cpcs = {t % 12 for t in tones}
        for j, (pos, dur) in enumerate(rhythm):
            s = steps[j % len(steps)]
            if r is None:
                m = min((p for p in pool if p % 12 == key % 12), key=lambda p: abs(p - prev))
            elif pos in (0.0, 2.0):
                good = [p for p in pool if p % 12 in cpcs] or pool
                m = min(good, key=lambda p: abs(p - (prev + 2 * s)))
            else:
                at = min(range(len(pool)), key=lambda q: abs(pool[q] - prev))
                m = pool[max(0, min(len(pool) - 1, at + s))]
            notes.append((i * 4.0 + pos, dur, m))
            prev = m
    return notes


# ── a song ───────────────────────────────────────────────────────────────────────────────────────────────────────

class Mix:
    def __init__(self, seconds):
        self.n = int(seconds * SR)
        self.dry = np.zeros((2, self.n), dtype=F32)
        self.verb = np.zeros((2, self.n), dtype=F32)
        self.echo = np.zeros((2, self.n), dtype=F32)

    def part(self):
        return np.zeros((2, self.n), dtype=F32)

    def add(self, part, verb=0.0, echo=0.0):
        self.dry += part
        if verb:
            self.verb += part * F32(verb)
        if echo:
            self.echo += part * F32(echo)


def plan(spec, max_bars=None):
    bars = []
    for sec in spec['form']:
        name, count, parts = sec[0], sec[1], set(sec[2].split())
        lo, hi = sec[3] if len(sec) > 3 else (1.0, 1.0)
        prog = spec.get('prog_' + name, spec['prog'])
        start = len(bars)
        for i in range(count):
            bars.append(dict(section=name, start=start, i=i, count=count, parts=parts,
                             open=lo + (hi - lo) * (i / max(count - 1, 1)), degree=prog[i % len(prog)]))
    bars = bars[:max_bars] if max_bars else bars
    for j, b in enumerate(bars):
        b['next'] = bars[j + 1]['parts'] if j + 1 < len(bars) else set()
        b['prev'] = bars[j - 1]['parts'] if j > 0 else set()
    return bars


PATTERNS = {
    'laid': dict(kick=[0, 8], kick_odd=[0, 8, 11], snare=[4, 12], hat=[0, 2, 4, 6, 8, 10, 12, 14], ohat=[14]),
    'disco': dict(kick=[0, 4, 8, 12], kick_odd=[0, 4, 8, 12], snare=[4, 12], clap=[4, 12], hat=[1, 3, 5, 7, 9, 11, 13, 15],
                  ohat=[2, 6, 10, 14]),
    'soft': dict(kick=[0, 8], kick_odd=[0, 8], rim=[4, 12], shaker=list(range(16))),
}


def render_song(spec, max_bars=None, log=print):
    rng = np.random.default_rng(spec['seed'])
    key, mode, bpm = spec['key'], spec['mode'], spec['bpm']
    beat = 60.0 / bpm
    bar = 4.0 * beat
    bars = plan(spec, max_bars)
    mix = Mix(len(bars) * bar + 4.0)
    times = np.array([j * bar for j in range(len(bars))] + [len(bars) * bar])
    opens = np.array([b['open'] for b in bars] + [bars[-1]['open']])

    def open_at(t0):
        return lambda tc: np.interp(t0 + tc, times, opens)

    chords = [chord(key, mode, b['degree'], 4) for b in bars]
    pads, prev = [], None
    for c in chords:
        prev = voice(c, prev, 50, 76)
        pads.append(prev)
    kicks = []
    clock = time.time()

    # drums first: their kicks drive the sidechain
    drums = mix.part()
    style = spec.get('drums')
    if style:
        K, P = kit(), PATTERNS[style]
        for j, b in enumerate(bars):
            if 'drums' not in b['parts']:
                continue
            t0 = j * bar
            fill = b['i'] == b['count'] - 1 and 'drums' in b['next'] and b['count'] >= 8
            if b['i'] == 0 and style != 'soft':
                put(drums, K['crash'], t0, 0.3, 0.16)
            for s in (P['kick_odd'] if j % 2 else P['kick']):
                if fill and s >= 8:
                    continue
                put(drums, K['kick'], t0 + s * beat / 4.0, 0.0, 0.6 if style != 'soft' else 0.42)
                kicks.append(t0 + s * beat / 4.0)
            for s in P.get('snare', []):
                if not (fill and s >= 8):
                    put(drums, K['snare'], t0 + s * beat / 4.0, -0.05, 0.42)
            for s in P.get('clap', []):
                put(drums, K['clap'], t0 + s * beat / 4.0, 0.1, 0.22)
            for s in P.get('rim', []):
                put(drums, K['rim'], t0 + s * beat / 4.0, -0.2, 0.2)
            for s in P.get('hat', []):
                put(drums, K['hat'], t0 + s * beat / 4.0, 0.3, (0.24 if s % 4 == 0 else 0.16) * float(rng.uniform(0.85, 1.0)))
            for s in P.get('ohat', []):
                put(drums, K['ohat'], t0 + s * beat / 4.0, 0.35, 0.15)
            for s in P.get('shaker', []):
                put(drums, K['shaker'], t0 + s * beat / 4.0, 0.25, (0.14 if s % 2 == 0 else 0.09) * float(rng.uniform(0.8, 1.0)))
            if fill:
                for k, s in enumerate((8, 10, 12, 14)):
                    put(drums, K['toms'][k], t0 + s * beat / 4.0, -0.4 + 0.25 * k, 0.4)
                if style == 'disco':
                    for k, s in enumerate((12, 13, 14, 15)):
                        put(drums, K['snare'], t0 + s * beat / 4.0, 0.0, 0.15 + 0.07 * k)
        mix.add(drums, verb=0.05)
    del drums
    log('    drums %.1fs' % (time.time() - clock))
    n = mix.n

    # pad: one held chord per run of bars with the same degree
    pad = mix.part()
    j = 0
    while j < len(bars):
        b = bars[j]
        if 'pad' not in b['parts']:
            j += 1
            continue
        k = j
        while k + 1 < len(bars) and 'pad' in bars[k + 1]['parts'] and bars[k + 1]['degree'] == b['degree'] and k + 1 - j < 4:
            k += 1
        t0, length = j * bar, (k - j + 1) * bar
        nn = int((length + 1.4) * SR)
        env = adsr(nn, int(length * SR), 0.35, 1.5, 0.8, 1.2)
        lo_o, hi_o = float(np.interp(t0, times, opens)), float(np.interp(t0 + length, times, opens))
        for note in pads[j]:
            for dv, pan in ((-9.0, -0.55), (0.0, 0.0), (9.0, 0.55)):
                cm = None if abs(hi_o - lo_o) < 0.02 else open_at(t0)
                x = osc(hz(note), nn, 'saw', cutoff=spec.get('pad_cut', 3400.0) * (lo_o if cm is None else 1.0), cut_mul=cm,
                        reso=0.1, detune=dv, phase=float(rng.uniform(0, 2 * np.pi)), max_k=28)
                put(pad, x * env, t0, pan, 0.07)
        j = k + 1
    pad = chorus(pad) * sidechain(n, kicks, 0.35)
    mix.add(pad, verb=0.35)
    del pad
    log('    pad %.1fs' % (time.time() - clock))

    # bass
    bass = mix.part()
    bstyle = spec.get('bass', 'octave')
    for j, b in enumerate(bars):
        if 'bass' not in b['parts']:
            continue
        root = 33 + ((chords[j][0] - 33) % 12)
        nxt = 33 + ((chords[j + 1][0] - 33) % 12) if j + 1 < len(bars) else root
        if bstyle == 'octave':
            events = [(i * 0.5, 0.5, root + (12 if i % 2 else 0), 1.0 if i % 2 == 0 else 0.75) for i in range(8)]
        elif bstyle == 'drive':
            events = [(i * 0.5, 0.5, root, 1.0 if i % 4 == 0 else 0.8) for i in range(7)]
            events.append((3.5, 0.5, nxt if nxt != root else root + 7, 0.8))
        else:
            events = [(0.0, 2.0, root, 0.9), (2.0, 1.5, root, 0.75), (3.5, 0.5, root + 7, 0.6)]
        for pos, dur, m, vel in events:
            gate = int(dur * beat * SR * (0.8 if bstyle != 'long' else 0.95))
            nn = gate + int(0.12 * SR)
            tt = np.arange(nn) / SR
            x = osc(hz(m), nn, 'saw', cutoff=950.0, cut_mul=lambda tc: 0.3 + 0.7 * np.exp(-tc / 0.13), reso=0.35, max_k=40)
            x += (0.3 * np.sin(2.0 * np.pi * hz(m) * tt)).astype(F32)
            put(bass, x * adsr(nn, gate, 0.004, 0.25, 0.65, 0.08), j * bar + pos * beat, 0.0, 0.14 * vel)
    mix.add(bass * sidechain(n, kicks, 0.55), verb=0.02)
    del bass
    log('    bass %.1fs' % (time.time() - clock))

    # arp
    arp = mix.part()
    rate = spec.get('arp', 16)
    for j, b in enumerate(bars):
        if 'arp' not in b['parts']:
            continue
        seq = [m + 12 for m in pads[j]] + [pads[j][1] + 24]
        order = list(range(len(seq))) + list(range(len(seq) - 2, 0, -1))
        step = 4.0 / rate
        for i in range(rate):
            m = seq[order[i % len(order)]]
            gate = int(step * beat * SR * 0.6)
            nn = gate + int(0.1 * SR)
            o = b['open']
            x = osc(hz(m), nn, 'square', cutoff=3400.0 * o, cut_mul=lambda tc: 0.3 + 0.7 * np.exp(-tc / 0.07), reso=0.25, max_k=24)
            vel = 1.0 if i % 4 == 0 else 0.75
            put(arp, x * adsr(nn, gate, 0.002, 0.08, 0.35, 0.08), j * bar + i * step * beat, 0.35 if i % 2 else -0.35, 0.075 * vel)
    mix.add(arp * sidechain(n, kicks, 0.25), verb=0.2, echo=0.35)
    del arp
    log('    arp %.1fs' % (time.time() - clock))

    # electric piano
    ep = mix.part()
    estyle = spec.get('ep', 'comp')
    for j, b in enumerate(bars):
        if 'ep' not in b['parts']:
            continue
        tones = voice(chord(key, mode, b['degree'], 5), None, 53, 79)
        hits = [(0.0, 4.0, 0.7)] if estyle == 'ballad' else ([(0.0, 1.5, 0.75), (2.5, 1.0, 0.6)] if j % 2 == 0
                                                             else [(0.0, 1.5, 0.7), (2.5, 0.5, 0.55), (3.5, 0.5, 0.5)])
        roll = 0.035 if estyle == 'ballad' else 0.0
        for pos, dur, vel in hits:
            for i, m in enumerate(tones):
                gate = int(dur * beat * SR)
                nn = gate + int(1.0 * SR)
                put(ep, epiano(hz(m), nn, gate, vel), j * bar + pos * beat + i * roll, (i / (len(tones) - 1) - 0.5) * 0.5, 0.07)
    ep = chorus(ep, rate=0.3, depth_ms=2.0, mix=0.35) * sidechain(n, kicks, 0.15)
    mix.add(ep, verb=0.25, echo=0.1)
    del ep
    log('    epiano %.1fs' % (time.time() - clock))

    # lead
    lstyle = spec.get('lead')
    if lstyle:
        lead = mix.part()
        mot = motif(np.random.default_rng(spec['seed'] + 7))
        j = 0
        while j < len(bars):
            if 'lead' not in bars[j]['parts']:
                j += 1
                continue
            k = j
            while k + 1 < len(bars) and 'lead' in bars[k + 1]['parts'] and bars[k + 1]['start'] == bars[j]['start']:
                k += 1
            notes = realize(mot, key, mode, chords[j:k + 1])
            last = None
            for pos, dur, m in notes:
                t0 = j * bar + pos * beat
                gate = int(dur * beat * SR * 0.92)
                nn = gate + int(0.25 * SR)
                glide = (hz(last[1]), 0.05) if last is not None and abs(last[0] - pos) < 1e-6 else None
                if lstyle == 'saw':
                    x = sum(osc(hz(m), nn, 'saw', cutoff=2800.0, cut_mul=lambda tc: 0.55 + 0.45 * np.exp(-tc / 0.25), reso=0.2,
                                detune=dv, vib=(5.2, 14.0, 0.25), glide=glide, max_k=36) for dv in (-6.0, 6.0))
                    env = adsr(nn, gate, 0.01, 0.3, 0.8, 0.12)
                    put(lead, x * env, t0, 0.05, 0.05)
                else:
                    x = osc(hz(m), nn, 'square', cutoff=1600.0, reso=0.1, vib=(5.0, 18.0, 0.2), glide=glide, max_k=20)
                    env = adsr(nn, gate, 0.04, 0.4, 0.75, 0.2)
                    put(lead, x * env, t0, 0.05, 0.075)
                last = (pos + dur, m)
            j = k + 1
        mix.add(lead, verb=0.3, echo=0.3)
        del lead
    log('    lead %.1fs' % (time.time() - clock))
    return master(mix, beat)


def master(mix, beat):
    x = mix.dry
    x += convolve(mix.verb, hall_ir()) * F32(0.9)
    x += ping_pong(mix.echo, beat)
    n = x.shape[1]
    f = np.fft.rfftfreq(n, 1.0 / SR)
    g = 1.0 / np.sqrt(1.0 + (40.0 / np.maximum(f, 1e-3)) ** 4)
    g *= 1.0 + (10 ** (1.5 / 20) - 1.0) / (1.0 + (f / 110.0) ** 2)                      # low shelf +1.5 dB
    g *= 10 ** (-1.2 / 20 * np.exp(-np.log2(np.maximum(f, 1.0) / 320.0) ** 2 / 0.5))    # -1.2 dB around 320 Hz
    g *= 1.0 + (10 ** (2.0 / 20) - 1.0) / (1.0 + (5000.0 / np.maximum(f, 1e-3)) ** 2)   # presence and air +2 dB
    x = np.fft.irfft(np.fft.rfft(x, axis=1) * g, n, axis=1).astype(F32)
    # THE SECTIONS RIDE CLOSER (measured on the first render: the intro and the break sat 11 dB under the verses). A
    # background track must not send the player to the volume: a slow gain rides each stretch toward the song's median
    # level, lifting a quiet section by at most about 5 dB and never pushing a loud one.
    slow = np.sqrt(movavg((x[0].astype(np.float64) ** 2 + x[1].astype(np.float64) ** 2) / 2.0, 3.0 * SR))
    live = slow > float(slow.max()) * 0.02
    target = float(np.median(slow[live])) if live.any() else 1.0
    ride = np.clip(target / np.maximum(slow, 1e-9), 1.0, 2.5) ** 0.6
    ride[~live] = 1.0
    x *= movavg(ride, 2.0 * SR).astype(F32)
    rms = np.sqrt(movavg((x[0].astype(np.float64) ** 2 + x[1].astype(np.float64) ** 2) / 2.0, 0.05 * SR))
    thr = 0.6 * float(rms.max())
    gain = np.where(rms > thr, (thr / np.maximum(rms, 1e-9)) ** 0.4, 1.0)
    x *= movavg(gain, 0.12 * SR).astype(F32)
    x /= F32(max(float(np.max(np.abs(x))), 1e-9) / 0.9)
    x = (np.tanh(1.15 * x) / math.tanh(1.15)).astype(F32)
    x /= F32(float(np.max(np.abs(x))) / 0.89)
    return x


# ── the game's short musical cues ────────────────────────────────────────────────────────────────────────────────

STINGERS = ('another_round', 'buy', 'order_ready', 'rim_done', 'serve_it', 'verdict_good', 'verdict_flat', 'verdict_bad',
            'debt_alarm', 'synth_swell', 'cheer_sfx', 'upset_sfx')


def stinger(name):
    """A cue in the songs' own instruments, mono float64: the electric piano for good news, a soft lead for bad, the
    pad for the closing beat's swell."""
    buf = np.zeros((2, int(3.0 * SR)), dtype=F32)

    def ep(notes, vel=0.7, hold=0.5):
        for at, m in notes:
            nn = int((hold + 1.2) * SR)
            put(buf, epiano(hz(m), nn, int(hold * SR), vel), at, 0.0, 0.5)

    def pluck(notes, vel=0.6):
        for at, m in notes:
            nn = int(0.35 * SR)
            x = osc(hz(m), nn, 'square', cutoff=2400.0, cut_mul=lambda tc: 0.3 + 0.7 * np.exp(-tc / 0.06), reso=0.2, max_k=24)
            put(buf, x * adsr(nn, int(0.12 * SR), 0.002, 0.08, 0.3, 0.1), at, 0.0, 0.35 * vel)

    if name == 'another_round':
        pluck([(0.0, 72), (0.11, 77)]); ep([(0.0, 72), (0.11, 77)], 0.75, 0.35)
    elif name == 'buy':
        pluck([(0.0, 72), (0.06, 76), (0.12, 79)]); ep([(0.18, 84)], 0.7, 0.3)
    elif name == 'order_ready':
        ep([(0.0, 81), (0.13, 88)], 0.45, 0.25)
    elif name == 'rim_done':
        ep([(0.0, 88)], 0.5, 0.12); pluck([(0.0, 88)], 0.4)
    elif name == 'serve_it':
        pluck([(0.0, 72), (0.0, 76)], 0.7); ep([(0.0, 76)], 0.6, 0.2)
    elif name == 'verdict_good':
        ep([(0.0, 72), (0.1, 76), (0.2, 79), (0.3, 84)], 0.7, 0.45)
    elif name == 'verdict_flat':
        ep([(0.0, 74), (0.18, 74)], 0.45, 0.2)
    elif name == 'verdict_bad':
        for at, m, d in ((0.0, 64, 0.18), (0.2, 63, 0.35)):
            nn = int((d + 0.3) * SR)
            x = osc(hz(m), nn, 'saw', cutoff=1100.0, reso=0.1, vib=(5.0, 10.0, 0.1), max_k=20)
            put(buf, x * adsr(nn, int(d * SR), 0.01, 0.2, 0.7, 0.2), at, 0.0, 0.3)
    elif name == 'debt_alarm':
        for at, m, d in ((0.0, 45, 0.16), (0.3, 45, 0.16), (0.6, 44, 0.35)):
            nn = int((d + 0.2) * SR)
            x = osc(hz(m), nn, 'square', cutoff=900.0, cut_mul=lambda tc: 0.5 + 0.5 * np.exp(-tc / 0.1), reso=0.3, max_k=30)
            put(buf, x * adsr(nn, int(d * SR), 0.005, 0.1, 0.7, 0.1), at, 0.0, 0.45)
    elif name == 'cheer_sfx':
        # A GOOD ORDER, heard as a notification and not a crowd (2026-09-15, the author: "insan sesi olmasin iyi ve kotu
        # siparislerde bildirim seslerine benzer sesler yeterli olur"): two quick bright blips up and a ring on top.
        pluck([(0.0, 79), (0.07, 84)], 0.8); ep([(0.07, 88), (0.07, 84), (0.07, 79)], 0.55, 0.35)
    elif name == 'upset_sfx':
        # A BAD ORDER: two soft notes falling a minor third, rounded off, a correction and not a scolding.
        for at, m, d in ((0.0, 69, 0.14), (0.16, 65, 0.3)):
            nn = int((d + 0.25) * SR)
            x = osc(hz(m), nn, 'square', cutoff=1300.0, cut_mul=lambda tc: 0.4 + 0.6 * np.exp(-tc / 0.12), reso=0.15, max_k=20)
            put(buf, x * adsr(nn, int(d * SR), 0.006, 0.15, 0.6, 0.15), at, 0.0, 0.4)
    elif name == 'synth_swell':
        nn = int(2.8 * SR)
        env = adsr(nn, int(2.2 * SR), 1.2, 2.0, 1.0, 0.6)
        for note in (57, 60, 64, 67, 71):
            for dv in (-9.0, 0.0, 9.0):
                x = osc(hz(note), nn, 'saw', cutoff=3500.0, cut_mul=lambda tc: 0.12 + 0.88 * np.clip(tc / 2.2, 0, 1) ** 2,
                        reso=0.25, detune=dv, max_k=28)
                put(buf, x * env, 0.0, dv / 20.0, 0.05)
    else:
        raise KeyError(name)
    mono = (buf[0].astype(np.float64) + buf[1].astype(np.float64)) * 0.5
    live = np.nonzero(np.abs(mono) > 1e-4)[0]
    mono = mono[:live[-1] + 1] if live.size else mono[:SR // 4]
    p = float(np.max(np.abs(mono)))
    return mono * (0.9 / p) if p > 1e-9 else mono


# ── the songs ────────────────────────────────────────────────────────────────────────────────────────────────────
# key is the tonic's MIDI note in octave 3; prog is scale degrees (0 = the tonic chord) one per bar; a form row is
# (section, bars, parts, (filter open at its start, at its end)). The night plays night_1, 2, ... in order.

SONGS = {
    'night_1': dict(seed=101, bpm=100, key=57, mode='minor', prog=[0, 5, 2, 6], drums='laid', bass='octave', arp=16, lead='saw',
                    form=[('intro', 8, 'pad arp', (0.3, 0.85)), ('verse', 16, 'pad arp bass drums'),
                          ('chorus', 16, 'pad arp bass drums lead'), ('break', 8, 'pad ep', (0.6, 0.6)),
                          ('chorus2', 16, 'pad arp bass drums lead'), ('outro', 8, 'pad arp bass', (0.9, 0.3))]),
    'night_2': dict(seed=202, bpm=108, key=53, mode='minor', prog=[0, 3, 6, 2], drums='disco', bass='octave', arp=8, lead='saw', ep='comp',
                    form=[('intro', 8, 'pad arp drums', (0.4, 0.9)), ('verse', 16, 'pad bass drums ep'),
                          ('chorus', 16, 'pad arp bass drums lead'), ('break', 8, 'pad arp', (0.5, 0.8)),
                          ('chorus2', 16, 'pad arp bass drums lead ep'), ('outro', 4, 'pad arp', (0.8, 0.3))]),
    'night_3': dict(seed=303, bpm=94, key=50, mode='dorian', prog=[0, 3, 2, 6], drums='laid', bass='drive', arp=16, lead='soft', ep='comp',
                    form=[('intro', 8, 'pad ep', (0.5, 0.8)), ('verse', 16, 'pad ep bass drums'),
                          ('chorus', 16, 'pad ep bass drums lead'), ('break', 8, 'pad arp', (0.5, 0.7)),
                          ('chorus2', 16, 'pad ep bass drums lead'), ('outro', 8, 'pad ep', (0.8, 0.4))]),
    'night_4': dict(seed=404, bpm=104, key=52, mode='minor', prog=[0, 2, 5, 6], drums='laid', bass='octave', arp=16, lead='saw',
                    form=[('intro', 8, 'pad arp', (0.25, 0.9)), ('verse', 16, 'pad arp bass drums'),
                          ('chorus', 16, 'pad arp bass drums lead'), ('break', 8, 'pad arp ep', (0.5, 0.6)),
                          ('chorus2', 16, 'pad arp bass drums lead'), ('outro', 8, 'pad arp', (0.9, 0.3))]),
    'night_5': dict(seed=505, bpm=98, key=49, mode='minor', prog=[5, 6, 0, 0], drums='laid', bass='drive', arp=8, lead='saw', ep='comp',
                    form=[('intro', 8, 'pad arp', (0.3, 0.8)), ('verse', 16, 'pad arp bass drums'),
                          ('chorus', 16, 'pad arp bass drums lead'), ('break', 8, 'pad ep', (0.6, 0.6)),
                          ('chorus2', 16, 'pad arp bass drums lead ep'), ('outro', 8, 'pad ep', (0.8, 0.3))]),
    'lastcall_1': dict(seed=606, bpm=84, key=48, mode='minor', prog=[0, 5, 3, 4], drums='soft', bass='long', arp=8, lead='soft', ep='comp',
                       form=[('intro', 4, 'pad ep', (0.5, 0.7)), ('verse', 16, 'pad ep bass drums'),
                             ('chorus', 16, 'pad ep bass drums lead arp'), ('verse2', 8, 'pad ep bass drums'),
                             ('outro', 8, 'pad ep', (0.7, 0.3))]),
    'story_1': dict(seed=707, bpm=72, key=55, mode='minor', prog=[0, 5, 3, 4], drums=None, bass='long', arp=8, lead='soft', ep='ballad',
                    form=[('intro', 4, 'pad ep', (0.4, 0.6)), ('verse', 16, 'pad ep'),
                          ('chorus', 16, 'pad ep arp lead bass'), ('outro', 8, 'pad ep', (0.6, 0.3))]),
    'dayend_1': dict(seed=808, bpm=92, key=58, mode='major', prog=[0, 5, 3, 4], drums='soft', bass='drive', arp=16, lead='soft', ep='comp',
                     form=[('intro', 8, 'pad arp', (0.3, 0.8)), ('verse', 16, 'pad arp bass drums ep'),
                           ('chorus', 16, 'pad arp bass drums lead'), ('outro', 8, 'pad arp ep', (0.8, 0.3))]),
    'dayend_2': dict(seed=909, bpm=88, key=54, mode='minor', prog=[0, 5, 2, 6], drums='soft', bass='long', arp=8, lead=None, ep='comp',
                     form=[('intro', 8, 'pad arp', (0.3, 0.7)), ('verse', 16, 'pad arp ep bass drums'),
                           ('chorus', 16, 'pad arp ep bass drums'), ('outro', 8, 'pad ep', (0.7, 0.3))]),
}


# ── export ───────────────────────────────────────────────────────────────────────────────────────────────────────

def write_wav(path, x):
    q = (np.clip(x, -1.0, 1.0).T * 32767.0).astype(np.int16)
    with wave.open(path, 'wb') as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(q.tobytes())


def encode(wav, name, lufs=-18.0):
    """Two-pass linear loudness normalisation to the game's music level, then Ogg Vorbis into Resources/Audio."""
    import json
    probe = subprocess.run(['ffmpeg', '-hide_banner', '-nostats', '-i', wav, '-af',
                            'loudnorm=I=%.1f:TP=-1.5:LRA=11:print_format=json' % lufs, '-f', 'null', '-'],
                           capture_output=True, text=True, encoding='utf-8', errors='replace').stderr
    m = json.loads(probe[probe.rindex('{'):probe.rindex('}') + 1])
    af = ('loudnorm=I=%.1f:TP=-1.5:LRA=11:measured_I=%s:measured_TP=%s:measured_LRA=%s:measured_thresh=%s:offset=%s:linear=true'
          % (lufs, m['input_i'], m['input_tp'], m['input_lra'], m['input_thresh'], m['target_offset']))
    out = os.path.join(OUT, name + '.ogg')
    r = subprocess.run(['ffmpeg', '-hide_banner', '-nostats', '-y', '-i', wav, '-af', af, '-ar', '44100', '-c:a', 'libvorbis',
                        '-q:a', '6', out], capture_output=True, text=True, encoding='utf-8', errors='replace')
    if r.returncode != 0:
        raise RuntimeError(r.stderr[-500:])
    return out, m['input_i']


def main():
    args = sys.argv[1:]
    max_bars = None
    if '--bars' in args:
        at = args.index('--bars')
        max_bars = int(args[at + 1])
        del args[at:at + 2]
    names = [a for a in args if not a.startswith('--')] or list(SONGS)
    tmp = tempfile.mkdtemp(prefix='lastcall_music_')
    for name in names:
        t0 = time.time()
        print('%s  (%d bpm, %s)' % (name, SONGS[name]['bpm'], SONGS[name]['mode']), flush=True)
        x = render_song(SONGS[name], max_bars, log=lambda s: print(s, flush=True))
        wav = os.path.join(tmp, 'music_%s.wav' % name)
        write_wav(wav, x)
        if max_bars:
            print('  sketch %.1fs of audio in %.0fs -> %s' % (x.shape[1] / SR, time.time() - t0, wav), flush=True)
            continue
        out, lufs_in = encode(wav, 'music_' + name)
        print('  %.1fs of audio in %.0fs, in %s LUFS -> %s' % (x.shape[1] / SR, time.time() - t0, lufs_in, out), flush=True)
    if not max_bars and not [a for a in args if not a.startswith('--')]:
        for f in sorted(os.listdir(OUT)):
            if f.startswith('music_') and f.endswith('.ogg') and f[6:-4] not in SONGS:
                for gone in (f, f + '.meta'):
                    p = os.path.join(OUT, gone)
                    if os.path.exists(p):
                        os.remove(p)
                        print('  removed %s (not a song any more)' % gone)


if __name__ == '__main__':
    main()
