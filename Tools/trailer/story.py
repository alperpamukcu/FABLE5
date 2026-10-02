# -*- coding: utf-8 -*-
"""THE STORY CUT (2026-10-02, v3). The author asked for a story, not a reel: Roxy as a narrator dropping in between
the beats, a different music, words that move, a camera that only moves for a reason, and a proper pixel-art end.

The rules come from the research in Docs/TRAILER.md (Derek Lieu's gameplay-trailer writing, Steam's muted autoplay,
the genre's own trailers):
  - gameplay in the first second, the sign at the music's drop, the call at the very end;
  - "tell, show": a line of Roxy's opens a pillar, the footage under it proves it;
  - the camera never drifts: a shot is WIDE (the game at 1:1) or a DETAIL (the same picture at exactly 2x, nearest
    neighbour, cut to - never zoomed into), and nothing moves while there is something to read;
  - the game's own sound is on: every mark in a film brings its effect in (the pour, the shake, the stamp, the door);
  - one thing to read at a time, eight words at most.

    python3 Tools/trailer/story.py                 cuts/story_60.json, 16:9 and 9:16
    python3 Tools/trailer/story.py --wide          16:9 only
    python3 Tools/trailer/story.py --lang tr       Roxy's lines and the call in Turkish

A segment: {"take": [["S02_serve_*", "first drop", 0.3], ["S01_roxy_serve", "first drop", 0.3]], "sec": 2.3,
            "camera": "detail", "focus": [0.45, 0.55], "speed": 1.0, "say": "roxy_card", "sign": true}
take: candidates in order - a film (or film* for any take), a mark in it, and how many seconds before the mark to
      start (negative starts after it); the first candidate that exists is used.
end: true makes the end card: the S08 frame (or the last candidate) with the sign switching on and the call.
"""
import fnmatch
import json
import math
import os
import shutil
import subprocess
import sys

import numpy as np
from PIL import Image

import edit as E
import overlays as O

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = E.ROOT
AUDIO = E.AUDIO
FPS = 60
WORK = os.path.join(HERE, 'out', 'work_story')

# the game's own sounds, brought in by the marks a film carries: (file, gain, seconds after the mark)
SFX = {
    'first drop': [('pour_tin', 0.9, 0.0)],
    'cellar open': [('cellar_open', 0.8, -0.3)],
    'cellar': [('cellar_open', 0.8, -0.3)],
    'card open': [('id_card', 0.9, -0.4)],
    'shake': [('shake_loop', 0.8, 0.0)],
    'stir': [('stir_loop', 0.8, 0.0)],
    'capped': [('cap_on', 0.9, -0.1)],
    'pour out': [('pour_glass', 0.9, 0.0)],
    'garnish ice': [('ice_drop', 0.9, 0.2)],
    'served': [('serve_clink_1', 0.9, -0.1), ('verdict_good', 0.7, 0.5), ('cash', 0.6, 1.2)],
    'pull': [('tap_pull', 0.9, 0.0)],
    'head': [('head_settle', 0.8, 0.0)],
    'kicked': [('kick_out', 1.0, -0.1), ('door', 0.7, 0.6)],
    'slip': [('printer_feed', 0.9, 0.0), ('bill_star', 0.8, 2.0), ('stamp', 1.0, 3.6)],
    'market': [('menu_open', 0.8, -0.2)],
    'bought': [('buy', 0.9, -0.2)],
    'roxy walks in': [('door', 0.6, -0.6)],
}
for k in range(1, 11):
    SFX['bottle %d' % k] = [('bottle_set_%d' % (1 + (k - 1) % 3), 0.8, -0.1)]
for k in range(0, 7):
    SFX['room %d' % k] = [('level_up', 0.55, 0.0)] if k else []


def films():
    return E.FILMS


def find_take(cands):
    """(film, start seconds, marks) for the first candidate that exists."""
    for c in cands:
        name, mark, lead = c[0], c[1], (c[2] if len(c) > 2 else 0.0)
        names = []
        if os.path.isdir(films()):
            all_ = sorted(f[:-4] for f in os.listdir(films()) if f.endswith('.mp4'))
            names = [n for n in all_ if fnmatch.fnmatch(n, name)]
        for n in names:
            marks = E.marks_of(n)
            hit = [m['t'] for m in marks if m['mark'] == mark] if mark else [0.0]
            if hit:
                return n, max(0.0, hit[0] - lead), marks
    return None


def picture(i, seg, dur, ow, oh, tall):
    """The segment's moving picture, no words: WIDE or DETAIL, never a drift."""
    out = os.path.join(WORK, 'pic_%dx%d_%02d.mp4' % (ow, oh, i))
    take = find_take(seg.get('take', []))
    if take is None:
        return None, None
    film, start, _ = take
    speed = float(seg.get('speed', 1.0))
    detail = seg.get('camera', 'wide') == 'detail'
    fx, fy = seg.get('tall_focus', seg.get('focus', [0.5, 0.5])) if tall else seg.get('focus', [0.5, 0.5])
    k = 2 if (detail or tall) else 1                              # whole-number scales only; 9:16 crops at 2x
    sw, sh = E.SRC_W * k, E.SRC_H * k
    chain = ('[0:v]fps=%d,setpts=(PTS-STARTPTS)/%f,scale=%d:%d:flags=neighbor,'
             'crop=%d:%d:%d:%d,tpad=stop_mode=clone:stop_duration=6,trim=duration=%f,setsar=1' %
             (FPS, speed, sw, sh, ow, oh, int(max(0, min(sw - ow, fx * sw - ow / 2))),
              int(max(0, min(sh - oh, fy * sh - oh / 2))), dur))
    if seg.get('dim'):
        chain += ',eq=brightness=-0.22:saturation=0.8'
    E.run(['ffmpeg', '-y', '-v', 'error', '-ss', '%.3f' % start, '-t', '%.3f' % (dur * speed + 0.3),
           '-i', os.path.join(films(), film + '.mp4'), '-filter_complex', chain + '[v]', '-map', '[v]', '-an',
           '-r', str(FPS), '-frames:v', str(int(round(dur * FPS))), '-c:v', 'libx264', '-preset', 'medium',
           '-crf', '12', '-pix_fmt', 'yuv420p', out])
    return out, take


def still_of(seg, ow, oh, tall):
    """The end card's picture: one frame of the take, at the frame's own scale."""
    take = find_take(seg.get('take', []))
    if take is None:
        return None
    film, start, _ = take
    png = os.path.join(WORK, 'end_%dx%d.png' % (ow, oh))
    k = 2 if tall else 1
    fx = seg.get('focus', [0.5, 0.5])[0]
    sw, sh = E.SRC_W * k, E.SRC_H * k
    E.run(['ffmpeg', '-y', '-v', 'error', '-ss', '%.3f' % start, '-i', os.path.join(films(), film + '.mp4'),
           '-frames:v', '1', '-vf', 'scale=%d:%d:flags=neighbor,crop=%d:%d:%d:%d' %
           (sw, sh, ow, oh, int(max(0, min(sw - ow, fx * sw - ow / 2))), (sh - oh) // 2), png])
    return png


def sign_image(width):
    sys.path.insert(0, os.path.join(ROOT, 'Tools', 'steam_page'))
    import keyart
    return keyart.title(width).convert('RGBA')


def overlay(base, layers, out, dur):
    """Lays PNG sequences over a picture: layers = [(dir, x, y, start seconds)]."""
    if not layers:
        shutil.copy(base, out)
        return out
    inputs, chain, last = ['-i', base], [], '0:v'
    for n, (d, x, y, st) in enumerate(layers, 1):
        inputs += ['-framerate', str(FPS), '-i', os.path.join(d, '%04d.png')]
        chain.append('[%d:v]setpts=PTS-STARTPTS+%f/TB[o%d]' % (n, st, n))
        chain.append('[%s][o%d]overlay=%d:%d:eof_action=pass[l%d]' % (last, n, x, y, n))
        last = 'l%d' % n
    E.run(['ffmpeg', '-y', '-v', 'error'] + inputs + ['-filter_complex', ';'.join(chain), '-map', '[%s]' % last,
           '-t', '%.3f' % dur, '-r', str(FPS), '-c:v', 'libx264', '-preset', 'medium', '-crf', '12',
           '-pix_fmt', 'yuv420p', out])
    return out


def build(cut_name='story_60', lang='en', tall=False):
    cut = json.load(open(os.path.join(HERE, 'cuts', cut_name + '.json')))
    lines_all = json.load(open(os.path.join(HERE, 'captions.json')))
    lines = dict(lines_all.get('en', {}))
    lines.update(lines_all.get(lang, {}))
    os.makedirs(WORK, exist_ok=True)
    ow, oh = (1080, 1920) if tall else (1920, 1080)
    period, _ = E.beat_grid(cut['music'], 0.0)
    music_from = cut.get('music_from', 0.0)
    print('%s %s: %s from %.1f s (drop at %.1f s), beat %.3f s' %
          (cut_name, 'tall' if tall else 'wide', cut['music'], music_from, cut.get('drop_at', 0), period))

    parts, sounds, t = [], [], 0.0
    for i, seg in enumerate(cut['segments']):
        dur = max(period, round(seg['sec'] / period) * period)      # every cut on a beat
        if seg.get('end'):
            png = still_of(seg, ow, oh, tall)
            if png is None:
                print('  ! end card has no picture')
                continue
            base = os.path.join(WORK, 'endbase_%dx%d.mp4' % (ow, oh))
            E.run(['ffmpeg', '-y', '-v', 'error', '-loop', '1', '-framerate', str(FPS), '-t', '%.3f' % dur, '-i', png,
                   '-vf', 'eq=brightness=-0.10,fade=t=in:st=0:d=0.25,setsar=1', '-r', str(FPS), '-c:v', 'libx264',
                   '-crf', '12', '-pix_fmt', 'yuv420p', base])
            layers = []
            sw = 840 if not tall else 840
            sign = sign_image(sw)
            sd = O.sign_on(sign, dur, os.path.join(WORK, 'sign_end_%d' % i))
            layers.append((sd, (ow - sign.width) // 2, int(oh * (0.06 if not tall else 0.08)), 0.3))
            face = None if lang == 'en' else E.face_for(lang, lines.get('wishlist', ''))
            cd, (cx, cy) = O.title(lines.get('wishlist', 'WISHLIST ON STEAM'), dur - 1.2, os.path.join(WORK, 'cta_%d' % i),
                                   ow, oh, lang_face=face, y_frac=0.40 if not tall else 0.32, max_scale=3, band=True)
            layers.append((cd, cx, cy, 1.2))
            if lines.get('when'):
                wd, (wx, wy) = O.title(lines['when'], dur - 1.8, os.path.join(WORK, 'when_%d' % i), ow, oh,
                                       lang_face=face, y_frac=0.50 if not tall else 0.38, max_scale=2, band=True)
                layers.append((wd, wx, wy, 1.8))
            if seg.get('say'):
                nd, (nx, ny), bl = O.narrator(lines[seg['say']], dur - 2.6, os.path.join(WORK, 'say_%d' % i), ow, oh)
                layers.append((nd, nx, ny, 2.6))
                sounds += [('key_press', 0.35, t + 2.6 + b) for b in bl]
            sounds.append(('synth_swell', 0.8, t + 0.2))
            parts.append(overlay(base, layers, os.path.join(WORK, 'seg_%dx%d_%02d.mp4' % (ow, oh, i)), dur))
            t += dur
            continue

        pic, take = picture(i, seg, dur, ow, oh, tall)
        if pic is None:
            print('  - segment %d skipped: nothing filmed for %s' % (i, seg.get('take')))
            continue
        film, start, marks = take
        speed = float(seg.get('speed', 1.0))
        layers = []
        if seg.get('say'):
            nd, (nx, ny), bl = O.narrator(lines[seg['say']], dur, os.path.join(WORK, 'say_%d' % i), ow, oh,
                                          top=seg.get('box') == 'top')
            layers.append((nd, nx, ny, 0.0))
            sounds += [('key_press', 0.35, t + b) for b in bl]
        if seg.get('sign'):
            sign = sign_image(840 if not tall else 840)
            sd = O.sign_on(sign, dur, os.path.join(WORK, 'sign_%d' % i))
            layers.append((sd, (ow - sign.width) // 2, (oh - sign.height) // 2 - (40 if not tall else 200), 0.0))
            sounds.append(('synth_swell', 0.8, t))
        parts.append(overlay(pic, layers, os.path.join(WORK, 'seg_%dx%d_%02d.mp4' % (ow, oh, i)), dur))
        # the game's own sound for every mark inside the window this segment shows
        for m in marks:
            rel = (m['t'] - start) / speed
            for name, gain, off in SFX.get(m['mark'], []):
                at = rel + off
                if 0 <= at < dur:
                    sounds.append((name, gain, t + at))
        for name, gain, at in seg.get('sfx', []):
            sounds.append((name, gain, t + at))
        t += dur
    total = t

    listing = os.path.join(WORK, 'parts_%dx%d.txt' % (ow, oh))
    with open(listing, 'w') as f:
        for p in parts:
            f.write("file '%s'\n" % p)
    pic = os.path.join(WORK, 'picture_%dx%d.mp4' % (ow, oh))
    E.run(['ffmpeg', '-y', '-v', 'error', '-f', 'concat', '-safe', '0', '-i', listing, '-c', 'copy', pic])

    a_in = ['-ss', '%.3f' % music_from, '-t', '%.3f' % total, '-i', os.path.join(AUDIO, cut['music'] + '.ogg')]
    filt = ['[1:a]volume=0.85,afade=t=out:st=%f:d=1.2[m]' % (total - 1.2)]
    mix = ['[m]']
    for k, (name, gain, at) in enumerate(sounds):
        path = os.path.join(AUDIO, name + '.wav')
        if not os.path.exists(path):
            continue
        a_in += ['-i', path]
        n = len(mix) + 1
        filt.append('[%d:a]atrim=0:2.5,adelay=%d|%d,volume=%f[s%d]' % (n, int(at * 1000), int(at * 1000), gain, k))
        mix.append('[s%d]' % k)
    filt.append('%samix=inputs=%d:normalize=0:duration=first,loudnorm=I=-14:TP=-1.5:LRA=11[a]' % (''.join(mix), len(mix)))
    os.makedirs(os.path.join(HERE, 'out'), exist_ok=True)
    final = os.path.join(HERE, 'out', cut_name + ('' if lang == 'en' else '_' + lang) + ('_tall' if tall else '') + '.mp4')
    E.run(['ffmpeg', '-y', '-v', 'error', '-i', pic] + a_in +
          ['-filter_complex', ';'.join(filt), '-map', '0:v', '-map', '[a]',
           '-c:v', 'libx264', '-preset', 'slow', '-crf', '16', '-profile:v', 'high', '-pix_fmt', 'yuv420p',
           '-r', str(FPS), '-c:a', 'aac', '-b:a', '320k', '-ar', '48000', '-movflags', '+faststart', '-shortest', final])
    print('  -> %s (%.1f s, %.1f MB)' % (os.path.relpath(final, ROOT), total, os.path.getsize(final) / 1e6))
    return final


def main(argv):
    lang = argv[argv.index('--lang') + 1] if '--lang' in argv else 'en'
    names = [a for a in argv if not a.startswith('--') and a != lang] or ['story_60']
    shapes = [False, True]
    if '--wide' in argv:
        shapes = [False]
    if '--tall' in argv:
        shapes = [True]
    for n in names:
        for tall in shapes:
            build(n, lang, tall)


if __name__ == '__main__':
    main(sys.argv[1:])
