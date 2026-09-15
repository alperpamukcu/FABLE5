# -*- coding: utf-8 -*-
"""LAST CALL's recorded sounds — downloaded CC0 takes cut, voiced and levelled into the bank's own ladder.

Run:  py -3 Tools/sfx_ingest.py <candidates root>            every pick in Tools/sfx_picks.json
      py -3 Tools/sfx_ingest.py <candidates root> click_1 ...  only those
      py -3 Tools/sfx_ingest.py <candidates root> --ledger     rewrite Docs/SES_KAYNAKLARI.md only

WHY (2026-09-15, the author: "Eksik olan sesleri ve müzikleri güncelleyelim internetten ücretsiz cozy, pixel game
soundlardan faydalanabilirsin arkplanda biraz daha 80ler elektronik jazz olmalı rahatlatıcı bir oyun olmalı"). The
synthesised bank (sfx_bank.py) stays the fallback and the house of the level ladder; a name picked here is a
RECORDING now, and sfx_bank skips it unless asked for it by name.

WHAT A PICK SAYS. `src` is a file under the candidates root (where the downloads and their manifest.json live —
the ledger's source pages say how to fetch them again). `mode` is how a clip comes out of a take nobody here has
listened to, so every choice is one the numbers make and anyone can re-run:

  whole    the take, silence trimmed off both ends
  event    one hit out of a take of several: `pick` "loudest" (default), or its index in time order, -1 the last
  steady   a loop: the `seconds`-long window whose level is highest and steadiest, crossfaded into itself

`level` is the bank's ladder (sfx_dsp.LEVELS) — by default the level of the bank clip of the same name (a take
`click_2` takes `click`'s), because a recording that replaces a clip must sit where that clip sat in the mix.
`replaces` names a bank clip a set of takes makes redundant; its .wav and .meta are deleted, because Sfx plays
takes in turn and would never reach the plain name again.

Music (`music`) and ambience beds (`beds`) keep their stereo and leave as Ogg Vorbis, loudness-normalised (music -18
LUFS, beds by their own `lufs`), silence trimmed, beds crossfaded into themselves.
"""
import json
import os
import subprocess
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sfx_dsp import SR, LEVELS, cozy, dc_block, loopify, normalize, write  # noqa
from sfx_bank import BANK  # noqa

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, '..', 'Assets', 'Resources', 'Audio'))
PICKS = os.path.join(HERE, 'sfx_picks.json')
LEDGER = os.path.normpath(os.path.join(HERE, '..', 'Docs', 'SES_KAYNAKLARI.md'))

HOP = 0.005          # envelope frame, seconds


def decode(path, channels=1):
    """Any file ffmpeg reads, as float64 at the bank's rate."""
    r = subprocess.run(['ffmpeg', '-hide_banner', '-nostats', '-i', path, '-ac', str(channels), '-ar', str(SR),
                        '-f', 'f64le', '-'], capture_output=True)
    if r.returncode != 0:
        raise RuntimeError('ffmpeg could not read %s: %s' % (path, r.stderr.decode('utf-8', 'replace')[-400:]))
    x = np.frombuffer(r.stdout, dtype=np.float64)
    return x.reshape(-1, channels) if channels > 1 else x


def envelope_db(x):
    h = max(1, int(SR * HOP))
    n = x.size // h
    if n == 0:
        return np.array([-120.0]), h
    e = np.sqrt(np.mean(x[:n * h].reshape(n, h) ** 2, axis=1) + 1e-12)
    return 20.0 * np.log10(e / max(float(e.max()), 1e-9)), h


def trim(x, floor_db=-48.0, pre=0.004, post=0.03):
    """Silence off both ends: whatever sits more than floor_db under the take's loudest frame."""
    edb, h = envelope_db(x)
    live = np.nonzero(edb > floor_db)[0]
    if live.size == 0:
        return x
    a = max(0, live[0] * h - int(pre * SR))
    b = min(x.size, (live[-1] + 1) * h + int(post * SR))
    return x[a:b]


def events(x, rise_db=-16.0, fall_db=-34.0, max_len=1.2, gap=0.08):
    """The hits in a take, in time order: (start, end, peak dB) in samples. A hit starts where the envelope climbs
    over rise_db (relative to the take's loudest frame) and ends once it has stayed fall_db under its own peak for
    40 ms, or at max_len."""
    edb, h = envelope_db(x)
    out, i, n = [], 0, edb.size
    hold = int(0.04 / HOP)
    while i < n:
        if edb[i] < rise_db:
            i += 1
            continue
        a = i
        while a > 0 and edb[a - 1] < edb[a] and i - a < int(0.03 / HOP):
            a -= 1
        pk = i + int(np.argmax(edb[i:min(n, i + int(0.15 / HOP))]))
        limit = min(n, a + int(max_len / HOP))
        e, quiet = pk, 0
        while e < limit:
            quiet = quiet + 1 if edb[e] < edb[pk] + fall_db else 0
            if quiet >= hold:
                break
            e += 1
        out.append((a * h, min(x.size, (e + 1) * h), float(edb[pk])))
        i = e + int(gap / HOP)
    return out


def steady(x, seconds, margin=0.3):
    """The window whose level is highest and steadiest: frames of 50 ms scored by mean minus twice their spread."""
    frame = int(0.05 * SR)
    n = x.size // frame
    rms = np.sqrt(np.mean(x[:n * frame].reshape(n, frame) ** 2, axis=1) + 1e-12)
    db = 20.0 * np.log10(rms)
    w = max(1, int(seconds / 0.05))
    lo, hi = int(margin / 0.05), n - w - int(margin / 0.05)
    if hi <= lo:
        return x[:int(seconds * SR)]
    best, at = -1e9, lo
    for s in range(lo, hi + 1):
        seg = db[s:s + w]
        score = float(seg.mean() - 2.0 * seg.std())
        if score > best:
            best, at = score, s
    return x[at * frame:(at + w) * frame]


def fades(x, fade_in=0.002, fade_out=0.015):
    n = x.size
    fi, fo = max(2, int(fade_in * SR)), max(2, int(fade_out * SR))
    if fi + fo >= n:
        fi = fo = max(2, n // 4)
    x[:fi] *= 0.5 - 0.5 * np.cos(np.linspace(0, np.pi, fi))
    x[-fo:] *= 0.5 + 0.5 * np.cos(np.linspace(0, np.pi, fo))
    x[0] = x[-1] = 0.0
    return x


def level_of(name, pick):
    if 'level' in pick:
        return pick['level']
    base = name.rsplit('_', 1)[0] if name.rsplit('_', 1)[-1].isdigit() else name
    if base in BANK:
        return BANK[base][1]
    raise KeyError('%s: no level given and no bank clip called %s' % (name, base))


def clip(root, name, pick):
    x = decode(os.path.join(root, pick['src']))
    if 'start' in pick or 'end' in pick:
        x = x[int(pick.get('start', 0) * SR):int(pick['end'] * SR) if 'end' in pick else None]
    mode = pick.get('mode', 'whole')
    loop = False
    if mode == 'whole':
        x = trim(x)
    elif mode == 'event':
        # `min_len` drops the ticks a quiet take is full of: a wipe or a set-down is a gesture, not a click.
        evs = [e for e in events(x, max_len=pick.get('max_len', 1.2))
               if e[1] - e[0] >= pick.get('min_len', 0.0) * SR]
        if not evs:
            raise RuntimeError('%s: no hit found in %s' % (name, pick['src']))
        which = pick.get('pick', 'loudest')
        a, b, _ = max(evs, key=lambda e: e[2]) if which == 'loudest' else evs[int(which)]
        x = trim(x[a:b])
    elif mode == 'steady':
        x = steady(x, pick.get('seconds', 2.0))
        loop = True
    elif mode == 'window':
        # the steadiest stretch as a one-shot, faded at both ends: a fizz or a curtain has no hit to find
        x = steady(x, pick.get('seconds', 1.0))
    else:
        raise ValueError('%s: unknown mode %s' % (name, mode))
    if not loop:
        # Three silent milliseconds in front, so the fade-in lands on silence and not on the hit: a Kenney click
        # starts on its very first sample, and the fade took 4 dB off it.
        x = np.concatenate([np.zeros(int(0.003 * SR)), x])
    x = dc_block(cozy(x))
    x = normalize(x, LEVELS[level_of(name, pick)] + pick.get('gain_db', 0.0))
    if loop:
        x = loopify(x, crossfade=pick.get('crossfade', 0.12))
        x = fades(x, 0.004, 0.004)
    else:
        x = fades(x)
    return x


def ogg(root, name, pick, lufs, loop):
    src = os.path.join(root, pick['src'])
    trim_af = ('silenceremove=start_periods=1:start_threshold=-55dB:start_silence=0.1,areverse,'
               'silenceremove=start_periods=1:start_threshold=-55dB:start_silence=0.1,areverse')
    if 'start' in pick or 'end' in pick:
        trim_af = 'atrim=start=%s%s,asetpts=PTS-STARTPTS,' % (pick.get('start', 0), (':end=%s' % pick['end']) if 'end' in pick else '') + trim_af
    r = subprocess.run(['ffmpeg', '-hide_banner', '-nostats', '-i', src, '-af',
                        trim_af + ',loudnorm=I=%.1f:TP=-1.5:LRA=11:print_format=json' % lufs, '-f', 'null', '-'],
                       capture_output=True, text=True, encoding='utf-8', errors='replace')
    t = r.stderr
    m = json.loads(t[t.rindex('{'):t.rindex('}') + 1])
    af = trim_af + (',loudnorm=I=%.1f:TP=-1.5:LRA=11:measured_I=%s:measured_TP=%s:measured_LRA=%s:measured_thresh=%s'
                    ':offset=%s:linear=true' % (lufs, m['input_i'], m['input_tp'], m['input_lra'], m['input_thresh'],
                                               m['target_offset']))
    if loop:
        # a bed crossfades its own tail over its head, so the loop point is not a splice
        xf = pick.get('crossfade', 1.5)
        af += (',asplit[a][b];[a]atrim=start=%s,asetpts=PTS-STARTPTS[body];[b]atrim=end=%s,asetpts=PTS-STARTPTS[head];'
               '[body][head]acrossfade=d=%s:c1=tri:c2=tri' % (xf, xf, xf))
    out = os.path.join(OUT, name + '.ogg')
    r2 = subprocess.run(['ffmpeg', '-hide_banner', '-nostats', '-y', '-i', src, '-filter_complex', af, '-ar', '44100',
                         '-ac', '2', '-c:a', 'libvorbis', '-q:a', '6', out],
                        capture_output=True, text=True, encoding='utf-8', errors='replace')
    if r2.returncode != 0:
        raise RuntimeError('%s: %s' % (name, r2.stderr[-500:]))
    print('  %-20s %s  in %s LUFS -> %s' % (name, pick['src'], m['input_i'], lufs))


def manifests(root):
    rows = {}
    for sub in ('music', 'sfx', 'ambience'):
        p = os.path.join(root, sub, 'manifest.json')
        if not os.path.exists(p):
            continue
        for e in json.load(open(p, encoding='utf-8')):
            f = e.get('file', '')
            key = (sub + '/' + (f if '/' in f.replace('\\', '/') or sub == 'music' else os.path.join(e.get('game_name', ''), f))).replace('\\', '/')
            rows[key] = e
            rows[(sub + '/' + os.path.basename(f)).replace('\\', '/')] = e
    return rows


def ledger(root, picks):
    rows = manifests(root)
    lines = ['# LAST CALL — Ses kaynakları (kayıtlar ve ortam)', '',
             '*`Tools/sfx_ingest.py --ledger` yazar; elle düzenleme. Buradaki her dosya CC0 / kamu malı; atıf gerekmiyor,',
             'ama sahiplerinin adı teşekkür için burada duruyor. Sentetik klipler (`Tools/sfx_bank.py`), şarkılar ve müzikal',
             'işaretler (oyunun kendi bestesi, `Tools/music_synth.py`) burada yok.*', '',
             '| Oyundaki dosya | Kaynak | Sahibi | Lisans | Lisansın yazdığı sayfa |', '|---|---|---|---|---|']
    for section in ('music', 'beds', 'clips'):
        for name, pick in sorted(picks.get(section, {}).items()):
            src = pick['src'].replace('\\', '/')
            e = rows.get(src) or rows.get(src.split('/')[0] + '/' + os.path.basename(src)) or {}
            who = e.get('artist') or e.get('author') or '?'
            title = e.get('title') or e.get('source_title') or e.get('why') or os.path.basename(src)
            page = e.get('source_page', '?')
            lines.append('| `%s` | [%s](%s) | %s | %s | %s |' % (
                name, str(title).replace('|', '/')[:70], page, who, e.get('licence', '?'), e.get('licence_proof_url', '?')))
    open(LEDGER, 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
    print('ledger -> %s' % LEDGER)


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    root = sys.argv[1]
    want = [a for a in sys.argv[2:] if not a.startswith('--')]
    picks = json.load(open(PICKS, encoding='utf-8'))
    if '--ledger' in sys.argv:
        ledger(root, picks)
        return
    for name, pick in picks.get('clips', {}).items():
        if want and name not in want:
            continue
        x = clip(root, name, pick)
        row = write(os.path.join(OUT, name + '.wav'), x, name)
        print('  %-20s %5.2fs  peak %6.1f dB  rms %6.1f dB  <- %s' % (name, row['seconds'], row['peak_db'], row['rms_db'], pick['src']))
        gone = pick.get('replaces')
        if gone:
            for ext in ('.wav', '.wav.meta'):
                p = os.path.join(OUT, gone + ext)
                if os.path.exists(p):
                    os.remove(p)
                    print('  removed %s%s (replaced by takes)' % (gone, ext))
    for name, pick in picks.get('music', {}).items():
        if not want or name in want:
            ogg(root, name, pick, pick.get('lufs', -18.0), loop=False)
    for name, pick in picks.get('beds', {}).items():
        if not want or name in want:
            ogg(root, name, pick, pick.get('lufs', -26.0), loop=True)
    ledger(root, picks)


if __name__ == '__main__':
    main()
