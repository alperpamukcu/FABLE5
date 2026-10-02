# -*- coding: utf-8 -*-
"""THE TRAILER'S EDIT (2026-10-02). Cuts the footage the game films of itself (LastCall -> Trailer -> Record Shots,
Recordings/Trailer/<shot>.mp4 + .json) into trailers, on the beat of the game's own music.

The author's brief (2026-10-02): at most a minute, cut like a vertical short - fast cuts, the picture never still,
hook the player in the first second. So every segment is a few BEATS long (a beat is ~0.5 s at the track's 118 BPM)
and every segment MOVES: a push-in across its length, a punch-in on its first frames, a white flash on the hard
cuts, and footage sped up where the hand would otherwise wait.

    python3 Tools/trailer/edit.py                      every cut in Tools/trailer/cuts/*.json, 16:9 and 9:16
    python3 Tools/trailer/edit.py hook_60              one cut
    python3 Tools/trailer/edit.py hook_60 --lang tr    its captions in another language (captions.json)
    python3 Tools/trailer/edit.py hook_60 --wide       only the 16:9 (Steam) version
    python3 Tools/trailer/edit.py hook_60 --tall       only the 9:16 (Shorts / TikTok / Reels) version

A segment:

    {"film": "S02_serve_*", "at": "mark:shake", "lead": 0.2, "beats": 2,
     "speed": 1.3, "zoom": [1.0, 1.08], "punch": 0.12, "flash": true, "focus": [0.5, 0.45],
     "caption": "shake", "still": "Tools/steam_page/out/screenshots/screenshot_02_1920x1080.png"}

film:    a shot in Recordings/Trailer. "S02_serve_*" is any take of that shot; `pick` takes the n-th take that
         carries the mark. "at" is seconds or "mark:<name>" from the shot's .json, "lead" seconds before it.
still:   stands in when the film is not there yet, so the cut can be watched (an animatic) before anything is shot.
speed:   how fast the footage runs (1.3 = 30% faster).
zoom:    the push across the segment, from and to; punch: extra zoom on the first frames, eased away in 0.18 s.
flash:   the segment opens out of white (a hard-cut accent, every second or third cut, never all of them).
focus:   where in the frame the zoom (and the 9:16 crop) is centred, as fractions of width and height.
optional: drop the segment from a filmed cut when no take carries its mark (the animatic still shows its still).
caption: a key in captions.json, drawn in the game's display face (Malibu Arcade, whole-number scale) and popped in.

Out: Tools/trailer/out/<cut>[_<lang>].mp4 at 1920x1080 and <cut>[_<lang>]_tall.mp4 at 1080x1920 - 60 fps, H.264 High,
CRF 16, AAC 320k, -14 LUFS, faststart. The 16:9 file is what Steam asks for (Steamworks: 1920x1080, 60 fps, H.264 +
AAC, 5000+ kbps); the 9:16 one is for the vertical platforms.
"""
import fnmatch
import json
import math
import os
import subprocess
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
FILMS = os.path.join(ROOT, 'Recordings', 'Trailer')
AUDIO = os.path.join(ROOT, 'Assets', 'Resources', 'Audio')
ARCADE = os.path.join(ROOT, 'Assets', 'Fonts', 'MalibuArcade-Regular.ttf')
OUT = os.path.join(HERE, 'out')
WORK = os.path.join(OUT, 'work')
FPS = 60
SRC_W, SRC_H = 1920, 1080

CREAM, SHADOW, AMBER, MAGENTA = (242, 232, 213), (13, 8, 19), (232, 163, 61), (232, 77, 166)


def run(cmd):
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode != 0:
        sys.exit('ffmpeg failed:\n' + ' '.join(cmd) + '\n' + r.stderr[-3000:])
    return r


# ── the music: tempo and the first beat ──────────────────────────────────────────────────────────────

def beat_grid(name, start):
    """(seconds per beat, first beat at or after `start`) measured from the track's own onsets."""
    path = os.path.join(AUDIO, name + '.ogg')
    raw = subprocess.run(['ffmpeg', '-v', 'error', '-i', path, '-ac', '1', '-ar', '11025', '-f', 'f32le', '-'],
                         capture_output=True).stdout
    x = np.frombuffer(raw, np.float32)
    hop, sr = 128, 11025
    n = len(x) // hop
    e = np.sqrt((x[:n * hop].reshape(n, hop) ** 2).mean(1))
    on = np.maximum(0, np.diff(np.log(e + 1e-4)))
    fr = sr / hop
    seg = on[: int(60 * fr)] - on[: int(60 * fr)].mean()
    ac = np.correlate(seg, seg, 'full')[len(seg) - 1:]
    lags = np.arange(len(ac))
    ok = (lags > fr * 60 / 170) & (lags < fr * 60 / 80)
    lag = lags[ok][np.argmax(ac[ok])]
    best, period = -1, lag / fr
    for p in np.linspace(lag - 1.5, lag + 1.5, 61):
        s = on[(np.arange(0, len(on) - 1, p)).astype(int)].sum()
        if s > best:
            best, period = s, p / fr
    phases = np.linspace(0, period, 48, endpoint=False)
    scores = [on[(np.arange(ph, len(on) / fr - 1, period) * fr).astype(int)].sum() for ph in phases]
    phase = phases[int(np.argmax(scores))]
    first = phase + math.ceil(max(0.0, start - phase) / period) * period
    return period, first


# ── words on screen ──────────────────────────────────────────────────────────────────────────────────

def face_for(lang, text):
    if lang == 'en':
        return ARCADE, 16
    sys.path.insert(0, os.path.join(ROOT, 'Tools', 'steam_page'))
    import kit
    path, size, _ = kit.heading_face(lang, text)
    return path, size


def pixel_words(text, lang, ink=CREAM, rule=True):
    """The words at the face's design size: hard-edged, a one-texel shadow, an amber rule under them."""
    font_path, size = face_for(lang, text)
    font = ImageFont.truetype(font_path, size)
    pad = 3
    box = font.getbbox(text)
    tw, th = box[2] - box[0], box[3] - box[1]
    small = Image.new('RGBA', (tw + 2 * pad + 1, th + 2 * pad + (5 if rule else 1)), (0, 0, 0, 0))
    d = ImageDraw.Draw(small)
    org = (pad - box[0], pad - box[1])
    d.text((org[0] + 1, org[1] + 1), text, font=font, fill=SHADOW + (255,))
    d.text(org, text, font=font, fill=ink + (255,))
    a = np.asarray(small).copy()
    a[..., 3] = np.where(a[..., 3] >= 128, 255, 0)
    small = Image.fromarray(a, 'RGBA')
    if rule:
        d = ImageDraw.Draw(small)
        ry = pad + th + 2
        d.line([(pad, ry), (pad + tw - 1, ry)], fill=AMBER + (255,))
        d.line([(pad + 1, ry + 1), (pad + tw, ry + 1)], fill=SHADOW + (255,))
    return small


def caption_png(text, lang, path, max_w):
    """A tight caption plate (words on a dark band), at the largest whole scale under max_w; popped in by ffmpeg."""
    lines = text.split('\n')
    smalls = [pixel_words(l, lang) for l in lines]
    w = max(s.width for s in smalls)
    scale = max(1, min(5, max_w // w))
    bigs = [s.resize((s.width * scale, s.height * scale), Image.NEAREST) for s in smalls]
    gap = 4 * scale
    bw = max(b.width for b in bigs) + 16 * scale
    bh = sum(b.height for b in bigs) + gap * (len(bigs) - 1) + 8 * scale
    plate = Image.new('RGBA', (bw, bh), SHADOW + (170,))
    y = 4 * scale
    for b in bigs:
        plate.alpha_composite(b, ((bw - b.width) // 2, y))
        y += b.height + gap
    plate.save(path)
    return plate.size


def end_card(path, lang, wishlist, when, tall):
    """The key art's master composition (keyart.py 'trailer_end' / 'trailer_end_tall') with the call to action."""
    sys.path.insert(0, os.path.join(ROOT, 'Tools', 'steam_page'))
    import keyart
    img, boxes = keyart.build('trailer_end_tall' if tall else 'trailer_end')
    img = img.convert('RGBA')
    tx0, ty0, tx1, ty1 = boxes['title']
    y = ty1 + 36
    for text, most in ((wishlist, 4), (when, 3)):
        if not text:
            continue
        s = pixel_words(text, lang, rule=False)
        scale = max(1, min(most, (tx1 - tx0) // s.width))
        big = s.resize((s.width * scale, s.height * scale), Image.NEAREST)
        img.alpha_composite(big, (tx0 + (tx1 - tx0 - big.width) // 2, y))
        y += big.height + 16
    img.convert('RGB').save(path)


# ── footage ──────────────────────────────────────────────────────────────────────────────────────────

def marks_of(shot):
    path = os.path.join(FILMS, shot + '.json')
    return json.load(open(path)).get('marks', []) if os.path.exists(path) else []


def resolve_film(seg):
    name = seg.get('film')
    if not name or not os.path.isdir(FILMS):
        return None
    if '*' not in name:
        return name if os.path.exists(os.path.join(FILMS, name + '.mp4')) else None
    takes = sorted(f[:-4] for f in os.listdir(FILMS) if f.endswith('.mp4') and fnmatch.fnmatch(f[:-4], name))
    at = seg.get('at', 0)
    want = at[5:] if isinstance(at, str) and at.startswith('mark:') else None
    fits = [t for t in takes if want is None or any(m['mark'] == want for m in marks_of(t))]
    k = int(seg.get('pick', 1)) - 1
    return fits[k] if k < len(fits) else (fits[-1] if fits else None)


def film_start(seg, shot):
    at = seg.get('at', 0)
    if isinstance(at, str) and at.startswith('mark:'):
        want = at[5:]
        hit = [m['t'] for m in marks_of(shot) if m['mark'] == want]
        if not hit:
            print('  ! %s has no mark "%s", starting at 0' % (shot, want))
        at = hit[0] if hit else 0.0
    return max(0.0, float(at) - float(seg.get('lead', 0)))


def render_segment(i, seg, dur, captions, lang, ow, oh):
    """One segment at ow x oh: the footage (or its still), covered, pushed in, punched, flashed, captioned."""
    out = os.path.join(WORK, 'seg_%dx%d_%02d.mp4' % (ow, oh, i))
    speed = float(seg.get('speed', 1.0))
    z0, z1 = seg.get('zoom', [1.0, 1.07])
    punch = float(seg.get('punch', 0.0))
    fx, fy = seg.get('focus', [0.5, 0.5])
    film = resolve_film(seg)
    inputs = []
    if film:
        src = ['-ss', '%.3f' % film_start(seg, film), '-t', '%.3f' % (dur * speed + 0.2),
               '-i', os.path.join(FILMS, film + '.mp4')]
        head = '[0:v]fps=%d,setpts=(PTS-STARTPTS)/%f' % (FPS, speed)
    elif seg.get('still'):
        src = ['-loop', '1', '-framerate', str(FPS), '-t', '%.3f' % (dur + 0.2), '-i', os.path.join(ROOT, seg['still'])]
        head = '[0:v]scale=%d:%d:flags=neighbor' % (SRC_W, SRC_H)
        z1 = max(z1, z0 + 0.10)                     # a still has only the camera to move it
    else:
        src = ['-f', 'lavfi', '-t', '%.3f' % (dur + 0.2), '-i', 'color=c=0x0D0813:s=%dx%d:r=%d' % (SRC_W, SRC_H, FPS)]
        head = '[0:v]null'
        print('  ! segment %d has no footage (%s) and no still: black' % (i, seg.get('film')))
    inputs += src
    cover = max(ow / SRC_W, oh / SRC_H)            # 9:16 crops the 16:9 picture, never letterboxes it
    zt = "(%f+(%f)*t/%f+%f*pow(max(0,1-t/0.18),2))" % (z0, z1 - z0, dur, punch)
    chain = [head + (',scale=w=\'2*trunc(%f*%s/2)\':h=\'2*trunc(%f*%s/2)\':eval=frame:flags=lanczos'
                     % (SRC_W * cover, zt, SRC_H * cover, zt))
             + ',crop=%d:%d:x=\'(iw-%d)*%f\':y=\'(ih-%d)*%f\'' % (ow, oh, ow, fx, oh, fy)
             + ',trim=duration=%f,setsar=1' % dur
             + (',fade=t=in:st=0:d=0.14:color=white' if seg.get('flash') else '')
             + '[v0]']
    last = 'v0'
    if seg.get('caption'):
        text = captions.get(seg['caption'], seg['caption'])
        png = os.path.join(WORK, 'cap_%dx%d_%02d.png' % (ow, oh, i))
        cw, ch = caption_png(text, lang, png, int(ow * 0.86))
        inputs += ['-loop', '1', '-framerate', str(FPS), '-t', '%.3f' % dur, '-i', png]
        # the words pop: in 25% large, settled in 0.12 s
        pop = "(1+0.25*pow(max(0,1-t/0.12),2))"
        cy = oh * (0.80 if oh <= ow else 0.70)
        chain.append("[1:v]format=rgba,scale=w='2*trunc(%d*%s/2)':h='2*trunc(%d*%s/2)':eval=frame:flags=neighbor[cap]"
                     % (cw, pop, ch, pop))
        chain.append("[%s][cap]overlay=x='(main_w-overlay_w)/2':y='%f-overlay_h/2':eval=frame[v1]" % (last, cy))
        last = 'v1'
    run(['ffmpeg', '-y', '-v', 'error'] + inputs + ['-filter_complex', ';'.join(chain), '-map', '[%s]' % last,
         '-an', '-r', str(FPS), '-frames:v', str(int(round(dur * FPS))),
         '-c:v', 'libx264', '-preset', 'medium', '-crf', '12', '-pix_fmt', 'yuv420p', out])
    return out


def build(cut_name, lang='en', tall=False):
    cut = json.load(open(os.path.join(HERE, 'cuts', cut_name + '.json')))
    captions_all = json.load(open(os.path.join(HERE, 'captions.json')))
    captions = dict(captions_all.get('en', {}))
    captions.update(captions_all.get(lang, {}))
    os.makedirs(WORK, exist_ok=True)
    ow, oh = (1080, 1920) if tall else (1920, 1080)
    period, first = beat_grid(cut['music'], cut.get('music_from', 0.0))
    print('%s %s: %s at %.1f bpm' % (cut_name, 'tall' if tall else 'wide', cut['music'], 60 / period))

    filmed = os.path.isdir(FILMS) and any(f.endswith('.mp4') for f in os.listdir(FILMS))
    parts, sfx, t = [], [], 0.0
    for i, seg in enumerate(cut['segments']):
        # a beat the run did not happen to film (no stirred drink was ordered, say) drops out of the real cut
        if seg.get('optional') and filmed and resolve_film(seg) is None:
            print('  - segment %d skipped: no take of %s has %s' % (i, seg.get('film'), seg.get('at')))
            continue
        dur = seg['beats'] * period
        parts.append(render_segment(i, seg, dur, captions, lang, ow, oh))
        for name, at in seg.get('sfx', []):
            sfx.append((name, t + at))
        t += dur
    card_png = os.path.join(WORK, 'end_card_%s_%dx%d.png' % (lang, ow, oh))
    end_card(card_png, lang, captions.get('wishlist', 'WISHLIST ON STEAM'), captions.get('when', ''), tall)
    card_dur = cut.get('end_card', 4.0)
    card = os.path.join(WORK, 'end_card_%dx%d.mp4' % (ow, oh))
    zt = '(1.06-0.06*min(1,t/0.5))'                # the card lands: a little large, settling as it fades up
    run(['ffmpeg', '-y', '-v', 'error', '-loop', '1', '-framerate', str(FPS), '-t', '%.3f' % (card_dur + 0.2), '-i', card_png,
         '-vf', "scale=w='2*trunc(%d*%s/2)':h='2*trunc(%d*%s/2)':eval=frame:flags=lanczos,crop=%d:%d,"
                "trim=duration=%f,fade=t=in:st=0:d=0.12:color=white,setsar=1" % (ow, zt, oh, zt, ow, oh, card_dur),
         '-r', str(FPS), '-frames:v', str(int(round(card_dur * FPS))),
         '-c:v', 'libx264', '-crf', '12', '-pix_fmt', 'yuv420p', card])
    parts.append(card)
    total = t + card_dur

    listing = os.path.join(WORK, 'parts_%dx%d.txt' % (ow, oh))
    with open(listing, 'w') as f:
        for p in parts:
            f.write("file '%s'\n" % p)
    picture = os.path.join(WORK, 'picture_%dx%d.mp4' % (ow, oh))
    run(['ffmpeg', '-y', '-v', 'error', '-f', 'concat', '-safe', '0', '-i', listing, '-c', 'copy', picture])

    a_in = ['-ss', '%.3f' % (first + cut.get('music_offset_beats', 0) * period), '-t', '%.3f' % total,
            '-i', os.path.join(AUDIO, cut['music'] + '.ogg')]
    filt = ['[1:a]afade=t=out:st=%f:d=1.5[m]' % (total - 1.5)]
    mix = ['[m]']
    for k, (name, at) in enumerate(sfx):
        a_in += ['-i', os.path.join(AUDIO, name + '.wav')]
        filt.append('[%d:a]adelay=%d|%d,volume=0.9[s%d]' % (k + 2, int(at * 1000), int(at * 1000), k))
        mix.append('[s%d]' % k)
    filt.append('%samix=inputs=%d:normalize=0:duration=first,loudnorm=I=-14:TP=-1.5:LRA=11[a]'
                % (''.join(mix), len(mix)))
    os.makedirs(OUT, exist_ok=True)
    final = os.path.join(OUT, cut_name + ('' if lang == 'en' else '_' + lang) + ('_tall' if tall else '') + '.mp4')
    run(['ffmpeg', '-y', '-v', 'error', '-i', picture] + a_in +
        ['-filter_complex', ';'.join(filt), '-map', '0:v', '-map', '[a]',
         '-c:v', 'libx264', '-preset', 'slow', '-crf', '16', '-profile:v', 'high', '-pix_fmt', 'yuv420p',
         '-r', str(FPS), '-c:a', 'aac', '-b:a', '320k', '-ar', '48000', '-movflags', '+faststart',
         '-shortest', final])
    print('  -> %s (%.1f s, %.1f MB)' % (os.path.relpath(final, ROOT), total, os.path.getsize(final) / 1e6))
    return final


def main(argv):
    lang = argv[argv.index('--lang') + 1] if '--lang' in argv else 'en'
    names = [a for a in argv if not a.startswith('--') and a != lang]
    if not names:
        names = sorted(f[:-5] for f in os.listdir(os.path.join(HERE, 'cuts')) if f.endswith('.json'))
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
