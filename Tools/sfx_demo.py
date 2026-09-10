# -*- coding: utf-8 -*-
"""ONE FILE TO JUDGE THE BANK BY (2026-09-10).

Seventy-three clips is not something anyone can hold in their head by opening seventy-three
files. This strings a representative walk through a night together — the interface, the
cellar, the pour, the shake, the serve, the till, the room — with a beat of silence between
each so nothing masks the thing after it, and writes one WAV.

It reads the built bank; it never synthesises. So what you hear here is exactly what the
game will play, at exactly the level the game will play it: the clips are NOT re-levelled,
because the level ladder (sfx_dsp.LEVELS) is half the design and a demo that flattens it
would be lying about the part most worth judging.

  py -3 -X utf8 Tools/sfx_demo.py [source_dir] [out.wav]
"""
import os
import sys
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
SR = 44100

# A night, in order. Loops are cut to a couple of seconds so the walk keeps moving.
WALK = [
    ('day_open', 0.55), ('ambience_loop', 3.0), ('click', 0.30), ('hover', 0.30),
    ('id_card', 0.45), ('voice_order', 0.55), ('page_turn', 0.35),
    ('cellar_open', 0.55), ('bottle_open', 0.40), ('pour_tin', 2.0),
    ('cap_on', 0.40), ('shake_loop', 2.0), ('tin_tip', 0.40), ('pour_glass', 1.8),
    ('ice_drop', 0.40), ('garnish', 0.35), ('rim_turn', 1.4), ('rim_done', 0.45),
    ('glass_down', 0.45), ('serve_clink', 0.70), ('voice_happy', 0.80),
    ('tap_pull', 1.8), ('head_settle', 0.60), ('coin', 0.45), ('cash', 0.80),
    ('star_earn', 0.80), ('door', 0.80), ('stool_take', 0.70), ('bill_slip', 0.50),
    ('stamp', 0.55), ('last_call_bell', 1.2), ('day_close', 1.2),
]


def read(path):
    w = wave.open(path, 'rb')
    n = w.getnframes()
    x = np.frombuffer(w.readframes(n), dtype=np.int16).astype(np.float64) / 32768.0
    w.close()
    return x


def main(src, out):
    gap = int(0.28 * SR)
    parts = []
    missing = []
    for name, seconds in WALK:
        p = os.path.join(src, name + '.wav')
        if not os.path.exists(p):
            missing.append(name)
            continue
        x = read(p)
        want = int(seconds * SR)
        if x.size > want:
            # A loop cut short still has to end at zero, or the demo pops where the
            # bank cannot: fade the cut over 40 ms.
            x = x[:want].copy()
            f = min(int(0.04 * SR), x.size // 2)
            x[-f:] *= np.linspace(1.0, 0.0, f)
        parts.append(x)
        parts.append(np.zeros(gap))
    if missing:
        print('  missing:', ', '.join(missing))
    y = np.concatenate(parts) if parts else np.zeros(SR)
    y[0] = 0.0
    y[-1] = 0.0
    q = np.clip(np.round(y * 32767.0), -32768, 32767).astype(np.int16)
    w = wave.open(out, 'wb')
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes(q.tobytes())
    w.close()
    print('demo -> %s  (%.1f s, %d clips)' % (out, q.size / float(SR), len(parts) // 2))


if __name__ == '__main__':
    src = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, 'sfx_v2')
    out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(HERE, 'sfx_demo_new.wav')
    main(src, out)
